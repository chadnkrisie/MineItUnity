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

        public NpcMinerManager Npcs { get; private set; } = null!;

        // MVP tuning: start small; you can raise later
        public int NpcMinerCount { get; set; } = 12;

        // ===== Vault Authentication (Win gating) =====
        public bool HasWon { get; private set; }

        public bool VaultAuthInProgress { get; private set; }
        public double VaultAuthRemainingSeconds { get; private set; }

        // Tuning: how long the Vault takes to authenticate once all 6 are deposited
        public const double VaultAuthTotalSeconds = 10.0;
        // ============================================


        // ===== Inventory (NEW - MVP) =====
        public Backpack Backpack { get; private set; } = null!;

        public int BackpackTier { get; private set; } = 1; // NEW: track tier explicitly


        public int Credits { get; private set; }   // NEW
        /// <summary>
        /// Attempts to spend credits. Returns true if successful.
        /// </summary>
        public bool TrySpendCredits(int amount)
        {
            if (amount <= 0)
                return true;

            if (Credits < amount)
                return false;

            Credits -= amount;
            return true;
        }

        /// <summary>
        /// Adds credits (use for rewards, sales, etc).
        /// </summary>
        public void AddCredits(int amount)
        {
            if (amount <= 0)
                return;

            Credits += amount;
        }

        // ===== Town actions (sanctioned methods Unity UI may call) =====

        public bool TryDepositBackpackToTown(out int movedStacks)
        {
            movedStacks = 0;

            if (!IsInTownZone)
            {
                PostStatus("DEPOSIT FAILED (not in town)", 1.5);
                return false;
            }

            movedStacks = Backpack.TransferAllTo(TownStorage);

            PostStatus(movedStacks > 0 ? "DEPOSIT OK" : "DEPOSIT (nothing)", 1.5);
            return true;
        }

        public bool TrySellTownOre(out int creditsGained, out int stacksSold)
        {
            creditsGained = 0;
            stacksSold = 0;

            if (!IsInTownZone)
            {
                PostStatus("SELL FAILED (not in town)", 1.5);
                return false;
            }

            var result = TownStorage.ComputeSaleValueAndClear();
            creditsGained = result.creditsGained;
            stacksSold = result.stacksSold;

            if (creditsGained > 0)
            {
                Credits += creditsGained;
                PostStatus($"SOLD {stacksSold} STACKS  +{creditsGained} cr", 1.5);
                return true;
            }

            PostStatus("SELL (nothing)", 1.5);
            return true;
        }

        public bool TryBuyDetectorTier(int desiredTier)
        {
            if (!IsInTownZone)
            {
                PostStatus("UPGRADE BLOCKED (not in town)", 1.5);
                return false;
            }

            if (desiredTier <= Player.DetectorTier)
                return false;

            int cost = MineIt.Inventory.UpgradeCatalog.DetectorPriceForTier(desiredTier);
            if (Credits < cost)
            {
                PostStatus($"NEED {cost} cr for Detector T{desiredTier}", 1.5);
                return false;
            }

            Credits -= cost;
            Player.DetectorTier = desiredTier;
            PostStatus($"BOUGHT Detector T{desiredTier}  -{cost} cr", 1.5);
            return true;
        }

        public bool TryBuyExtractorTier(int desiredTier)
        {
            if (!IsInTownZone)
            {
                PostStatus("UPGRADE BLOCKED (not in town)", 1.5);
                return false;
            }

            if (desiredTier <= Player.ExtractorTier)
                return false;

            int cost = MineIt.Inventory.UpgradeCatalog.ExtractorPriceForTier(desiredTier);
            if (Credits < cost)
            {
                PostStatus($"NEED {cost} cr for Extractor T{desiredTier}", 1.5);
                return false;
            }

            Credits -= cost;
            Player.ExtractorTier = desiredTier;
            PostStatus($"BOUGHT Extractor T{desiredTier}  -{cost} cr", 1.5);
            return true;
        }

        public bool TryBuyBackpackTier(int desiredTier)
        {
            if (!IsInTownZone)
            {
                PostStatus("UPGRADE BLOCKED (not in town)", 1.5);
                return false;
            }

            if (desiredTier <= BackpackTier)
                return false;

            int cost = MineIt.Inventory.UpgradeCatalog.BackpackPriceForTier(desiredTier);
            if (Credits < cost)
            {
                PostStatus($"NEED {cost} cr for Backpack T{desiredTier}", 1.5);
                return false;
            }

            Credits -= cost;
            BackpackTier = desiredTier;

            double cap = MineIt.Inventory.UpgradeCatalog.BackpackCapacityKgForTier(desiredTier);
            Backpack.CapacityKg = System.Math.Max(cap, Backpack.CurrentKg);

            PostStatus($"BOUGHT Backpack T{desiredTier} ({Backpack.CapacityKg:0}kg)  -{cost} cr", 1.5);
            return true;
        }



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
        public event Action ScanExecuted;   // fires ONLY when scan actually runs
        public event Action ScanDud;        // fires when scan pressed but blocked (cooldown)
        public event Action ClaimSucceeded; // fires ONLY when claim completes successfully
        public event Action ClaimFailed;    // fires when claim cannot be started / is canceled / etc.
        public event Action VictoryAchieved;

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

        // ===== Contested Window / Afterglow (Per-Deposit, Locked) =====
        // Each scan creates an afterglow on each detected deposit for 90 seconds.
        // NPCs may claim ONLY deposits that are:
        //   - discovered by player, and
        //   - currently within that deposit’s afterglow expiry window, and
        //   - were revealed by a scan (i.e., entered/extended by scan scheduling here).
        public const double ContestedWindowTotalSeconds = 90.0;

        // Deterministic scan sequence counter (increments only on successful scans).
        private int _scanSequence;

        // Per-deposit afterglow + scheduled NPC claim attempt.
        private readonly Dictionary<int, ContestedDeposit> _contestedDeposits = new Dictionary<int, ContestedDeposit>();

        private struct ContestedDeposit
        {
            public int DepositId;
            public int NpcId;

            // Absolute timestamps in Clock.TotalRealSeconds space
            public double WindowEndRealSeconds; // now + 90 when revealed (may be extended)
            public double ClaimAtRealSeconds;   // now + deterministic delay (15..60) at first reveal
        }

        // HUD helpers (computed in StepNpcAfterglowClaims)
        public int AfterglowActiveSiteCount { get; private set; }
        public double AfterglowMaxRemainingSeconds { get; private set; }
        // ============================================================

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

        public void PostStatus(string text, double seconds = 1.5)
        {
            LastActionText = text ?? "";
            LastActionFlashSeconds = seconds;
        }

        private int _seed;
        public int Seed => _seed;

        private Random _scanRng = null!;

        // claim internal
        private int _claimTargetDepositId; // 0 means none
        private int _claimStartTx;
        private int _claimStartTy;

        // extraction internal (NEW)
        private int _extractTargetDepositId; // 0 means none
        private double _extractKgCarry;      // fractional kg carry to avoid loss

        private struct MovementStep
        {
            public double Vx;
            public double Vy;
            public bool MovedThisFrame;

            public int PlayerTx;
            public int PlayerTy;
        }

        public void InitializeNewGame(int seed)
        {
            _seed = seed;
            _scanRng = new Random(seed ^ 0x5A17C3D1);

            Chunks = new ChunkManager(seed, WORLD_W_TILES, WORLD_H_TILES, cacheMaxChunks: 256);
            Fog = new FogOfWar(WORLD_W_TILES, WORLD_H_TILES);
            Deposits = new DepositManager(seed);

            Npcs = new NpcMinerManager(seed);
            Npcs.InitializeMvpNpcSet(NpcMinerCount);

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

            Credits = 10000000; // MVP start
        }

        public void Update(double dtSeconds, InputSnapshot input)
        {
            // 0) Global time
            Clock.Advance(dtSeconds);

            // 1) Streaming & deposits (must happen early so reads are valid)
            StepChunkStreamingAndDeposits();

            // 2) Movement (authoritative)
            MovementStep mv = StepMovement(dtSeconds, input);

            // 3) Town zone (must be computed BEFORE any town-gated actions)
            StepTownZone(mv.PlayerTx, mv.PlayerTy);

            // 4) Town actions (upgrade / deposit / sell). Uses IsInTownZone.
            StepTownActions(input);

            // ===== Vault Authentication gating =====
            if (!HasWon)
            {
                if (!VaultAuthInProgress)
                {
                    // Start authentication once, when all artifacts are present in the Vault
                    if (HasAllArtifactsInVault())
                    {
                        VaultAuthInProgress = true;
                        VaultAuthRemainingSeconds = VaultAuthTotalSeconds;
                        PostStatus("VAULT AUTH STARTED", 2.0);
                    }
                }
                else
                {
                    // Tick authentication
                    VaultAuthRemainingSeconds -= dtSeconds;
                    if (VaultAuthRemainingSeconds <= 0)
                    {
                        VaultAuthRemainingSeconds = 0;
                        VaultAuthInProgress = false;

                        HasWon = true;
                        PostStatus("DIRECTIVE COMPLETE", 3.0);
                        VictoryAchieved?.Invoke();
                    }
                }
            }
            // ============================================


            // 5) Fog (depends on position + clock)
            StepFog(mv.PlayerTx, mv.PlayerTy);

            // 6) Candidate prompts (depends on updated deposits + position)
            StepCandidates(mv.PlayerTx, mv.PlayerTy);

            // 7) Scan / Claim / Extract inputs
            StepScan(input, mv.PlayerTx, mv.PlayerTy);
            StepClaimInput(input, mv.PlayerTx, mv.PlayerTy);
            StepExtractInput(input);

            // 8) Claim / Extract ticks (continuous behaviors)
            StepClaimTick(dtSeconds, mv.PlayerTx, mv.PlayerTy, mv.MovedThisFrame);
            StepExtractTick(dtSeconds, mv.PlayerTx, mv.PlayerTy, mv.MovedThisFrame);

            // 8.5) NPC miners (headless competition)
            StepNpcMiners(dtSeconds);

            // 8.75) NPC afterglow claims (contested window)
            StepNpcAfterglowClaims();

            // 9) Timers
            StepTimers(dtSeconds);

        }

        private void StepChunkStreamingAndDeposits()
        {
            // Ensure chunk streaming around player/camera
            Chunks.EnsureActiveRadius(Player.PositionX, Player.PositionY, ActiveChunkRadius);
            PopulateDepositsForActiveChunks();
        }

        private MovementStep StepMovement(double dtSeconds, InputSnapshot input)
        {
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

            int playerTx = (int)Math.Floor(Player.PositionX);
            int playerTy = (int)Math.Floor(Player.PositionY);

            bool movedThisFrame = (Math.Abs(vx) > 1e-9) || (Math.Abs(vy) > 1e-9);

            return new MovementStep
            {
                Vx = vx,
                Vy = vy,
                MovedThisFrame = movedThisFrame,
                PlayerTx = playerTx,
                PlayerTy = playerTy
            };
        }

        private void StepTownZone(int playerTx, int playerTy)
        {
            int dxT = playerTx - TownCenterTx;
            int dyT = playerTy - TownCenterTy;
            IsInTownZone = (dxT * dxT + dyT * dyT) <= TownRadiusTiles * TownRadiusTiles;
        }

        private void StepTownActions(InputSnapshot input)
        {
            // ----- Upgrade hotkeys (town-only, costs Credits) -----
            if (IsInTownZone)
            {
                TryPurchaseUpgrades(input);
            }
            else
            {
                if ((input.DevSetDetectorTier is >= 1 and <= 5) ||
                    (input.DevSetExtractorTier is >= 1 and <= 5) ||
                    (input.DevSetBackpackTier is >= 1 and <= 5))
                {
                    LastActionText = "UPGRADE BLOCKED (not in town)";
                    LastActionFlashSeconds = 1.5;
                }
            }

            // ----- Deposit backpack into town storage -----
            if (input.DepositPressed)
            {
                if (!IsInTownZone)
                {
                    LastActionText = "DEPOSIT FAILED (not in town)";
                    LastActionFlashSeconds = 1.5;
                }
                else
                {
                    int movedStacks = Backpack.TransferAllTo(TownStorage);

                    LastActionText = movedStacks > 0 ? "DEPOSIT OK" : "DEPOSIT (nothing)";
                    LastActionFlashSeconds = 1.5;
                }
            }

            // ----- Sell town storage ore for credits -----
            if (input.SellPressed)
            {
                if (!IsInTownZone)
                {
                    LastActionText = "SELL FAILED (not in town)";
                    LastActionFlashSeconds = 1.5;
                }
                else
                {
                    var result = TownStorage.ComputeSaleValueAndClear();
                    int totalCredits = result.creditsGained;
                    int stacks = result.stacksSold;

                    if (totalCredits > 0)
                    {
                        Credits += totalCredits;
                        LastActionText = $"SOLD {stacks} STACKS  +{totalCredits} cr";
                    }
                    else
                    {
                        LastActionText = "SELL (nothing)";
                    }

                    LastActionFlashSeconds = 1.5;
                }
            }
        }

        private void StepFog(int playerTx, int playerTy)
        {
            Fog.ClearVisibleNow();
            int vision = Clock.IsNight ? 6 : 9; // MVP: no equipped light yet
            Fog.RevealCircle(playerTx, playerTy, vision);
        }

        private void StepCandidates(int playerTx, int playerTy)
        {
            ComputeClaimCandidate(playerTx, playerTy);
            ComputeExtractCandidate(playerTx, playerTy);
        }

        private void StepScan(InputSnapshot input, int playerTx, int playerTy)
        {
            if (!input.ScanPressed)
                return;

            bool scanExecuted = false;

            if (ScanCooldownRemainingSeconds <= 0.0)
            {
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
                    detectorTier: Player.DetectorTier,
                    rng: _scanRng);

                // Enrich scan intelligence tags (NPC/Deep/Urgent/Warning/Artifact/Depleted + ETA)
                EnrichScanResultsWithTagsAndEta();

                // Sort scan results by locked rules:
                // - Uncollected artifacts always above non-artifacts
                // - Otherwise by PriorityScore (highest first)
                LastScanResults.Sort((a, b) =>
                {
                    bool aArt = a != null && a.IsArtifact;
                    bool bArt = b != null && b.IsArtifact;

                    if (aArt != bArt)
                        return aArt ? -1 : 1;

                    double pa = a != null ? a.ComputePriorityScore() : double.NegativeInfinity;
                    double pb = b != null ? b.ComputePriorityScore() : double.NegativeInfinity;
                    return pb.CompareTo(pa);
                });

                // Mark discovered
                foreach (var sr in LastScanResults)
                    MarkDepositDiscoveredById(sr.DepositId);

                // ---- NEW: Schedule per-deposit afterglow + deterministic NPC claims ----
                ScheduleAfterglowForLastScanDeposits();
                // ----------------------------------------------------------------------

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

        private void StepClaimInput(InputSnapshot input, int playerTx, int playerTy)
        {
            if (!input.ClaimPressed)
                return;

            if (ClaimInProgress)
            {
                CancelClaim("CLAIM CANCELED");
                ClaimFailed?.Invoke();
                return;
            }

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

        private void StepExtractInput(InputSnapshot input)
        {
            if (!input.ExtractPressed)
                return;

            if (ExtractInProgress)
            {
                StopExtraction("EXTRACT STOP");
                return;
            }

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

        private void StepClaimTick(double dtSeconds, int playerTx, int playerTy, bool movedThisFrame)
        {
            if (!ClaimInProgress)
                return;

            if (movedThisFrame)
            {
                CancelClaim("CLAIM INTERRUPTED (moved)");
                ClaimFailed?.Invoke();
                return;
            }

            if (!IsStillInClaimRange(playerTx, playerTy, _claimTargetDepositId))
            {
                CancelClaim("CLAIM INTERRUPTED (out of range)");
                ClaimFailed?.Invoke();
                return;
            }

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

        private void StepExtractTick(double dtSeconds, int playerTx, int playerTy, bool movedThisFrame)
        {
            if (!ExtractInProgress)
                return;

            if (movedThisFrame)
            {
                StopExtraction("EXTRACT INTERRUPTED (moved)");
                return;
            }

            var d = Deposits.TryGetDepositById(_extractTargetDepositId);
            if (d == null)
            {
                StopExtraction("EXTRACT INTERRUPTED (missing)");
                return;
            }

            if (!IsStillEligibleForExtraction(playerTx, playerTy, d))
            {
                StopExtraction("EXTRACT INTERRUPTED (eligibility)");
                return;
            }

            if (Backpack.IsFull)
            {
                StopExtraction("EXTRACT STOP (backpack full)");
                return;
            }

            TickExtraction(dtSeconds, d);
        }

        private void StepNpcMiners(double dtSeconds)
        {
            if (Npcs == null) return;
            if (Deposits == null) return;

            Npcs.Tick(dtSeconds, Deposits);
        }

        private void StepTimers(double dtSeconds)
        {
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

        private void EnrichScanResultsWithTagsAndEta()
        {
            var list = LastScanResults;
            if (list == null || list.Count == 0) return;

            // Locked thresholds (architecture)
            const double urgentSec = 180.0;
            const double warningSec = 420.0;

            int pTx = (int)Math.Floor(Player.PositionX);
            int pTy = (int)Math.Floor(Player.PositionY);

            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;

                // Reset tags
                r.TagNpc = false;
                r.TagDeep = false;
                r.TagUrgent = false;
                r.TagWarning = false;
                r.TagArtifact = false;
                r.TagDepleted = false;
                r.DepletionEtaSeconds = -1.0;

                // Artifact / depleted
                if (r.IsArtifact) r.TagArtifact = true;
                if (r.IsDepleted) r.TagDepleted = true;

                // NPC tag
                if (r.ClaimedByNpcId >= 0) r.TagNpc = true;

                // Deep tag = exceeds current extractor depth capability (player guidance)
                if (r.DepthMeters > Player.ExtractorMaxDepthMeters) r.TagDeep = true;

                // If NPC claimed, estimate depletion ETA using NPC extraction rate (deterministic)
                if (r.ClaimedByNpcId >= 0 && Npcs != null && Deposits != null)
                {
                    var d = Deposits.TryGetDepositById(r.DepositId);
                    if (d != null && d.RemainingUnits > 0)
                    {
                        // Find NPC object by id
                        NpcMinerManager.NpcMiner npc = null;
                        var npcs = Npcs.Npcs;
                        if (npcs != null)
                        {
                            for (int n = 0; n < npcs.Count; n++)
                            {
                                if (npcs[n].NpcId == r.ClaimedByNpcId)
                                {
                                    npc = npcs[n];
                                    break;
                                }
                            }
                        }

                        if (npc != null)
                        {
                            double rate = Npcs.GetNpcExtractionRateKgPerSec(npc, d.OreTypeId);
                            double eta = d.EstimateSecondsToDeplete(rate);

                            if (!double.IsInfinity(eta))
                                r.DepletionEtaSeconds = eta;

                            if (eta > 0 && eta < urgentSec) r.TagUrgent = true;
                            else if (eta > 0 && eta < warningSec) r.TagWarning = true;
                        }
                    }
                }

                // DistanceTiles should already be set by DepositManager; if not, recompute defensively.
                if (r.DistanceTiles <= 0)
                {
                    int dx = r.CenterTx - pTx;
                    int dy = r.CenterTy - pTy;
                    r.DistanceTiles = (int)Math.Round(Math.Sqrt(dx * dx + dy * dy));
                }
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
                        Deposits.PopulateChunkDeposits(ch, TownCenterTx, TownCenterTy);
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
            Deposit best = null;

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
            Deposit best = null;

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
                                                         
            // ===== Artifact Tier Gating (Locked) =====
            // Artifacts are extractable ONLY with Extractor Tier 5.
            if (d.IsArtifact && Player.ExtractorTier < 5)
                return false;
            // ========================================

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
            // ===== Artifact extraction (quest items) =====
            if (d.IsArtifact)
            {
                // Locked: require Extractor Tier 5 to extract artifacts.
                if (Player.ExtractorTier < 5)
                {
                    StopExtraction("ARTIFACT LOCKED (need Extractor T5)");
                    return;
                }
                
                // Add to backpack as a unique artifact ID, not as ore.

                string aid = d.ArtifactId ?? "";
                bool added = Backpack.AddArtifact(aid);

                // Deplete deposit immediately (artifact is one-time)
                d.RemainingUnits = 0;
                d.IsDepleted = true;

                StopExtraction(added
                    ? $"ARTIFACT SECURED: {aid}"
                    : $"ARTIFACT ALREADY HAVE: {aid}");

                return;
            }
            // ===========================================

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

            // Auto-stop if deposit depleted (archive marker)
            if (d.RemainingUnits <= 0)
            {
                d.RemainingUnits = 0;
                d.IsDepleted = true;
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

        private static readonly string[] RequiredArtifacts =
{
    "stellar_shard",
    "ancient_lattice",
    "void_compass",
    "quantum_fossil",
    "machine_relic",
    "echo_prism"
};

        private bool HasAllArtifactsInVault()
        {
            if (TownStorage == null) return false;

            for (int i = 0; i < RequiredArtifacts.Length; i++)
            {
                if (!TownStorage.HasArtifact(RequiredArtifacts[i]))
                    return false;
            }
            return true;
        }

        public void LoadFromSave(Save.SaveGameData data)
        {
            if (data == null) return;

            // Re-init deterministic session from seed
            InitializeNewGame(data.Seed);

            HasWon = data.HasWon;
            VaultAuthInProgress = data.VaultAuthInProgress;
            VaultAuthRemainingSeconds = data.VaultAuthRemainingSeconds;

            // Clock
            // We only have Advance() currently; set via repeated advance is dumb.
            // So: set using reflection-free direct assignment by adding a setter-like approach.
            // Minimal: advance to target.
            double target = data.TotalRealSeconds;
            double cur = Clock.TotalRealSeconds;
            if (target > cur)
                Clock.Advance(target - cur);

            // Player + upgrades
            Player.PositionX = data.Player.PositionX;
            Player.PositionY = data.Player.PositionY;

            Player.DetectorTier = data.Player.DetectorTier;
            Player.ExtractorTier = data.Player.ExtractorTier;

            BackpackTier = data.Player.BackpackTier;
            Credits = data.Player.Credits;

            // Ensure backpack capacity matches tier (never invalidates current load)
            double cap = MineIt.Inventory.UpgradeCatalog.BackpackCapacityKgForTier(BackpackTier);
            Backpack.CapacityKg = cap;

            // Inventory
            Backpack.LoadOreUnits(data.BackpackOre);
            TownStorage.LoadOreUnits(data.TownOre);

            Backpack.LoadArtifacts(data.BackpackArtifacts);
            TownStorage.LoadArtifacts(data.TownArtifacts);

            // Fog
            if (!string.IsNullOrEmpty(data.FogDiscoveredBitsBase64))
            {
                byte[] bytes = Convert.FromBase64String(data.FogDiscoveredBitsBase64);
                uint[] bits = new uint[(bytes.Length + 3) / 4];
                Buffer.BlockCopy(bytes, 0, bits, 0, Math.Min(bytes.Length, bits.Length * 4));
                Fog.OverwriteDiscoveredBits(bits);
            }

            // Deposits: apply mutable deltas (chunks/deposits exist as they are loaded)
            // Ensure active chunks are loaded so at least local deposits exist
            Chunks.EnsureActiveRadius(Player.PositionX, Player.PositionY, ActiveChunkRadius);
            PopulateDepositsForActiveChunks();

            foreach (var sd in data.Deposits)
            {
                var d = Deposits.TryGetDepositById(sd.DepositId);
                if (d == null) continue;

                d.RemainingUnits = sd.RemainingUnits;

                // Artifacts
                d.IsArtifact = sd.IsArtifact;
                d.ArtifactId = sd.ArtifactId ?? "";

                d.ClaimedByPlayer = sd.ClaimedByPlayer;
                d.ClaimedByNpcId = (sd.ClaimedByNpcId >= 0) ? sd.ClaimedByNpcId : (int?)null;
                d.DiscoveredByPlayer = sd.DiscoveredByPlayer;
                d.IsDepleted = sd.IsDepleted || (d.RemainingUnits <= 0);
            }

            // NPC miners
            if (data.NpcMiners != null && data.NpcMiners.Count > 0)
            {
                Npcs.LoadFromSave(data.NpcMiners);

                // Re-assert deposit ownership consistency for NPC targets (best-effort):
                // If an NPC has a target deposit, ensure the deposit's ClaimedByNpcId is set.
                for (int i = 0; i < Npcs.Npcs.Count; i++)
                {
                    var npc = Npcs.Npcs[i];
                    if (npc.TargetDepositId == 0) continue;

                    var d = Deposits.TryGetDepositById(npc.TargetDepositId);
                    if (d != null && !d.ClaimedByPlayer)
                    {
                        if (!d.ClaimedByNpcId.HasValue)
                            d.ClaimedByNpcId = npc.NpcId;
                    }
                }
            }
            else
            {
                // No NPC data in old saves -> initialize default NPCs
                Npcs.InitializeMvpNpcSet(NpcMinerCount);
            }

            LastActionText = "LOAD OK";
            LastActionFlashSeconds = 1.5;
        }

        // ===== Contested Window / Afterglow helpers (NEW) =====

        // ===== Contested Window / Afterglow helpers (Per-Deposit) =====

        private void ScheduleAfterglowForLastScanDeposits()
        {
            // Increment only on successful scans (called from StepScan when scan executes).
            _scanSequence++;

            var results = LastScanResults;
            if (results == null || results.Count == 0) return;

            int npcCount = (Npcs != null) ? Npcs.NpcCount : 0;
            if (npcCount <= 0) return;

            double now = Clock.TotalRealSeconds;
            double newEnd = now + ContestedWindowTotalSeconds;

            for (int i = 0; i < results.Count; i++)
            {
                int depositId = results[i].DepositId;

                var d = Deposits.TryGetDepositById(depositId);
                if (d == null) continue;

                // Must be a viable contested candidate
                if (!d.DiscoveredByPlayer) continue;
                if (d.RemainingUnits <= 0) continue;
                if (d.IsArtifact) continue;            // locked: NPC never contest artifacts
                if (d.ClaimedByPlayer) continue;
                if (d.ClaimedByNpcId.HasValue) continue;

                if (_contestedDeposits.TryGetValue(depositId, out var cd))
                {
                    // Already contested: extend window, but DO NOT reroll NPC or delay.
                    if (newEnd > cd.WindowEndRealSeconds)
                        cd.WindowEndRealSeconds = newEnd;

                    _contestedDeposits[depositId] = cd;
                }
                else
                {
                    // First time this deposit becomes vulnerable: choose deterministic delay + npc.
                    double delay = ComputeDeterministicDelaySeconds(_seed, depositId, _scanSequence); // 15..60
                    int npcId = ComputeDeterministicNpcId(_seed, depositId, _scanSequence, npcCount); // 1..NpcCount

                    _contestedDeposits[depositId] = new ContestedDeposit
                    {
                        DepositId = depositId,
                        NpcId = npcId,
                        WindowEndRealSeconds = newEnd,
                        ClaimAtRealSeconds = now + delay
                    };
                }
            }
        }

        private void StepNpcAfterglowClaims()
        {
            // Compute HUD helpers every frame (cheap; dictionary is small).
            AfterglowActiveSiteCount = 0;
            AfterglowMaxRemainingSeconds = 0.0;

            if (_contestedDeposits.Count == 0) return;
            if (Npcs == null || Deposits == null) { _contestedDeposits.Clear(); return; }

            double now = Clock.TotalRealSeconds;

            _tmpContestedKeys.Clear();
            foreach (var kv in _contestedDeposits)
                _tmpContestedKeys.Add(kv.Key);

            for (int i = 0; i < _tmpContestedKeys.Count; i++)
            {
                int depositId = _tmpContestedKeys[i];

                if (!_contestedDeposits.TryGetValue(depositId, out var cd))
                    continue;

                // Expired? Drop.
                if (now > cd.WindowEndRealSeconds)
                {
                    _contestedDeposits.Remove(depositId);
                    continue;
                }

                // Still active (for HUD)
                AfterglowActiveSiteCount++;
                double rem = cd.WindowEndRealSeconds - now;
                if (rem > AfterglowMaxRemainingSeconds)
                    AfterglowMaxRemainingSeconds = rem;

                // Not due yet?
                if (now < cd.ClaimAtRealSeconds)
                    continue;

                // Re-check eligibility at claim time (player may have claimed, etc.)
                var d = Deposits.TryGetDepositById(depositId);
                if (d == null)
                {
                    _contestedDeposits.Remove(depositId);
                    continue;
                }

                if (!d.DiscoveredByPlayer ||
                    d.RemainingUnits <= 0 ||
                    d.IsArtifact ||
                    d.ClaimedByPlayer ||
                    d.ClaimedByNpcId.HasValue)
                {
                    // Not claimable anymore; consume.
                    _contestedDeposits.Remove(depositId);
                    continue;
                }

                // Attempt claim (one-shot). If succeeds, NPC starts extracting via its normal Tick.
                bool ok = Npcs.TryClaimAndStartExtraction(cd.NpcId, cd.DepositId, Deposits);

                // Either way, consume the scheduled claim so we don't retry.
                _contestedDeposits.Remove(depositId);

                // Optional: you can emit a core event here later if you want UI/audio hooks:
                // if (ok) NpcClaimedDeposit?.Invoke(cd.DepositId, cd.NpcId);
            }
        }

        // Reused list (avoid modifying dictionary during enumeration)
        private readonly List<int> _tmpContestedKeys = new List<int>(128);

        // Deterministic helpers unchanged
        private static double ComputeDeterministicDelaySeconds(int seed, int depositId, int scanSeq)
        {
            uint h = Hash3((uint)seed, (uint)depositId, (uint)scanSeq);
            double u01 = (h & 0x00FFFFFFu) / (double)0x01000000;
            return 15.0 + (u01 * 45.0); // 15..60
        }

        private static int ComputeDeterministicNpcId(int seed, int depositId, int scanSeq, int npcCount)
        {
            if (npcCount <= 0) return 1;
            uint h = Hash3((uint)seed ^ 0xA6C1D2E3u, (uint)depositId, (uint)scanSeq);
            int idx = (int)(h % (uint)npcCount);
            return idx + 1;
        }

        private static uint Hash3(uint a, uint b, uint c)
        {
            uint x = 0x9E3779B9u;
            x ^= a + 0x85EBCA6Bu + (x << 6) + (x >> 2);
            x ^= b + 0xC2B2AE35u + (x << 6) + (x >> 2);
            x ^= c + 0x27D4EB2Fu + (x << 6) + (x >> 2);

            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        // ============================================================

        // Reused list to avoid allocations
        private readonly List<int> _tmpDueClaims = new List<int>(64);

        // =====================================================
    }
}
