using System;
using System.Collections.Generic;
using MineIt.Fog;
using MineIt.Input;
using MineIt.Mining;
using MineIt.Models;
using MineIt.World;
using MineIt.Inventory; // NEW

namespace MineIt.Simulation
{
    public sealed class GameSession
    {
        // World constants (MVP)
        public const int WORLD_W_TILES = 512;
        public const int WORLD_H_TILES = 512;

        public GameClock Clock { get; } = new GameClock();
        public PlayerState Player { get; } = new PlayerState();

        public ChunkManager Chunks { get; private set; } = null!;
        public FogOfWar Fog { get; private set; } = null!;
        public DepositManager Deposits { get; private set; } = null!;

        // ===== Inventory (NEW - MVP) =====
        public Backpack Backpack { get; private set; } = null!;

        public int BackpackTier { get; private set; } = 1; // NEW: track tier explicitly

        public int Credits { get; private set; }   // NEW

        public TownStorage TownStorage { get; private set; } = null!;

        // Temporary town definition (MVP):
        public int TownCenterTx { get; private set; }
        public int TownCenterTy { get; private set; }
        public int TownRadiusTiles { get; private set; } = 10;

        // HUD helper
        public bool IsInTownZone { get; private set; }

        // ===== Extraction (NEW) =====
        public bool ExtractInProgress => _extractTargetDepositId != 0;

        public int ExtractTargetDepositId => _extractTargetDepositId;
        public double ExtractKgRemainder { get; private set; } // carried between ticks to avoid loss

        // HUD prompt info (computed each frame)
        public bool CanExtractNow { get; private set; }
        public int ExtractCandidateDepositId { get; private set; } // 0 if none
        public int ExtractCandidateTx { get; private set; }
        public int ExtractCandidateTy { get; private set; }


        // Active radius around camera (chunks)
        public int ActiveChunkRadius { get; set; } = 3;

        // ===== UI feedback events (WPF subscribes; core remains audio-agnostic) =====
        public event Action? ScanExecuted;   // fires ONLY when scan actually runs
        public event Action? ScanDud;        // fires when scan pressed but blocked (cooldown)
        public event Action? ClaimSucceeded; // fires ONLY when claim completes successfully
        public event Action? ClaimFailed;    // fires when claim cannot be started / is canceled / etc.

        // ===== Scan debug / HUD fields =====
        public List<ScanResult> LastScanResults { get; private set; } = new List<ScanResult>();

        // Scan ring flash
        public double LastScanFlashSeconds { get; private set; }
        public int LastScanCenterTx { get; private set; }
        public int LastScanCenterTy { get; private set; }
        public int LastScanRadiusTiles { get; private set; }

        // Scan cooldown
        public double ScanCooldownRemainingSeconds { get; private set; }
        public double ScanCooldownMaxSeconds => Player.DetectorCooldownSeconds;

        // ===== Claim prompt + channel (NEW) =====
        public const double ClaimChannelTotalSeconds = 3.0;

        // True while channeling
        public bool ClaimInProgress => _claimTargetDepositId != 0 && ClaimChannelRemainingSeconds > 0;

        // Remaining time on channel (counts down to 0)
        public double ClaimChannelRemainingSeconds { get; private set; }

        // HUD prompt info (computed each frame)
        public bool CanClaimNow { get; private set; }
        public int ClaimCandidateDepositId { get; private set; } // 0 if none
        public int ClaimCandidateTx { get; private set; }
        public int ClaimCandidateTy { get; private set; }

        // Last action HUD line (debug)
        public string LastActionText { get; private set; } = "";
        public double LastActionFlashSeconds { get; private set; }

        private int _seed;
        private Random _scanRng = null!;

        // claim internal
        private int _claimTargetDepositId; // 0 means none
        private int _claimStartTx;
        private int _claimStartTy;

        // extraction internal (NEW)
        private int _extractTargetDepositId; // 0 means none
        private double _extractKgCarry;      // fractional kg carry to avoid loss

