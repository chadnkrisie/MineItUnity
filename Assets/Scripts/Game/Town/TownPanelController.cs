using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineItUnity.Game.Town
{
    /// <summary>
    /// Minimal town panel: Deposit, Sell, Buy next tiers.
    /// Opens only while in town zone.
    /// Can auto-build its UI at runtime if references aren't wired.
    /// </summary>
    public sealed class TownPanelController : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Header("UI (optional - auto-built if null)")]
        public CanvasGroup RootGroup;
        public TextMeshProUGUI BodyText;

        public Button DepositButton;
        public Button SellButton;

        public Button BuyDetectorButton;
        public Button BuyExtractorButton;
        public Button BuyBackpackButton;

        public Button CloseButton;

        [Header("Input")]
        public KeyCode ToggleKey = KeyCode.B;

        [Header("Update Rate")]
        public float UpdatesPerSecond = 6f;

        private bool _visible;
        private float _nextUpdateTime;
        private readonly StringBuilder _sb = new StringBuilder(1024);

        private void Awake()
        {
            if (Controller == null)
                Controller = FindObjectOfType<GameController>();

            EnsureUi();
            WireButtons();

            SetVisible(false);
        }

        private void Update()
        {
            if (Controller == null || Controller.Session == null)
                return;

            // Force closed outside town
            if (!Controller.Session.IsInTownZone && _visible)
                SetVisible(false);

            // Toggle only in town
            if (Input.GetKeyDown(ToggleKey) && Controller.Session.IsInTownZone)
                SetVisible(!_visible);

            if (!_visible) return;

            if (Time.unscaledTime < _nextUpdateTime) return;
            _nextUpdateTime = Time.unscaledTime + (1f / Mathf.Max(1f, UpdatesPerSecond));

            RefreshTextAndButtons();
        }

        private void RefreshTextAndButtons()
        {
            var s = Controller.Session;

            _sb.Clear();
            _sb.Append("TOWN\n");
            _sb.Append("Credits: ").Append(s.Credits).AppendLine();

            // Backpack summary
            _sb.Append("Backpack: ")
               .Append(s.Backpack.CurrentKg.ToString("0.0"))
               .Append("/")
               .Append(s.Backpack.CapacityKg.ToString("0.0"))
               .Append(" kg")
               .AppendLine();

            // Town storage summary (top few stacks)
            _sb.Append("Storage:\n");
            int shown = 0;
            foreach (var kv in s.TownStorage.OreUnits)
            {
                if (kv.Value <= 0) continue;
                _sb.Append("  ").Append(kv.Key).Append(": ").Append(kv.Value).AppendLine();
                shown++;
                if (shown >= 6) { _sb.Append("  ...\n"); break; }
            }
            if (shown == 0) _sb.Append("  (empty)\n");

            // Artifacts in Vault (fixed order + completion counter)
            string[] ordered = {"stellar_shard", "ancient_lattice", "void_compass", "quantum_fossil", "machine_relic", "echo_prism"};

            int have = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                if (s.TownStorage.HasArtifact(ordered[i])) have++;
            }

            _sb.Append("Artifacts (Vault): ")
               .Append(have).Append('/').Append(ordered.Length)
               .AppendLine();

            for (int i = 0; i < ordered.Length; i++)
            {
                string id = ordered[i];
                bool ok = s.TownStorage.HasArtifact(id);

                _sb.Append("  ")
                   .Append(ok ? "[X] " : "[ ] ")
                   .Append(PrettyArtifactName(id))
                   .AppendLine();
            }

            if (s.HasWon)
            {
                _sb.AppendLine();
                _sb.AppendLine("Directive: COMPLETE");
            }
            else if (s.VaultAuthInProgress)
            {
                _sb.AppendLine();
                _sb.Append("Vault Authentication: ")
                   .Append(Mathf.CeilToInt((float)s.VaultAuthRemainingSeconds))
                   .AppendLine("s");
            }


            // Upgrades (next tier)
            int detNext = Mathf.Clamp(s.Player.DetectorTier + 1, 1, 5);
            int extNext = Mathf.Clamp(s.Player.ExtractorTier + 1, 1, 5);
            int bagNext = Mathf.Clamp(s.BackpackTier + 1, 1, 5);

            int detCost = MineIt.Inventory.UpgradeCatalog.DetectorPriceForTier(detNext);
            int extCost = MineIt.Inventory.UpgradeCatalog.ExtractorPriceForTier(extNext);
            int bagCost = MineIt.Inventory.UpgradeCatalog.BackpackPriceForTier(bagNext);

            _sb.AppendLine();
            _sb.Append("Upgrades (next tier):\n");
            _sb.Append("  Detector T").Append(s.Player.DetectorTier).Append(" → T").Append(detNext)
               .Append("  (").Append(detCost).Append(" cr)\n");
            _sb.Append("  Extractor T").Append(s.Player.ExtractorTier).Append(" → T").Append(extNext)
               .Append("  (").Append(extCost).Append(" cr)\n");
            _sb.Append("  Backpack T").Append(s.BackpackTier).Append(" → T").Append(bagNext)
               .Append("  (").Append(bagCost).Append(" cr)\n");

            if (BodyText != null)
                BodyText.text = _sb.ToString();

            // Buttons enable/labels
            if (DepositButton != null) DepositButton.interactable = true;
            if (SellButton != null) SellButton.interactable = true;

            if (BuyDetectorButton != null) BuyDetectorButton.interactable = (s.Player.DetectorTier < 5 && s.Credits >= detCost);
            if (BuyExtractorButton != null) BuyExtractorButton.interactable = (s.Player.ExtractorTier < 5 && s.Credits >= extCost);
            if (BuyBackpackButton != null) BuyBackpackButton.interactable = (s.BackpackTier < 5 && s.Credits >= bagCost);
        }

        private static string PrettyArtifactName(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "(unknown)";
            // "stellar_shard" -> "Stellar Shard"
            string s = id.Replace('_', ' ').Trim();
            if (s.Length == 0) return "(unknown)";

            var parts = s.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i];
                if (p.Length == 0) continue;
                parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : "");
            }

            return string.Join(" ", parts);
        }

        private void WireButtons()
        {
            if (DepositButton != null)
                DepositButton.onClick.AddListener(() =>
                {
                    if (Controller?.Session == null) return;
                    Controller.Session.TryDepositBackpackToTown(out _);
                    RefreshTextAndButtons();
                });

            if (SellButton != null)
                SellButton.onClick.AddListener(() =>
                {
                    if (Controller?.Session == null) return;
                    Controller.Session.TrySellTownOre(out _, out _);
                    RefreshTextAndButtons();
                });

            if (BuyDetectorButton != null)
                BuyDetectorButton.onClick.AddListener(() =>
                {
                    if (Controller?.Session == null) return;
                    int next = Mathf.Clamp(Controller.Session.Player.DetectorTier + 1, 1, 5);
                    Controller.Session.TryBuyDetectorTier(next);
                    RefreshTextAndButtons();
                });

            if (BuyExtractorButton != null)
                BuyExtractorButton.onClick.AddListener(() =>
                {
                    if (Controller?.Session == null) return;
                    int next = Mathf.Clamp(Controller.Session.Player.ExtractorTier + 1, 1, 5);
                    Controller.Session.TryBuyExtractorTier(next);
                    RefreshTextAndButtons();
                });

            if (BuyBackpackButton != null)
                BuyBackpackButton.onClick.AddListener(() =>
                {
                    if (Controller?.Session == null) return;
                    int next = Mathf.Clamp(Controller.Session.BackpackTier + 1, 1, 5);
                    Controller.Session.TryBuyBackpackTier(next);
                    RefreshTextAndButtons();
                });

            if (CloseButton != null)
                CloseButton.onClick.AddListener(() => SetVisible(false));
        }

        private void SetVisible(bool v)
        {
            _visible = v;

            if (RootGroup != null)
            {
                RootGroup.alpha = v ? 1f : 0f;
                RootGroup.interactable = v;
                RootGroup.blocksRaycasts = v;
            }

            _nextUpdateTime = 0f;
        }

        private void EnsureUi()
        {
            // If everything is wired, do nothing.
            if (RootGroup != null && BodyText != null &&
                DepositButton != null && SellButton != null &&
                BuyDetectorButton != null && BuyExtractorButton != null && BuyBackpackButton != null &&
                CloseButton != null)
                return;

            // Find a canvas to parent under (your scene has HUDCanvas)
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("TownPanelController: No Canvas found. Create a Canvas in the scene.");
                return;
            }

            // Root panel
            var rootGO = new GameObject("TownPanel");
            rootGO.transform.SetParent(canvas.transform, worldPositionStays: false);

            var bg = rootGO.AddComponent<Image>();

            // Solid dark background for clarity
            bg.color = new Color(0.08f, 0.10f, 0.08f, 1.0f);

            // Subtle border to separate panel from world
            var outline = rootGO.AddComponent<Outline>();
            outline.effectColor = new Color(5f, 5f, 5f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            RootGroup = rootGO.AddComponent<CanvasGroup>();

            var rt = rootGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.15f, 0.15f);
            rt.anchorMax = new Vector2(0.85f, 0.85f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // Body text
            var textGO = new GameObject("TownBodyText");
            textGO.transform.SetParent(rootGO.transform, worldPositionStays: false);

            BodyText = textGO.AddComponent<TextMeshProUGUI>();
            BodyText.fontSize = 24;
            BodyText.alignment = TextAlignmentOptions.TopLeft;
            BodyText.enableWordWrapping = false;

            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.05f, 0.20f);
            trt.anchorMax = new Vector2(0.95f, 0.95f);
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            // Buttons row
            var buttonsGO = new GameObject("TownButtons");
            buttonsGO.transform.SetParent(rootGO.transform, worldPositionStays: false);

            var brt = buttonsGO.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.05f, 0.05f);
            brt.anchorMax = new Vector2(0.95f, 0.16f);
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            var h = buttonsGO.AddComponent<HorizontalLayoutGroup>();
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = true;
            h.spacing = 10;

            DepositButton = CreateButton(buttonsGO.transform, "Deposit");
            SellButton = CreateButton(buttonsGO.transform, "Sell");

            BuyDetectorButton = CreateButton(buttonsGO.transform, "Buy Detector");
            BuyExtractorButton = CreateButton(buttonsGO.transform, "Buy Extractor");
            BuyBackpackButton = CreateButton(buttonsGO.transform, "Buy Backpack");

            CloseButton = CreateButton(buttonsGO.transform, "Close");
        }

        private static Button CreateButton(Transform parent, string label)
        {
            var go = new GameObject(label.Replace(" ", "") + "Button");
            go.transform.SetParent(parent, worldPositionStays: false);

            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.15f);

            var btn = go.AddComponent<Button>();

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 0);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, worldPositionStays: false);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 22;

            var trt = textGO.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            return btn;
        }
    }
}
