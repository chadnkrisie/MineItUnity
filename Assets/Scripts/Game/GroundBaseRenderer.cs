using UnityEngine;

namespace MineItUnity.Game
{
    /// <summary>
    /// Renders a continuous repeating ground texture underneath all tilemaps.
    /// This avoids "tile stamping" visuals for the base terrain.
    ///
    /// Presentation-only. Deterministic is not required here because it is purely visual.
    /// The ground texture repeats; variation comes from DecalTilemapRenderer.
    /// </summary>
    public sealed class GroundBaseRenderer : MonoBehaviour
    {
        [Header("Ground Texture")]
        [Tooltip("A small seamless ground texture (e.g., 128x128 or 256x256). Can be your grass sprite texture initially.")]
        public Texture2D GroundTexture;

        [Tooltip("How many world units per one texture repeat. With 1 unit = 1 tile, try 8..32.")]
        public float UnitsPerRepeat = 12f;

        [Header("Coverage")]
        [Tooltip("Extra tiles beyond camera bounds so you never see edges.")]
        public float PaddingTiles = 6f;

        [Header("Sorting")]
        [Tooltip("Sorting order relative to SpriteRenderer/Tilemaps. Lower draws behind.")]
        public int SortingOrder = -100;

        private Camera _cam;
        private GameObject _quad;
        private MeshRenderer _mr;
        private Material _mat;

        private void Awake()
        {
            _cam = Camera.main;
            CreateQuad();
        }

        private void LateUpdate()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || !_cam.orthographic) return;

            if (_mr == null || _mat == null) return;

            FitQuadToCamera();
            UpdateTiling();
        }

        private void CreateQuad()
        {
            _quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _quad.name = "GroundQuad";
            _quad.transform.SetParent(transform, worldPositionStays: false);

            // Remove collider (not needed)
            var col = _quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            _mr = _quad.GetComponent<MeshRenderer>();

            // Use an unlit texture material so lighting doesn't change ground color
            Shader shader = Shader.Find("Unlit/Texture");
            if (shader == null)
            {
                Debug.LogWarning("GroundBaseRenderer: Unlit/Texture shader not found; using Sprites/Default.");
                shader = Shader.Find("Sprites/Default");
            }

            _mat = new Material(shader);
            _mr.sharedMaterial = _mat;

            // Render ordering: use sorting order if available
            _mr.sortingOrder = SortingOrder;
        }

        private void FitQuadToCamera()
        {
            Vector3 camPos = _cam.transform.position;

            float halfH = _cam.orthographicSize + PaddingTiles;
            float halfW = halfH * _cam.aspect;

            // Quad is 1x1 by default; scale to cover camera view
            float w = halfW * 2f;
            float h = halfH * 2f;

            _quad.transform.position = new Vector3(camPos.x, camPos.y, 0f);
            _quad.transform.localScale = new Vector3(w, h, 1f);
        }

        private void UpdateTiling()
        {
            if (GroundTexture == null) return;

            // Ensure the texture repeats
            GroundTexture.wrapMode = TextureWrapMode.Repeat;
            GroundTexture.filterMode = FilterMode.Point; // keep crisp for pixel art
            GroundTexture.mipMapBias = 0f;

            _mat.mainTexture = GroundTexture;

            float w = _quad.transform.localScale.x;
            float h = _quad.transform.localScale.y;

            // How many repeats across the quad
            float unitsPerRepeat = Mathf.Max(0.1f, UnitsPerRepeat);
            float repsX = Mathf.Max(1f, w / unitsPerRepeat);
            float repsY = Mathf.Max(1f, h / unitsPerRepeat);

            _mat.mainTextureScale = new Vector2(repsX, repsY);

            // ===== Critical: anchor the texture to WORLD coordinates =====
            // The quad moves with the camera. Without offset, UVs stay fixed to the quad,
            // making the ground texture appear "camera-locked".
            //
            // Offset by quad world position so the texture appears stationary in the world.
            Vector3 p = _quad.transform.position;

            // Convert world position to UV space: 1 UV unit per texture repeat
            float ox = p.x / unitsPerRepeat;
            float oy = p.y / unitsPerRepeat;

            // Keep offsets bounded (optional but avoids huge numbers over time)
            ox = ox - Mathf.Floor(ox);
            oy = oy - Mathf.Floor(oy);

            _mat.mainTextureOffset = new Vector2(ox, oy);
            // ===========================================================
        }
    }
}