        public void InitializeNewGame(int seed)
        {
            _seed = seed;
            _scanRng = new Random(seed ^ 0x5A17C3D1);

            Chunks = new ChunkManager(seed, WORLD_W_TILES, WORLD_H_TILES, cacheMaxChunks: 256);
            Fog = new FogOfWar(WORLD_W_TILES, WORLD_H_TILES);
            Deposits = new DepositManager(seed);
            BackpackTier = 1;
            Backpack = new Backpack { CapacityKg = UpgradeCatalog.BackpackCapacityKgForTier(BackpackTier) };
            TownStorage = new TownStorage();

            // MVP: define "town" as the start location (later: real town biome/parcel)
            TownCenterTx = WORLD_W_TILES / 2;
            TownCenterTy = WORLD_H_TILES / 2;

            // Start at town center
            Player.PositionX = TownCenterTx;
            Player.PositionY = TownCenterTy;

            // Ensure initial chunks loaded
            Chunks.EnsureActiveRadius(Player.PositionX, Player.PositionY, ActiveChunkRadius);

            // Populate deposits for already-loaded chunks (first frame)
            PopulateDepositsForActiveChunks();

            // ===== TESTING: guaranteed nearby deposit so scan + claim are always testable =====
            {
                int pTx = (int)Math.Floor(Player.PositionX);
                int pTy = (int)Math.Floor(Player.PositionY);

                // Place 2 tiles east so Tier-1 radius=4 will definitely hit it
                int dTx = Math.Min(pTx + 2, WORLD_W_TILES - 2);
                int dTy = pTy;

                var cc = ChunkManager.TileToChunk(dTx, dTy);
                var ch = Chunks.GetOrLoadChunk(cc.Cx, cc.Cy);

                const int debugId = 7777777;

                var debugDeposit = new Deposit
                {
                    DepositId = debugId,
                    OreTypeId = "iron",
                    CenterTx = dTx,
                    CenterTy = dTy,
                    DepthMeters = 80,
                    SizeTier = 8,
                    RemainingUnits = 224,
                    DiscoveredByPlayer = false
                };

                Deposits.AddOrReplaceDepositToChunk(ch, debugDeposit);
            }

            LastActionText = "NEW GAME";
            LastActionFlashSeconds = 1.5;

            // reset claim
            _claimTargetDepositId = 0;
            ClaimChannelRemainingSeconds = 0;
            // reset extraction
            _extractTargetDepositId = 0;
            _extractKgCarry = 0.0;
            ExtractKgRemainder = 0.0;

            Credits = 0; // MVP start
        }

