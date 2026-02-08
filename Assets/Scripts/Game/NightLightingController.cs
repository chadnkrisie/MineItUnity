using UnityEngine;
using MineIt.Simulation;

namespace MineItUnity.Game
{
    /// <summary>
    /// Night lighting using SpriteMask:
    /// - A fullscreen black overlay whose alpha follows Clock.Darkness01
    /// - A circular SpriteMask centered on player, so overlay is visible outside the mask
    /// </summary>
    public sealed class NightLightingController : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Header("Tuning")]
        [Range(0f, 1f)]
        public float MaxDarkness = 0.65f;

        // Light radius in tiles (Unity units), matches your MVP night vision until you add a flashlight system.
        public float NightLightRadiusTiles = 6f;

        // How many screen tiles beyond camera bounds to ensure overlay fully covers
        public float OverlayPaddingTiles = 2f;

        [Header("Sorting")]
        public int OverlaySortingOrder = 100; // above world/fog, below future UI

        private Camera _cam;

        private GameObject _overlayObj;
        private SpriteRenderer _overlaySr;

        private GameObject _maskObj;
        private SpriteMask _mask;

        private Sprite _whiteSprite;       // 1x1 white, for overlay
        private Sprite _circleMaskSprite;  // circle sprite for SpriteMask

        private void Awake()
        {
            _cam = Camera.main;

            _whiteSprite = CreateSolidSprite();
            _circleMaskSprite = CreateCircleMaskSprite(sizePx: 128);

            CreateOverlayObjects();
        }

        private void LateUpdate()
        {
            if (Controller == null || Controller.Session == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || !_cam.orthographic) return;

            var clock = Controller.Session.Clock;

            float darkness01 = (float)clock.Darkness01;
            float overlayAlpha = darkness01 * MaxDarkness;

            // If no darkness, hide overlay entirely
            bool active = overlayAlpha > 0.001f;
            if (_overlayObj.activeSelf != active) _overlayObj.SetActive(active);
            if (_maskObj.activeSelf != active) _maskObj.SetActive(active);

            if (!active) return;

            // 1) Position & scale overlay to cover camera view
            FitOverlayToCamera();

            // 2) Set overlay alpha
            var c = _overlaySr.color;
            c.r = 0f; c.g = 0f; c.b = 0f;
            c.a = overlayAlpha;
            _overlaySr.color = c;

            // 3) Move mask to player and scale to radius
            float px = (float)Controller.Session.Player.PositionX;
            float py = (float)Controller.Session.Player.PositionY;

            _maskObj.transform.position = new Vector3(px, py, 0f);

            // SpriteMask scale: our circle sprite is normalized to 1 unit in size
            float radius = NightLightRadiusTiles;
            float diameter = radius * 2f;
            _maskObj.transform.localScale = new Vector3(diameter, diameter, 1f);
        }

        private void CreateOverlayObjects()
        {
            // Parent for cleanliness
            var root = new GameObject("NightLighting");
            root.transform.SetParent(transform, worldPositionStays: false);

            // --- Darkness overlay ---
            _overlayObj = new GameObject("NightOverlay");
            _overlayObj.transform.SetParent(root.transform, worldPositionStays: false);

            _overlaySr = _overlayObj.AddComponent<SpriteRenderer>();
            _overlaySr.sprite = _whiteSprite;
            _overlaySr.sortingOrder = OverlaySortingOrder;

            // Critical: show darkness OUTSIDE the mask (hole in the middle)
            _overlaySr.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;

            // --- Player light mask ---
            _maskObj = new GameObject("PlayerLightMask");
            _maskObj.transform.SetParent(root.transform, worldPositionStays: false);

            _mask = _maskObj.AddComponent<SpriteMask>();
            _mask.sprite = _circleMaskSprite;

            // Ensure mask affects overlay: default is fine, but keep consistent
            _mask.isCustomRangeActive = true;
            _mask.frontSortingOrder = OverlaySortingOrder + 1;
            _mask.backSortingOrder = OverlaySortingOrder - 1;
        }

        private void FitOverlayToCamera()
        {
            // Overlay should follow camera center
            Vector3 camPos = _cam.transform.position;
            _overlayObj.transform.position = new Vector3(camPos.x, camPos.y, 0f);

            float halfH = _cam.orthographicSize + OverlayPaddingTiles;
            float halfW = halfH * _cam.aspect;

            // Our overlay sprite is 1 unit wide/high; scale it to cover view
            float w = halfW * 2f;
            float h = halfH * 2f;

            _overlayObj.transform.localScale = new Vector3(w, h, 1f);
        }

        private static Sprite CreateSolidSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }

        private static Sprite CreateCircleMaskSprite(int sizePx)
        {
            // White circle on transparent background
            var tex = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float cx = (sizePx - 1) * 0.5f;
            float cy = (sizePx - 1) * 0.5f;
            float r = (sizePx * 0.5f) - 1f;
            float r2 = r * r;

            for (int y = 0; y < sizePx; y++)
                for (int x = 0; x < sizePx; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float d2 = dx * dx + dy * dy;

                    // Inside circle: alpha 1, outside: alpha 0
                    float a = d2 <= r2 ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f), 1);
        }
    }
}
