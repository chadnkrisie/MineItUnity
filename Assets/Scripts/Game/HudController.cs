using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MineIt.Inventory;
using MineIt.Mining;

namespace MineItUnity.Game
{
    /// <summary>
    /// MVP HUD: single TextMeshPro block updated at a fixed rate (cheap).
    /// Reads state from GameSession; does not mutate game state.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;
        public TextMeshProUGUI HudText;

        [Header("Bars (Optional UI Images)")]
        [Tooltip("Parent GameObject for claim bar (enable/disable). Optional.")]
        public GameObject ClaimBarRoot;

        [Tooltip("Fill Image for claim bar (Image Type must be Filled / Horizontal). Optional.")]
        public Image ClaimBarFill;

        [Tooltip("Parent GameObject for scan cooldown bar (enable/disable). Optional.")]
        public GameObject ScanBarRoot;

        [Tooltip("Fill Image for scan cooldown bar (Image Type must be Filled / Horizontal). Optional.")]
        public Image ScanBarFill;

        [Tooltip("Parent GameObject for backpack bar. Optional.")]
        public GameObject BackpackBarRoot;

        [Tooltip("Fill Image for backpack bar (Image Type must be Filled / Horizontal). Optional.")]
        public Image BackpackBarFill;

        [Tooltip("If true, backpack bar only shows while extracting; otherwise it is always visible.")]
        public bool ShowBackpackBarOnlyWhileExtracting = false;

        [Header("Update Rate")]
        [Tooltip("HUD refreshes at this rate (Hz). 10 is plenty and reduces allocations.")]
        public float UpdatesPerSecond = 10f;

        private float _nextUpdateTime;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private void Awake()
        {
            if (UpdatesPerSecond < 1f) UpdatesPerSecond = 1f;
            _nextUpdateTime = 0f;
        }

        private void Update()
        {
            if (Controller == null || Controller.Session == null || HudText == null)
                return;

            // Bars should update smoothly every frame (no allocations).
            UpdateBars(Controller.Session);

            if (Time.unscaledTime < _nextUpdateTime)
                return;

            _nextUpdateTime = Time.unscaledTime + (1f / UpdatesPerSecond);

            var s = Controller.Session;
            BuildHudText(s);
            HudText.text = _sb.ToString();
        }

        private void BuildHudText(MineIt.Simulation.GameSession s)
        {
            _sb.Clear();

            // Time
            double hours = s.Clock.InGameHours;
            int hh = (int)hours;
            int mm = (int)((hours - hh) * 60.0);
            if (mm < 0) mm = 0;
            if (mm > 59) mm = 59;

            bool night = s.Clock.IsNight;
            double dark01 = s.Clock.Darkness01;

            int pTx = Mathf.FloorToInt((float)s.Player.PositionX);
            int pTy = Mathf.FloorToInt((float)s.Player.PositionY);

            int chunkSize = MineIt.World.Chunk.CHUNK_SIZE_TILES;
            int cX = pTx / chunkSize;
            int cY = pTy / chunkSize;

            _sb.Append("Time ")
               .Append(hh.ToString("00")).Append(':').Append(mm.ToString("00"))
               .Append("   ").Append(night ? "NIGHT" : "DAY")
               .Append("   Darkness ").Append((dark01 * 100.0).ToString("0")).Append('%')
               .AppendLine();

            _sb.Append("Player Tile (").Append(pTx).Append(',').Append(pTy).Append(")")
               .Append("   Chunk (").Append(cX).Append(',').Append(cY).Append(')')
               .AppendLine();

            // Detector + Extractor
            int detTier = s.Player.DetectorTier;
            int detRad = s.Player.DetectorRadiusTiles;
            int detDepth = s.Player.DetectorMaxDepthMeters;

            int exTier = s.Player.ExtractorTier;
            int exRange = s.Player.ExtractorRangeTiles;
            int exDepth = s.Player.ExtractorMaxDepthMeters;
            double exRate = s.Player.ExtractorRateKgPerSec;

            _sb.Append("Detector T").Append(detTier)
               .Append("   R ").Append(detRad).Append("   D ").Append(detDepth).Append("m")
               .AppendLine();

            _sb.Append("Extractor T").Append(exTier)
               .Append("   R ").Append(exRange).Append("   D ").Append(exDepth).Append("m")
               .Append("   ").Append(exRate.ToString("0.0")).Append("kg/s")
               .AppendLine();

            // Scan info
            int hits = s.LastScanResults != null ? s.LastScanResults.Count : 0;
            _sb.Append("Last Scan Hits: ").Append(hits).AppendLine();

            // Backpack + Credits
            if (s.Backpack != null)
            {
                _sb.Append("Backpack: ")
                   .Append(s.Backpack.CurrentKg.ToString("0.0"))
                   .Append('/')
                   .Append(s.Backpack.CapacityKg.ToString("0.0"))
                   .Append(" kg")
                   .AppendLine();
            }

            _sb.Append("Credits: ").Append(s.Credits).AppendLine();

            // Cooldowns / channels (text-only MVP)
            if (s.ScanCooldownMaxSeconds > 0.0001)
            {
                if (s.ScanCooldownRemainingSeconds > 0.0)
                    _sb.Append("Scan CD: ").Append(s.ScanCooldownRemainingSeconds.ToString("0.0")).Append("s").AppendLine();
                else
                    _sb.Append("Scan READY").AppendLine();
            }

            if (s.ClaimInProgress)
            {
                _sb.Append("Claiming: ").Append(s.ClaimChannelRemainingSeconds.ToString("0.0")).Append("s").AppendLine();
            }

            if (s.ExtractInProgress)
            {
                // Rich extraction status
                var d = s.Deposits.TryGetDepositById(s.ExtractTargetDepositId);

                _sb.Append("EXTRACTING (E to stop)").AppendLine();

                if (d != null)
                {
                    // Effective rate accounts for ore difficulty (matches core extraction math)
                    double diff = OreCatalog.ExtractionDifficulty(d.OreTypeId);
                    if (diff < 1e-6) diff = 1.0;

                    double effRate = s.Player.ExtractorRateKgPerSec / diff;

                    _sb.Append("  Ore: ").Append(d.OreTypeId)
                       .Append("   Depth: ").Append(d.DepthMeters).Append("m")
                       .AppendLine();

                    _sb.Append("  Remaining: ").Append(d.RemainingUnits).Append(" units")
                       .AppendLine();

                    _sb.Append("  Rate: ").Append(effRate.ToString("0.00")).Append(" kg/s")
                       .Append("   Carry: ").Append(s.ExtractKgRemainder.ToString("0.00")).Append(" kg")
                       .AppendLine();

                    if (s.Backpack != null && s.Backpack.CapacityKg > 1e-9)
                    {
                        double pct = (s.Backpack.CurrentKg / s.Backpack.CapacityKg) * 100.0;
                        _sb.Append("  Backpack: ")
                           .Append(pct.ToString("0")).Append("%")
                           .AppendLine();
                    }
                }
                else
                {
                    _sb.Append("  (target deposit not found)").AppendLine();
                }
            }

            // Prompts
            if (s.CanClaimNow && !s.ClaimInProgress)
                _sb.Append("C to Claim").AppendLine();

            if (s.CanExtractNow && !s.ExtractInProgress)
            {
                _sb.Append("E to Extract").AppendLine();

                // Show candidate details to reduce ambiguity
                var cand = s.Deposits.TryGetDepositById(s.ExtractCandidateDepositId);
                if (cand != null)
                {
                    _sb.Append("  Target: ").Append(cand.OreTypeId)
                       .Append("   Depth ").Append(cand.DepthMeters).Append("m")
                       .Append("   Remaining ").Append(cand.RemainingUnits).Append("u")
                       .AppendLine();
                }
            }

            if (s.IsInTownZone)
            {
                _sb.Append("T to Deposit Ore").AppendLine();
                _sb.Append("Y to Sell Ore").AppendLine();
                _sb.Append("Upgrades: 1-5 Detector | Shift+1-5 Extractor | Ctrl+1-5 Backpack").AppendLine();
            }

            // Last action (debug)
            if (s.LastActionFlashSeconds > 0 && !string.IsNullOrEmpty(s.LastActionText))
            {
                _sb.Append("Last: ").Append(s.LastActionText).AppendLine();
            }
        }