        public void Update(double dtSeconds, InputSnapshot input)
        {
            Clock.Advance(dtSeconds);

            // Ensure chunk streaming around player/camera
            Chunks.EnsureActiveRadius(Player.PositionX, Player.PositionY, ActiveChunkRadius);
            PopulateDepositsForActiveChunks();

            // ----- Movement -----
            double vx = 0, vy = 0;
            if (input.MoveUp) vy -= 1;
            if (input.MoveDown) vy += 1;
            if (input.MoveLeft) vx -= 1;
            if (input.MoveRight) vx += 1;

            double len = Math.Sqrt(vx * vx + vy * vy);
            if (len > 1e-9) { vx /= len; vy /= len; }

            double nextX = Player.PositionX + vx * Player.MoveSpeedTilesPerSec * dtSeconds;
            double nextY = Player.PositionY + vy * Player.MoveSpeedTilesPerSec * dtSeconds;

            nextX = Math.Clamp(nextX, 0, WORLD_W_TILES - 1);
            nextY = Math.Clamp(nextY, 0, WORLD_H_TILES - 1);

            Player.PositionX = nextX;
            Player.PositionY = nextY;

            // Player tile coords for this frame (compute ONCE and reuse)
            int playerTx = (int)Math.Floor(Player.PositionX);
            int playerTy = (int)Math.Floor(Player.PositionY);

            // ----- Upgrade hotkeys (town-only, costs Credits) -----
            // Hotkeys are one-frame commands routed via InputSnapshot (UI does not mutate state directly).
            if (IsInTownZone)
            {
                TryPurchaseUpgrades(input);
            }
            else
            {
                // If player presses upgrade keys outside town, give feedback (optional but useful)
                if ((input.DevSetDetectorTier is >= 1 and <= 5) ||
                    (input.DevSetExtractorTier is >= 1 and <= 5) ||
                    (input.DevSetBackpackTier is >= 1 and <= 5))
                {
                    LastActionText = "UPGRADE BLOCKED (not in town)";
                    LastActionFlashSeconds = 1.5;
                }
            }


            // Town zone (temporary MVP)
            {
                int dxT = playerTx - TownCenterTx;
                int dyT = playerTy - TownCenterTy;
                IsInTownZone = (dxT * dxT + dyT * dyT) <= TownRadiusTiles * TownRadiusTiles;
            }

            // ----- Fog-of-war -----
            Fog.ClearVisibleNow();
            int vision = Clock.IsNight ? 6 : 9; // MVP: no equipped light yet
            Fog.RevealCircle(playerTx, playerTy, vision);

            // ----- Compute claim candidate for HUD prompt (NEW) -----
            ComputeClaimCandidate(playerTx, playerTy);
            // ----- Compute extract candidate for HUD prompt (NEW) -----
            ComputeExtractCandidate(playerTx, playerTy);


            // ----- Scan action -----
            if (input.ScanPressed)
            {
                bool scanExecuted = false;

                if (ScanCooldownRemainingSeconds <= 0.0)
                {
                    // Execute scan
                    LastScanCenterTx = playerTx;
                    LastScanCenterTy = playerTy;
                    LastScanRadiusTiles = Player.DetectorRadiusTiles;
                    LastScanFlashSeconds = 0.35;

                    LastScanResults = Deposits.Scan(
                        Chunks,
                        scanCenterTx: playerTx,
                        scanCenterTy: playerTy,
                        radiusTiles: Player.DetectorRadiusTiles,
                        maxDepthMeters: Player.DetectorMaxDepthMeters,
                        sizeNoiseTiers: Player.DetectorSizeNoiseTiers,
                        rng: _scanRng);

                    foreach (var sr in LastScanResults)
                        MarkDepositDiscoveredById(sr.DepositId);

                    ScanCooldownRemainingSeconds = Player.DetectorCooldownSeconds;
                    scanExecuted = true;

                    LastActionText = $"SCAN OK  hits={LastScanResults.Count}";
                    LastActionFlashSeconds = 1.5;
                }
                else
                {
                    LastActionText = $"SCAN BLOCKED  cd={ScanCooldownRemainingSeconds:0.0}s";
                    LastActionFlashSeconds = 1.5;
                }

                if (scanExecuted) ScanExecuted?.Invoke();
                else ScanDud?.Invoke();
            }

            // ----- Claim input (NEW: starts/cancels 3-second channel) -----
            if (input.ClaimPressed)
            {
                if (ClaimInProgress)
                {
                    // Toggle cancel
                    CancelClaim("CLAIM CANCELED");
                    ClaimFailed?.Invoke();
                }
                else
                {
                    // Start only if candidate exists
                    if (CanClaimNow && ClaimCandidateDepositId != 0)
                    {
                        StartClaim(playerTx, playerTy, ClaimCandidateDepositId);
                        LastActionText = "CLAIM START";
                        LastActionFlashSeconds = 1.5;
                    }
                    else
                    {
                        LastActionText = "CLAIM FAILED (not in range)";
                        LastActionFlashSeconds = 1.5;
                        ClaimFailed?.Invoke();
                    }
                }
            }
            // ----- Deposit backpack into town storage (NEW) -----
            if (input.DepositPressed)
            {
                if (!IsInTownZone)
                {
                    LastActionText = "DEPOSIT FAILED (not in town)";
                    LastActionFlashSeconds = 1.5;
                }
                else
                {
                    int movedStacks = 0;

                    // Copy keys to avoid modifying during enumeration
                    var keys = new List<string>(Backpack.OreUnits.Keys);
                    foreach (var oreId in keys)
                    {
                        int units = Backpack.GetUnits(oreId);
                        if (units <= 0) continue;

                        TownStorage.AddOreUnits(oreId, units);
                        movedStacks++;

                        // Remove from backpack by clearing and re-adding others is heavy,
                        // so for MVP just clear and re-add via a new method below.
                    }

                    // MVP: simplest deterministic approach: clear backpack after transfer
                    // (This assumes ALL backpack contents are ore only.)
                    Backpack.Clear();

                    LastActionText = movedStacks > 0 ? "DEPOSIT OK" : "DEPOSIT (nothing)";
                    LastActionFlashSeconds = 1.5;
                }
            }


            // ----- Extract input (NEW: toggles continuous extraction) -----
            if (input.ExtractPressed)
            {
                if (ExtractInProgress)
                {
                    StopExtraction("EXTRACT STOP");
                }
                else
                {
                    if (CanExtractNow && ExtractCandidateDepositId != 0)
                    {
                        StartExtraction(ExtractCandidateDepositId);
                        LastActionText = "EXTRACT START";
                        LastActionFlashSeconds = 1.5;
                    }
                    else
                    {
                        LastActionText = "EXTRACT FAILED (not eligible)";
                        LastActionFlashSeconds = 1.5;
                    }
                }
            }
            // ----- Sell town storage ore for credits (NEW) -----
            if (input.SellPressed)
            {
                if (!IsInTownZone)
                {
                    LastActionText = "SELL FAILED (not in town)";
                    LastActionFlashSeconds = 1.5;
                }
                else
                {
                    int totalCredits = 0;
                    int stacks = 0;

                    var keys = new List<string>(TownStorage.OreUnits.Keys);
                    foreach (var oreId in keys)
                    {
                        int units = TownStorage.GetUnits(oreId);
                        if (units <= 0) continue;

                        int price = OreCatalog.BasePricePerUnit(oreId);
                        totalCredits += units * price;
                        stacks++;
                    }

                    if (totalCredits > 0)
                    {
                        Credits += totalCredits;
                        TownStorage.Clear();   // see next step
                        LastActionText = $"SOLD {stacks} STACKS  +{totalCredits} cr";
                    }
                    else
                    {
                        LastActionText = "SELL (nothing)";
                    }

                    LastActionFlashSeconds = 1.5;
                }
            }


            // ----- Claim channel tick (NEW) -----
            if (ClaimInProgress)
            {
                // Interrupt conditions (per your doc: movement interrupts; also leaving range interrupts)
                bool movedThisFrame = (Math.Abs(vx) > 1e-9) || (Math.Abs(vy) > 1e-9);
                if (movedThisFrame)
                {
                    CancelClaim("CLAIM INTERRUPTED (moved)");
                    ClaimFailed?.Invoke();
                }
                else if (!IsStillInClaimRange(playerTx, playerTy, _claimTargetDepositId))
                {
                    CancelClaim("CLAIM INTERRUPTED (out of range)");
                    ClaimFailed?.Invoke();
                }
                else
                {
                    ClaimChannelRemainingSeconds -= dtSeconds;
                    if (ClaimChannelRemainingSeconds <= 0)
                    {
                        ClaimChannelRemainingSeconds = 0;

                        bool ok = CompleteClaim(_claimTargetDepositId);
                        _claimTargetDepositId = 0;

                        if (ok)
                        {
                            LastActionText = "CLAIM OK";
                            LastActionFlashSeconds = 1.5;
                            ClaimSucceeded?.Invoke();
                        }
                        else
                        {
                            LastActionText = "CLAIM FAILED (lost eligibility)";
                            LastActionFlashSeconds = 1.5;
                            ClaimFailed?.Invoke();
                        }
                    }
                }
            }

            // ----- Extraction tick (NEW: continuous while active) -----
            if (ExtractInProgress)
            {
                // Interrupt on movement or leaving range, or deposit invalid, or backpack full.
                bool movedThisFrame = (System.Math.Abs(vx) > 1e-9) || (System.Math.Abs(vy) > 1e-9);
                if (movedThisFrame)
                {
                    StopExtraction("EXTRACT INTERRUPTED (moved)");
                }
                else
                {
                    var d = Deposits.TryGetDepositById(_extractTargetDepositId);
                    if (d == null)
                    {
                        StopExtraction("EXTRACT INTERRUPTED (missing)");
                    }
                    else if (!IsStillEligibleForExtraction(playerTx, playerTy, d))
                    {
                        StopExtraction("EXTRACT INTERRUPTED (eligibility)");
                    }
                    else if (Backpack.IsFull)
                    {
                        StopExtraction("EXTRACT STOP (backpack full)");
                    }
                    else
                    {
                        TickExtraction(dtSeconds, d);
                    }
                }
            }




            // ----- Timers -----
            if (LastScanFlashSeconds > 0)
            {
                LastScanFlashSeconds -= dtSeconds;
                if (LastScanFlashSeconds < 0) LastScanFlashSeconds = 0;
            }

            if (ScanCooldownRemainingSeconds > 0)
            {
                ScanCooldownRemainingSeconds -= dtSeconds;
                if (ScanCooldownRemainingSeconds < 0) ScanCooldownRemainingSeconds = 0;
            }

            if (LastActionFlashSeconds > 0)
            {
                LastActionFlashSeconds -= dtSeconds;
                if (LastActionFlashSeconds < 0) LastActionFlashSeconds = 0;
            }
        }

