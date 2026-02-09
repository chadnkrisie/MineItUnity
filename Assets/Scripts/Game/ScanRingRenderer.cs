using UnityEngine;
using MineItUnity.Game;

namespace MineItUnity.Game
{
    /// <summary>
    /// Renders a temporary scan ring when a scan executes.
    /// Pure presentation: reads GameSession state only.
    /// </summary>
    public sealed class ScanRingRenderer : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Header("Visuals")]
        [Tooltip("Ring color at full intensity")]
        public Color RingColor = new Color(0.2f, 1.0f, 1.0f, 1.0f); // cyan
        public float RingThickness = 0.06f;
        public int SortingOrder = 25;

        private SpriteRenderer _sr;
        private Sprite _ringSprite;

        private void Awake()
        {
            _ringSprite = CreateRingSprite(sizePx: 256, thicknessPx: 6);

            var go = new GameObject("ScanRing");
            go.transform.SetParent(transform, worldPositionStays: false);

            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = _ringSprite;
            _sr.sortingOrder = SortingOrder;
            _sr.enabled = false;
        }

        private void LateUpdate()
        {
            if (Controller == null || Controller.Session == null)
                return;

            var s = Controller.Session;

            if (s.LastScanFlashSeconds <= 0)
            {
                _sr.enabled = false;
                return;
            }

            _sr.enabled = true;

            // Center on scan origin (tile center)
            float cx = s.LastScanCenterTx + 0.5f;
            float cy = s.LastScanCenterTy + 0.5f;
            _sr.transform.position = new Vector3(cx, cy, 0f);

            // Scale ring: diameter = radius * 2
            float radius = s.LastScanRadiusTiles;
            float diameter = radius * 2f;
            _sr.transform.localScale = new Vector3(diameter, diameter, 1f);

            // Fade based on remaining flash time
            float alpha = Mathf.Clamp01((float)(s.LastScanFlashSeconds / 0.35));
            var c = RingColor;
            c.a *= alpha;
            _sr.color = c;
        }

        private static Sprite CreateRingSprite(int sizePx, int thicknessPx)
        {
            var tex = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float cx = (sizePx - 1) * 0.5f;
            float cy = (sizePx - 1) * 0.5f;
            float rOuter = (sizePx * 0.5f) - 1f;
            float rInner = rOuter - thicknessPx;

            float rOuter2 = rOuter * rOuter;
            float rInner2 = rInner * rInner;

            for (int y = 0; y < sizePx; y++)
                for (int x = 0; x < sizePx; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d2 = dx * dx + dy * dy;

                    bool insideRing = (d2 <= rOuter2) && (d2 >= rInner2);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, insideRing ? 1f : 0f));
                }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f), 1);
        }
    }
}
