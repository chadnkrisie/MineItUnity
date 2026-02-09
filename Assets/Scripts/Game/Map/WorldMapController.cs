using MineIt.Mining;
using MineIt.Simulation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MineItUnity.Game.Map
{
    /// <summary>
    /// Full-screen fog-aware world map overlay (M toggles).
    /// Unity-only presentation: reads GameSession state, does not mutate core.
    /// </summary>
    public sealed class WorldMapController : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Tooltip("Root panel GameObject to enable/disable when map is shown.")]
        public GameObject MapRoot;

        [Tooltip("RawImage that displays the generated map texture.")]
        public RawImage MapImage;

        [Tooltip("Waypoint state shared with compass arrow.")]
        public WaypointManager Waypoints;

        [Tooltip("Optional legend text (TextMeshProUGUI) displayed while map is open.")]
        public TextMeshProUGUI LegendText;

        [Tooltip("Optional: if present, we hide/show via CanvasGroup to keep layout stable in edit mode.")]
        public CanvasGroup MapCanvasGroup;

        [Header("Input")]
        public KeyCode ToggleKey = KeyCode.M;

        [Header("Update Rate")]
        [Tooltip("Map refreshes at this rate (Hz) while visible.")]
        public float UpdatesPerSecond = 10f;

        [Header("Colors")]
        public Color32 Undiscovered = new Color32(0, 0, 0, 255);
        public Color32 Discovered = new Color32(40, 40, 40, 255);
        public Color32 VisibleNow = new Color32(90, 90, 90, 255);

        public Color32 PlayerColor = new Color32(255, 255, 255, 255);

        public Color32 DepositUnclaimed = new Color32(50, 255, 255, 255);   // cyan
        public Color32 DepositPlayer = new Color32(50, 255, 50, 255);       // green
        public Color32 DepositNpc = new Color32(255, 90, 30, 255);          // orange/red

        [Header("Waypoint")]
        public bool AllowWaypoint = true;
        public Color32 WaypointColor = new Color32(80, 170, 255, 255); // blue
        public int WaypointDotRadius = 3;

        [Header("Deposit Click Waypoint")]
        public bool DepositClickSetsWaypoint = true;

        [Tooltip("How close (in tiles) a click must be to a deposit center to count as clicking it.")]
        public int DepositClickRadiusTiles = 5;


        [Header("Town")]
        public bool ShowTownMarker = true;
        public Color32 TownColor = new Color32(255, 220, 60, 255); // yellow
        public int TownDotRadius = 3;

        [Header("Fast Travel")]
        public bool AllowFastTravelToTown = true;

        [Tooltip("Optional hotkey to fast travel to town while map is open.")]
        public KeyCode FastTravelKey = KeyCode.F;

        [Tooltip("How close (in tiles) a click must be to the town marker to count as clicking town.")]
        public int TownClickRadiusTiles = 6;

        [Tooltip("Credits required to fast travel to town.")]
        public int FastTravelCostCredits = 10;

        [Header("Marker Sizes")]
        [Tooltip("Radius in pixels for the player dot.")]
        public int PlayerDotRadius = 2;

        [Header("Depletion Warning")]
        public double DepletionWarningSeconds = 120.0; // 2 minutes

        [Tooltip("Radius in pixels for deposit dots.")]
        public int DepositDotRadius = 2;

        private Texture2D _tex;
        private Color32[] _pixels;
        private float _nextUpdateTime;

        private bool _visible;

        private void Awake()
        {
            if (UpdatesPerSecond < 1f) UpdatesPerSecond = 1f;

            SetVisible(false);
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(ToggleKey))
            {
                SetVisible(!_visible);
            }

            if (!_visible) return;

            if (Controller == null || Controller.Session == null) return;
            if (MapRoot == null || MapImage == null) return;

            // --- Fast travel hotkey (map open) ---
            if (AllowFastTravelToTown && UnityEngine.Input.GetKeyDown(FastTravelKey))
            {
                FastTravelToTown();
                return; // prevents other click logic from running this frame
            }

            // --- Fast travel click consumes left-click when clicking town marker ---
            if (AllowFastTravelToTown && UnityEngine.Input.GetMouseButtonDown(0))
            {
                if (TryGetMapTileUnderMouse(out int tx, out int ty))
                {
                    int dx = tx - Controller.Session.TownCenterTx;
                    int dy = ty - Controller.Session.TownCenterTy;

                    if (dx * dx + dy * dy <= TownClickRadiusTiles * TownClickRadiusTiles)
                    {
                        // Do NOT set waypoint when clicking town.
                        FastTravelToTown();
                        return;
                    }
                }
            }

            // --- Waypoint click handling ---
            if (AllowWaypoint && Waypoints != null)
            {
                // Left click = set waypoint, Right click = clear waypoint
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    if (TryGetMapTileUnderMouse(out int tx, out int ty))
                    {
                        // Prefer deposit click waypoint to deposit center
                        if (DepositClickSetsWaypoint && TryFindNearestDepositUnderClick(Controller.Session, tx, ty, out int dTx, out int dTy))
                        {
                            Waypoints.SetWaypoint(dTx, dTy);
                        }
                        else
                        {
                            Waypoints.SetWaypoint(tx, ty);
                        }
                    }
                }
                else if (UnityEngine.Input.GetMouseButtonDown(1))
                {
                    Waypoints.ClearWaypoint();
                }
            }


            if (Time.unscaledTime < _nextUpdateTime) return;
            _nextUpdateTime = Time.unscaledTime + (1f / UpdatesPerSecond);

            EnsureTexture(Controller.Session);
            RenderMap(Controller.Session);
        }

        private void SetVisible(bool v)
        {
            _visible = v;

            if (Waypoints != null)
                Waypoints.IsMapOpen = v;

            if (MapCanvasGroup != null)
            {
                MapCanvasGroup.alpha = v ? 1f : 0f;
                MapCanvasGroup.interactable = v;
                MapCanvasGroup.blocksRaycasts = v;
            }
            else if (MapRoot != null)
            {
                // Fallback if CanvasGroup not wired
                MapRoot.SetActive(v);
            }

            // When opening, force immediate refresh
            _nextUpdateTime = 0f;

            if (v && LegendText != null)
            {
                LegendText.text = BuildLegendString();
            }
        }

        private void EnsureTexture(GameSession s)
        {
            int w = s.Fog.Width;
            int h = s.Fog.Height;

            if (_tex != null && _tex.width == w && _tex.height == h && _pixels != null && _pixels.Length == w * h)
                return;

            _tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.filterMode = FilterMode.Point;

            _pixels = new Color32[w * h];

            MapImage.texture = _tex;

            // NOTE: RawImage may not support preserveAspect in this Unity/UI version.
            // Use RectTransform + AspectRatioFitter on the MapImage object if you want aspect preservation.
        }

        private void FastTravelToTown()
        {
            if (Controller == null || Controller.Session == null) return;

            var s = Controller.Session;

            if (!AllowFastTravelToTown)
                return;

            int cost = Mathf.Max(0, FastTravelCostCredits);

            if (cost > 0 && !s.TrySpendCredits(cost))
            {
                s.PostStatus($"FAST TRAVEL FAILED: need {cost} cr", 2.0);
                return; // keep map open
            }

            // Teleport player to town center
            s.Player.PositionX = s.TownCenterTx;
            s.Player.PositionY = s.TownCenterTy;

            // Ensure chunks around town are loaded for immediate visuals
            s.Chunks.EnsureActiveRadius(s.Player.PositionX, s.Player.PositionY, s.ActiveChunkRadius);

            s.PostStatus(cost > 0 ? $"FAST TRAVEL: TOWN (-{cost} cr)" : "FAST TRAVEL: TOWN", 1.5);

            // Close map after fast travel success
            SetVisible(false);
        }


        private bool TryGetMapTileUnderMouse(out int tx, out int ty)
        {
            tx = ty = 0;
            if (MapImage == null) return false;

            RectTransform rt = MapImage.rectTransform;

            // Screen-space overlay: camera can be null
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, UnityEngine.Input.mousePosition, null, out Vector2 local))
                return false;

            Rect r = rt.rect;

            // local is centered at pivot; convert to 0..1 inside rect
            float nx = (local.x - r.xMin) / r.width;
            float ny = (local.y - r.yMin) / r.height;

            // Must be inside the RawImage rect
            if (nx < 0f || nx > 1f || ny < 0f || ny > 1f) return false;

            // Map texture is world-sized (512x512). Convert normalized to tile coords.
            int w = _tex != null ? _tex.width : (Controller != null && Controller.Session != null ? Controller.Session.Fog.Width : 0);
            int h = _tex != null ? _tex.height : (Controller != null && Controller.Session != null ? Controller.Session.Fog.Height : 0);
            if (w <= 0 || h <= 0) return false;

            tx = Mathf.Clamp(Mathf.FloorToInt(nx * w), 0, w - 1);
            ty = Mathf.Clamp(Mathf.FloorToInt(ny * h), 0, h - 1);

            return true;
        }


        private void RenderMap(GameSession s)
        {
            int w = _tex.width;
            int h = _tex.height;

            // 1) Base layer: fog states
            // We render in texture coords where (0,0) is bottom-left.
            // Our fog queries are (tx,ty) with ty increasing downward in your core semantics,
            // but since you’ve already adapted Y semantics for movement/rendering, we’ll keep
            // the mapping direct: (tx,ty) -> pixel (tx,ty). If you want Y flipped visually,
            // we can do it with one line change.
            for (int ty = 0; ty < h; ty++)
            {
                int row = ty * w;
                for (int tx = 0; tx < w; tx++)
                {
                    Color32 c;

                    if (!s.Fog.IsDiscovered(tx, ty))
                        c = Undiscovered;
                    else if (!s.Fog.IsVisibleNow(tx, ty))
                        c = Discovered;
                    else
                        c = VisibleNow;

                    _pixels[row + tx] = c;
                }
            }

            // 2) Deposit markers (discovered only)
            foreach (var d in s.Deposits.GetAllDeposits())
            {
                if (!d.DiscoveredByPlayer) continue;
                if (d.RemainingUnits <= 0) continue;

                Color32 dc;
                if (d.ClaimedByPlayer) dc = DepositPlayer;
                else if (d.ClaimedByNpcId.HasValue) dc = DepositNpc;
                else dc = DepositUnclaimed;

                Color32 finalColor = dc;

                // If NPC-claimed and nearly depleted darken / redden
                if (d.ClaimedByNpcId.HasValue && d.RemainingUnits > 0)
                {
                    NpcMinerManager.NpcMiner npc = null;

                    var npcList = s.Npcs?.Npcs;
                    if (npcList != null)
                    {
                        for (int i = 0; i < npcList.Count; i++)
                        {
                            if (npcList[i].NpcId == d.ClaimedByNpcId.Value)
                            {
                                npc = npcList[i];
                                break;
                            }
                        }
                    }

                    if (npc != null)
                    {
                        double rate = s.Npcs.GetNpcExtractionRateKgPerSec(npc, d.OreTypeId);
                        double eta = d.EstimateSecondsToDeplete(rate);

                        if (eta <= DepletionWarningSeconds)
                        {
                            finalColor = new Color32(200, 40, 40, 255); // urgent red
                        }
                    }
                }

                DrawDot(w, h, d.CenterTx, d.CenterTy, DepositDotRadius, finalColor);
            }

            // 3) Town marker (always visible on map; tweak rule later if desired)
            if (ShowTownMarker)
            {
                int tTx = Mathf.Clamp(s.TownCenterTx, 0, w - 1);
                int tTy = Mathf.Clamp(s.TownCenterTy, 0, h - 1);

                DrawDot(w, h, tTx, tTy, TownDotRadius, TownColor);
            }

            // 4) Player dot (always)
            int pTx = Mathf.Clamp(Mathf.FloorToInt((float)s.Player.PositionX), 0, w - 1);
            int pTy = Mathf.Clamp(Mathf.FloorToInt((float)s.Player.PositionY), 0, h - 1);
            DrawDot(w, h, pTx, pTy, PlayerDotRadius, PlayerColor);

            // 5) Waypoint marker
            if (Waypoints != null && Waypoints.HasWaypoint)
            {
                DrawDot(w, h, Waypoints.WaypointTx, Waypoints.WaypointTy, WaypointDotRadius, WaypointColor);
            }

            // Apply
            _tex.SetPixels32(_pixels);
            _tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        }

        private void DrawDot(int w, int h, int cx, int cy, int r, Color32 c)
        {
            int r2 = r * r;

            int minX = Mathf.Max(0, cx - r);
            int maxX = Mathf.Min(w - 1, cx + r);
            int minY = Mathf.Max(0, cy - r);
            int maxY = Mathf.Min(h - 1, cy + r);

            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - cy;
                int row = y * w;
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - cx;
                    if (dx * dx + dy * dy > r2) continue;
                    _pixels[row + x] = c;
                }
            }
        }

        private bool TryFindNearestDepositUnderClick(GameSession s, int clickTx, int clickTy, out int depositTx, out int depositTy)
        {
            depositTx = depositTy = 0;
            if (s == null) return false;

            int r = Mathf.Max(0, DepositClickRadiusTiles);
            int r2 = r * r;

            int bestDist2 = int.MaxValue;
            int bestTx = 0, bestTy = 0;
            bool found = false;

            foreach (var d in s.Deposits.GetAllDeposits())
            {
                if (!d.DiscoveredByPlayer) continue;
                if (d.RemainingUnits <= 0) continue;

                int dx = d.CenterTx - clickTx;
                int dy = d.CenterTy - clickTy;
                int dist2 = dx * dx + dy * dy;

                if (dist2 <= r2 && dist2 < bestDist2)
                {
                    bestDist2 = dist2;
                    bestTx = d.CenterTx;
                    bestTy = d.CenterTy;
                    found = true;
                }
            }

            if (!found) return false;

            depositTx = bestTx;
            depositTy = bestTy;
            return true;
        }

        private string BuildLegendString()
        {
            // Text-only legend. Colors are represented by words; UI colors are already visible on the map.
            // Keep it short so it reads at a glance.
            return
                "MAP LEGEND\n" +
                "• White: You\n" +
                "• Yellow: Town\n" +
                "• Cyan: Unclaimed Deposit\n" +
                "• Green: Your Claim\n" +
                "• Orange: NPC Claim\n" +
                "• Gray: Explored\n" +
                "• Black: Unknown\n" +
                "• Blue: Waypoint\n";
        }

    }
}
