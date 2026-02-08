using System.Text;
using TMPro;
using UnityEngine;

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
                _sb.Append("Extracting... (E to stop)").AppendLine();
            }

            // Prompts
            if (s.CanClaimNow && !s.ClaimInProgress)
                _sb.Append("C to Claim").AppendLine();

            if (s.CanExtractNow && !s.ExtractInProgress)
                _sb.Append("E to Extract").AppendLine();

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
    }
}
