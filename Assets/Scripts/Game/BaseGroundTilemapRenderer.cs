using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using MineIt.World;

namespace MineItUnity.Game
{
    /// <summary>
    /// Paints a "base ground" layer (grass) beneath the world tiles.
    /// This eliminates repetition artifacts by ensuring the world layer only draws
    /// local augmentations (e.g., rocks), while grass is always present.
    ///
    /// Presentation-only. Deterministic: uses the core world/chunk streaming.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public sealed class BaseGroundTilemapRenderer : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Header("Grass Tile (Tile asset or runtime-built from sprite)")]
        public TileBase GrassTile;

        [Header("Grass Sprite (optional)")]
        [Tooltip("If GrassTile is not assigned, we will create a runtime Tile from this sprite.")]
        public Sprite GrassSprite;

        // Runtime-built tile (created only if needed)
        private Tile _runtimeGrassTile;

        // Track which chunks have already been painted
        private readonly HashSet<ChunkCoord> _paintedChunks = new HashSet<ChunkCoord>();

        private Tilemap _tilemap;

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
            EnsureGrassTile();
            ForceRepaintAll();
        }

        private void LateUpdate()
        {
            if (Controller == null || Controller.Session == null)
                return;

            EnsureGrassTile();
            PaintVisibleChunks();
        }

        public void ForceRepaintAll()
        {
            _paintedChunks.Clear();

            if (_tilemap != null)
                _tilemap.ClearAllTiles();
        }

        private void EnsureGrassTile()
        {
            if (GrassTile != null) return;

            if (GrassSprite != null)
            {
                _runtimeGrassTile = ScriptableObject.CreateInstance<Tile>();
                _runtimeGrassTile.sprite = GrassSprite;
                _runtimeGrassTile.colliderType = Tile.ColliderType.None;
                GrassTile = _runtimeGrassTile;
            }
        }

        private void PaintVisibleChunks()
        {
            if (GrassTile == null) return;

            var session = Controller.Session;
            var chunks = session.Chunks;

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return;

            Vector3 camPos = cam.transform.position;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // Determine visible tile bounds (+padding)
            int minTx = Mathf.FloorToInt(camPos.x - halfWidth) - 2;
            int maxTx = Mathf.CeilToInt(camPos.x + halfWidth) + 2;
            int minTy = Mathf.FloorToInt(camPos.y - halfHeight) - 2;
            int maxTy = Mathf.CeilToInt(camPos.y + halfHeight) + 2;

            minTx = Mathf.Clamp(minTx, 0, chunks.WorldWidthTiles - 1);
            maxTx = Mathf.Clamp(maxTx, 0, chunks.WorldWidthTiles - 1);
            minTy = Mathf.Clamp(minTy, 0, chunks.WorldHeightTiles - 1);
            maxTy = Mathf.Clamp(maxTy, 0, chunks.WorldHeightTiles - 1);

            int chunkSize = Chunk.CHUNK_SIZE_TILES;

            int minCx = minTx / chunkSize;
            int maxCx = maxTx / chunkSize;
            int minCy = minTy / chunkSize;
            int maxCy = maxTy / chunkSize;

            for (int cy = minCy; cy <= maxCy; cy++)
                for (int cx = minCx; cx <= maxCx; cx++)
                {
                    var coord = new ChunkCoord(cx, cy);
                    if (_paintedChunks.Contains(coord))
                        continue;

                    Chunk chunk;
                    try
                    {
                        chunk = chunks.GetOrLoadChunk(cx, cy);
                    }
                    catch
                    {
                        continue;
                    }

                    PaintChunkAsGrass(chunk);
                    _paintedChunks.Add(coord);
                }
        }

        private void PaintChunkAsGrass(Chunk chunk)
        {
            int chunkSize = Chunk.CHUNK_SIZE_TILES;
            int baseTx = chunk.Coord.Cx * chunkSize;
            int baseTy = chunk.Coord.Cy * chunkSize;

            for (int ly = 0; ly < chunkSize; ly++)
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    Vector3Int cell = new Vector3Int(baseTx + lx, baseTy + ly, 0);
                    _tilemap.SetTile(cell, GrassTile);
                }
        }
    }
}
