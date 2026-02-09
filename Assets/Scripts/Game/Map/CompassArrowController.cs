using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MineItUnity.Game.Map
{
    /// <summary>
    /// Displays a simple compass arrow pointing from player toward the waypoint.
    /// UI-only: does not affect simulation.
    /// </summary>
    public sealed class CompassArrowController : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;
        public WaypointManager Waypoints;

        [Header("UI")]
        public RectTransform ArrowRoot;
        public Image ArrowImage;

        [Tooltip("Optional distance text (tiles).")]
        public TextMeshProUGUI DistanceText;

        [Header("Tuning")]
        public float ArrowRadiusPixels = 120f; // distance from screen center
        public float ArrowSizePixels = 36f;    // arrow icon size

        private void Awake()
        {
            if (ArrowImage != null && ArrowImage.sprite == null)
                ArrowImage.sprite = CreateArrowSprite(64, 64);
        }

        private void Update()
        {
            if (Controller == null || Controller.Session == null || Waypoints == null || ArrowRoot == null || ArrowImage == null)
                return;

            bool active = Waypoints.HasWaypoint && !Waypoints.IsMapOpen;

            // Never disable the controller GameObject; just toggle visuals
            if (ArrowImage != null) ArrowImage.enabled = active;
            if (DistanceText != null) DistanceText.enabled = active;

            if (!active)
            {
                if (DistanceText != null) DistanceText.text = "";
                return;
            }

            float px = (float)Controller.Session.Player.PositionX;
            float py = (float)Controller.Session.Player.PositionY;

            float wx = Waypoints.WaypointTx + 0.5f;
            float wy = Waypoints.WaypointTy + 0.5f;

            Vector2 dir = new Vector2(wx - px, wy - py);
            float dist = dir.magnitude;

            if (dist < 0.001f)
            {
                // On top of waypoint
                ArrowRoot.anchoredPosition = Vector2.zero;
                ArrowImage.rectTransform.localRotation = Quaternion.identity;
                if (DistanceText != null) DistanceText.text = "0";
                return;
            }

            dir /= dist;

            // Place arrow around center
            ArrowRoot.anchoredPosition = dir * ArrowRadiusPixels;

            // Rotate arrow to point toward waypoint
            float angleDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            ArrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angleDeg - 90f);

            // Size
            ArrowImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ArrowSizePixels);
            ArrowImage.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, ArrowSizePixels);

            // Distance in tiles
            if (DistanceText != null)
                DistanceText.text = $"{Mathf.RoundToInt(dist)}";
        }

        private static Sprite CreateArrowSprite(int w, int h)
        {
            // Simple triangle arrow (white)
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = new Color(1f, 1f, 1f, 1f);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, clear);

            // Triangle pointing up
            int cx = w / 2;
            int top = h - 4;
            int baseY = 8;

            for (int y = baseY; y <= top; y++)
            {
                float t = (float)(y - baseY) / (top - baseY);
                int half = Mathf.RoundToInt(Mathf.Lerp(w * 0.30f, 0f, t));
                int x0 = cx - half;
                int x1 = cx + half;

                for (int x = x0; x <= x1; x++)
                {
                    if (x >= 0 && x < w)
                        tex.SetPixel(x, y, solid);
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