        private void PopulateDepositsForActiveChunks()
        {
            int centerTx = (int)Math.Floor(Player.PositionX);
            int centerTy = (int)Math.Floor(Player.PositionY);
            var center = ChunkManager.TileToChunk(centerTx, centerTy);

            for (int dy = -ActiveChunkRadius; dy <= ActiveChunkRadius; dy++)
                for (int dx = -ActiveChunkRadius; dx <= ActiveChunkRadius; dx++)
                {
                    int cx = center.Cx + dx;
                    int cy = center.Cy + dy;

                    Chunk ch;
                    try { ch = Chunks.GetOrLoadChunk(cx, cy); }
                    catch { continue; }

                    // Populate at most once per chunk (requires Chunk.DepositsPopulated in Chunk.cs)
                    if (!ch.DepositsPopulated)
                    {
                        Deposits.PopulateChunkDeposits(ch);
                        ch.DepositsPopulated = true;
                    }
                }
        }

        private void MarkDepositDiscoveredById(int depositId)
        {
            var d = Deposits.TryGetDepositById(depositId);
            if (d != null) d.DiscoveredByPlayer = true;
        }

        // ===== Claim candidate + channel helpers (NEW) =====

        private const int ClaimRangeTiles = 2;

        private void ComputeClaimCandidate(int playerTx, int playerTy)
        {
            CanClaimNow = false;
            ClaimCandidateDepositId = 0;

            int bestDist2 = int.MaxValue;
            Deposit? best = null;

            foreach (var ch in Chunks.GetLoadedChunks())
                foreach (var d in ch.Deposits)
                {
                    if (!d.DiscoveredByPlayer) continue;
                    if (d.RemainingUnits <= 0) continue;
                    if (d.ClaimedByPlayer) continue;
                    if (d.ClaimedByNpcId.HasValue) continue;

                    int dx = d.CenterTx - playerTx;
                    int dy = d.CenterTy - playerTy;
                    int dist2 = dx * dx + dy * dy;

                    if (dist2 <= ClaimRangeTiles * ClaimRangeTiles && dist2 < bestDist2)
                    {
                        bestDist2 = dist2;
                        best = d;
                    }
                }

            if (best != null)
            {
                CanClaimNow = true;
                ClaimCandidateDepositId = best.DepositId;
                ClaimCandidateTx = best.CenterTx;
                ClaimCandidateTy = best.CenterTy;
            }
        }

