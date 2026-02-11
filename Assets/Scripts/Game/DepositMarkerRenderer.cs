using System.Collections.Generic;
using UnityEngine;
using MineIt.Mining;
using MineIt.World;

namespace MineItUnity.Game
{
    /// <summary>
    /// Draws discovered deposit markers using pooled SpriteRenderers.
    /// - Only within camera bounds
    /// - Colors by ownership
    /// - No per-frame Instantiate/Destroy
    /// </summary>
    public sealed class DepositMarkerRenderer : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Header("Tuning")]
        public int SortingOrder = 30;              // above fog, below night overlay
        public float MarkerScale = 0.35f;          // relative to 1 tile = 1 unit
        public int CameraPaddingTiles = 2;
        public int InitialPoolSize = 256;

        // Colors (match your spec)
        public Color UnclaimedColor = new Color(0.2f, 1.0f, 1.0f, 1.0f);    // cyan
        public Color PlayerClaimedColor = new Color(0.2f, 1.0f, 0.2f, 1.0f); // green
        public Color NpcClaimedColor = new Color(1.0f, 0.35f, 0.1f, 1.0f);   // orange/red
        public Color DepletedColor = new Color(0.55f, 0.55f, 0.55f, 1.0f);   // gray

        private Camera _cam;

        // Pooling
        private readonly Stack<SpriteRenderer> _pool = new Stack<SpriteRenderer>();
        private readonly Dictionary<int, SpriteRenderer> _activeByDepositId = new Dictionary<int, SpriteRenderer>();
        private readonly HashSet<int> _seenThisFrame = new HashSet<int>();

        private Sprite _circleSprite;

        private void Awake()
        {
            _cam = Camera.main;
            _circleSprite = CreateCircleSprite(16);

            WarmPool(InitialPoolSize);
        }

        private void LateUpdate()
        {
            if (Controller == null || Controller.Session == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || !_cam.orthographic) return;

            RenderMarkers();
        }

        private void RenderMarkers()
        {
            var session = Controller.Session;
            var chunks = session.Chunks;

            // Camera bounds in tile coords (1 Unity unit = 1 tile)
            float halfH = _cam.orthographicSize;
            float halfW = halfH * _cam.aspect;

            Vector3 camPos = _cam.transform.position;

            int minTx = Mathf.FloorToInt(camPos.x - halfW) - CameraPaddingTiles;
            int maxTx = Mathf.CeilToInt(camPos.x + halfW) + CameraPaddingTiles;
            int minTy = Mathf.FloorToInt(camPos.y - halfH) - CameraPaddingTiles;
            int maxTy = Mathf.CeilToInt(camPos.y + halfH) + CameraPaddingTiles;

            minTx = Mathf.Clamp(minTx, 0, chunks.WorldWidthTiles - 1);
            maxTx = Mathf.Clamp(maxTx, 0, chunks.WorldWidthTiles - 1);
            minTy = Mathf.Clamp(minTy, 0, chunks.WorldHeightTiles - 1);
            maxTy = Mathf.Clamp(maxTy, 0, chunks.WorldHeightTiles - 1);

            _seenThisFrame.Clear();

            // Iterate loaded chunks only
            foreach (var ch in chunks.GetLoadedChunks())
            {
                // Quick reject: if chunk AABB doesn't intersect camera bounds, skip
                int baseTx = ch.Coord.Cx * Chunk.CHUNK_SIZE_TILES;
                int baseTy = ch.Coord.Cy * Chunk.CHUNK_SIZE_TILES;
                int endTx = baseTx + Chunk.CHUNK_SIZE_TILES - 1;
                int endTy = baseTy + Chunk.CHUNK_SIZE_TILES - 1;

                if (endTx < minTx || baseTx > maxTx || endTy < minTy || baseTy > maxTy)
                    continue;

                // Chunk deposits list contains the authoritative deposit objects
                foreach (var d in ch.Deposits)
                {
                    if (!d.DiscoveredByPlayer) continue;

                    // For trust/history: show depleted as gray markers instead of hiding them.
                    bool depleted = d.IsDepleted || (d.RemainingUnits <= 0);

                    int tx = d.CenterTx;
                    int ty = d.CenterTy;

                    if (tx < minTx || tx > maxTx || ty < minTy || ty > maxTy)
                        continue;

                    _seenThisFrame.Add(d.DepositId);

                    var sr = GetOrCreateMarker(d.DepositId);

                    // Center marker in the tile
                    sr.transform.position = new Vector3(tx + 0.5f, ty + 0.5f, 0f);
                    sr.transform.localScale = new Vector3(MarkerScale, MarkerScale, 1f);

                    // Ownership / archival color
                    if (depleted) sr.color = DepletedColor;
                    else if (d.ClaimedByPlayer) sr.color = PlayerClaimedColor;
                    else if (d.ClaimedByNpcId.HasValue) sr.color = NpcClaimedColor;
                    else sr.color = UnclaimedColor;
                }
            }

            // Deactivate any markers not seen this frame
            // (Use a temp list to avoid modifying dictionary during enumeration)
            if (_activeByDepositId.Count > 0)
            {
                _toRemove.Clear();
                foreach (var kv in _activeByDepositId)
                {
                    if (!_seenThisFrame.Contains(kv.Key))
                        _toRemove.Add(kv.Key);
                }

                for (int i = 0; i < _toRemove.Count; i++)
                {
                    int id = _toRemove[i];
                    ReturnToPool(id);
                }
            }
        }

        // Reused list to avoid allocations
        private readonly List<int> _toRemove = new List<int>(256);

        private SpriteRenderer GetOrCreateMarker(int depositId)
        {
            if (_activeByDepositId.TryGetValue(depositId, out var sr))
                return sr;

            sr = (_pool.Count > 0) ? _pool.Pop() : CreateMarkerRenderer();
            sr.gameObject.SetActive(true);
            _activeByDepositId[depositId] = sr;
            return sr;
        }

        private void ReturnToPool(int depositId)
        {
            if (!_activeByDepositId.TryGetValue(depositId, out var sr))
                return;

            _activeByDepositId.Remove(depositId);
            sr.gameObject.SetActive(false);
            _pool.Push(sr);
        }

        private void WarmPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var sr = CreateMarkerRenderer();
                sr.gameObject.SetActive(false);
                _pool.Push(sr);
            }
        }

        private SpriteRenderer CreateMarkerRenderer()
        {
            var go = new GameObject("DepositMarker");
            go.transform.SetParent(transform, worldPositionStays: false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _circleSprite;
            sr.sortingOrder = SortingOrder;
            sr.color = UnclaimedColor;

            return sr;
        }

        private static Sprite CreateCircleSprite(int sizePx)
        {
            var tex = new Texture2D(sizePx, sizePx, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Point;

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

                    // Solid circle, transparent outside
                    float a = d2 <= r2 ? 1f : 0f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, sizePx, sizePx), new Vector2(0.5f, 0.5f), 1);
        }
    }
}