        private void UpdateBars(MineIt.Simulation.GameSession s)
        {
            // ---- Claim progress ----
            if (ClaimBarRoot != null && ClaimBarFill != null)
            {
                bool active = s.ClaimInProgress;
                if (ClaimBarRoot.activeSelf != active)
                    ClaimBarRoot.SetActive(active);

                if (active)
                {
                    // progress = 1 - remaining/total
                    double total = MineIt.Simulation.GameSession.ClaimChannelTotalSeconds;
                    double rem = s.ClaimChannelRemainingSeconds;

                    float p = 0f;
                    if (total > 1e-6)
                        p = Mathf.Clamp01((float)(1.0 - (rem / total)));

                    ClaimBarFill.fillAmount = p;
                }
                else
                {
                    ClaimBarFill.fillAmount = 0f;
                }
            }

            // ---- Scan cooldown progress ----
            if (ScanBarRoot != null && ScanBarFill != null)
            {
                // Show bar when cooling down; hide when ready.
                bool cooling = s.ScanCooldownRemainingSeconds > 0.0 && s.ScanCooldownMaxSeconds > 1e-6;
                if (ScanBarRoot.activeSelf != cooling)
                    ScanBarRoot.SetActive(cooling);

                if (cooling)
                {
                    // progress = 1 - remaining/max
                    double p01 = 1.0 - (s.ScanCooldownRemainingSeconds / s.ScanCooldownMaxSeconds);
                    ScanBarFill.fillAmount = Mathf.Clamp01((float)p01);
                }
                else
                {
                    ScanBarFill.fillAmount = 1f; // visually "ready" if you ever show it
                }
            }

            // ---- Backpack fill (extraction feedback) ----
            if (BackpackBarRoot != null && BackpackBarFill != null && s.Backpack != null)
            {
                bool active = ShowBackpackBarOnlyWhileExtracting ? s.ExtractInProgress : true;

                if (BackpackBarRoot.activeSelf != active)
                    BackpackBarRoot.SetActive(active);

                if (active)
                {
                    double cap = s.Backpack.CapacityKg;
                    double cur = s.Backpack.CurrentKg;

                    float p = 0f;
                    if (cap > 1e-9) p = Mathf.Clamp01((float)(cur / cap));

                    BackpackBarFill.fillAmount = p;
                }
                else
                {
                    BackpackBarFill.fillAmount = 0f;
                }
            }

        }


    }
}