        private void StartClaim(int playerTx, int playerTy, int depositId)
        {
            _claimTargetDepositId = depositId;
            ClaimChannelRemainingSeconds = ClaimChannelTotalSeconds;
            _claimStartTx = playerTx;
            _claimStartTy = playerTy;
        }

        private void CancelClaim(string reason)
        {
            _claimTargetDepositId = 0;
            ClaimChannelRemainingSeconds = 0;
            LastActionText = reason;
            LastActionFlashSeconds = 1.5;
        }

        private bool IsStillInClaimRange(int playerTx, int playerTy, int depositId)
        {
            var d = Deposits.TryGetDepositById(depositId);
            if (d == null) return false;

            // still eligible?
            if (!d.DiscoveredByPlayer) return false;
            if (d.RemainingUnits <= 0) return false;
            if (d.ClaimedByPlayer) return false;
            if (d.ClaimedByNpcId.HasValue) return false;

            int dx = d.CenterTx - playerTx;
            int dy = d.CenterTy - playerTy;
            int dist2 = dx * dx + dy * dy;
            return dist2 <= ClaimRangeTiles * ClaimRangeTiles;
        }

        private bool CompleteClaim(int depositId)
        {
            var d = Deposits.TryGetDepositById(depositId);
            if (d == null) return false;

            // re-check eligibility at completion time
            if (!d.DiscoveredByPlayer) return false;
            if (d.RemainingUnits <= 0) return false;
            if (d.ClaimedByPlayer) return false;
            if (d.ClaimedByNpcId.HasValue) return false;

            d.ClaimedByPlayer = true;
            return true;
        }

        // ===== Extraction helpers (NEW) =====

        private void ComputeExtractCandidate(int playerTx, int playerTy)
        {
            CanExtractNow = false;
            ExtractCandidateDepositId = 0;

            int bestDist2 = int.MaxValue;
            Deposit? best = null;

            foreach (var ch in Chunks.GetLoadedChunks())
                foreach (var d in ch.Deposits)
                {
                    if (!d.DiscoveredByPlayer) continue;
                    if (d.RemainingUnits <= 0) continue;

                    // Must be player-claimed for MVP extraction
                    if (!d.ClaimedByPlayer) continue;

                    // Must be in extractor depth capability
                    if (d.DepthMeters > Player.ExtractorMaxDepthMeters) continue;

                    int dx = d.CenterTx - playerTx;
                    int dy = d.CenterTy - playerTy;
                    int dist2 = dx * dx + dy * dy;

                    int r = Player.ExtractorRangeTiles;
                    if (dist2 <= r * r && dist2 < bestDist2)
                    {
                        bestDist2 = dist2;
                        best = d;
                    }
                }

            if (best != null)
            {
                CanExtractNow = true;
                ExtractCandidateDepositId = best.DepositId;
                ExtractCandidateTx = best.CenterTx;
                ExtractCandidateTy = best.CenterTy;
            }
        }

        private bool IsStillEligibleForExtraction(int playerTx, int playerTy, Deposit d)
        {
            if (!d.DiscoveredByPlayer) return false;
            if (d.RemainingUnits <= 0) return false;
            if (!d.ClaimedByPlayer) return false;
            if (d.ClaimedByNpcId.HasValue) return false; // future-proof

            if (d.DepthMeters > Player.ExtractorMaxDepthMeters) return false;

            int dx = d.CenterTx - playerTx;
            int dy = d.CenterTy - playerTy;
            int dist2 = dx * dx + dy * dy;

            int r = Player.ExtractorRangeTiles;
            return dist2 <= r * r;
        }

        private void StartExtraction(int depositId)
        {
            _extractTargetDepositId = depositId;
            _extractKgCarry = 0.0;
            ExtractKgRemainder = 0.0;
        }

        private void StopExtraction(string reason)
        {
            _extractTargetDepositId = 0;
            _extractKgCarry = 0.0;
            ExtractKgRemainder = 0.0;

            LastActionText = reason;
            LastActionFlashSeconds = 1.5;
        }

        private void TickExtraction(double dtSeconds, Deposit d)
        {
            // MVP: no MiningSkill yet. When you add skills, apply bonus here.
            double baseRateKgPerSec = Player.ExtractorRateKgPerSec;

            // Difficulty from catalog (later: from ores.json)
            double diff = OreCatalog.ExtractionDifficulty(d.OreTypeId);
            if (diff < 1e-6) diff = 1.0;

            double rateKgPerSec = baseRateKgPerSec / diff;
            if (rateKgPerSec < 1e-6) rateKgPerSec = 1e-6;

            double kgThisTick = rateKgPerSec * dtSeconds;

            // Convert kg -> units with carry to avoid fractional loss
            double unitKg = OreCatalog.UnitMassKg(d.OreTypeId);
            if (unitKg < 1e-6) unitKg = 1.0;

            _extractKgCarry += kgThisTick;

            int unitsPotential = (int)System.Math.Floor(_extractKgCarry / unitKg);
            if (unitsPotential <= 0)
            {
                ExtractKgRemainder = _extractKgCarry;
                return;
            }

            // Bound by deposit remaining
            int unitsByDeposit = System.Math.Min(unitsPotential, d.RemainingUnits);
            if (unitsByDeposit <= 0)
            {
                StopExtraction("EXTRACT STOP (depleted)");
                return;
            }

            // Bound by backpack capacity
            int unitsAdded = Backpack.AddOreUnitsClamped(d.OreTypeId, unitsByDeposit);
            if (unitsAdded <= 0)
            {
                StopExtraction("EXTRACT STOP (backpack full)");
                return;
            }

            // Apply effects
            d.RemainingUnits -= unitsAdded;

            // Spend kg carry for the units we actually added
            _extractKgCarry -= unitsAdded * unitKg;
            if (_extractKgCarry < 0) _extractKgCarry = 0;

            ExtractKgRemainder = _extractKgCarry;

            // Auto-stop if deposit depleted
            if (d.RemainingUnits <= 0)
            {
                d.RemainingUnits = 0;
                StopExtraction("EXTRACT STOP (depleted)");
            }
        }

        private void TryPurchaseUpgrades(InputSnapshot input)
        {
            // Detector upgrade request
            if (input.DevSetDetectorTier is >= 1 and <= 5)
            {
                int desired = input.DevSetDetectorTier;
                if (desired > Player.DetectorTier)
                {
                    int cost = MineIt.Inventory.UpgradeCatalog.DetectorPriceForTier(desired);
                    if (Credits >= cost)
                    {
                        Credits -= cost;
                        Player.DetectorTier = desired;
                        LastActionText = $"BOUGHT Detector T{desired}  -{cost} cr";
                    }
                    else
                    {
                        LastActionText = $"NEED {cost} cr for Detector T{desired}";
                    }
                    LastActionFlashSeconds = 1.5;
                }
            }

            // Extractor upgrade request
            if (input.DevSetExtractorTier is >= 1 and <= 5)
            {
                int desired = input.DevSetExtractorTier;
                if (desired > Player.ExtractorTier)
                {
                    int cost = MineIt.Inventory.UpgradeCatalog.ExtractorPriceForTier(desired);
                    if (Credits >= cost)
                    {
                        Credits -= cost;
                        Player.ExtractorTier = desired;
                        LastActionText = $"BOUGHT Extractor T{desired}  -{cost} cr";
                    }
                    else
                    {
                        LastActionText = $"NEED {cost} cr for Extractor T{desired}";
                    }
                    LastActionFlashSeconds = 1.5;
                }
            }

            // Backpack upgrade request
            if (input.DevSetBackpackTier is >= 1 and <= 5)
            {
                int desired = input.DevSetBackpackTier;
                if (desired > BackpackTier)
                {
                    int cost = MineIt.Inventory.UpgradeCatalog.BackpackPriceForTier(desired);
                    if (Credits >= cost)
                    {
                        Credits -= cost;
                        BackpackTier = desired;

                        double cap = MineIt.Inventory.UpgradeCatalog.BackpackCapacityKgForTier(desired);
                        Backpack.CapacityKg = System.Math.Max(cap, Backpack.CurrentKg); // never invalidate current weight

                        LastActionText = $"BOUGHT Backpack T{desired} ({Backpack.CapacityKg:0}kg)  -{cost} cr";
                    }
                    else
                    {
                        LastActionText = $"NEED {cost} cr for Backpack T{desired}";
                    }
                    LastActionFlashSeconds = 1.5;
                }
            }
        }


    }
}
