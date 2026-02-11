using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using MineIt.World;

namespace MineItUnity.Game
{
    /// <summary>
    /// Renders MineIt world chunks into a Unity Tilemap.
    /// One Unity unit == one MineIt tile.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public sealed class WorldTilemapRenderer : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Header("Tiles (Tile assets or runtime-built from sprites)")]
        public TileBase RockTile;

        [Header("Tile Sprites (optional)")]
        [Tooltip("If RockTile is not assigned, we will create runtime tiles from these sprites and pick deterministically per tile.")]
        public Sprite[] RockSprites;

        // Runtime-built rock tiles (created only if needed)
        private TileBase[] _runtimeRockTiles;

        // Track which chunks have already been painted
        private readonly HashSet<ChunkCoord> _paintedChunks = new HashSet<ChunkCoord>();

        private Tilemap _tilemap;

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();

            // If the user didn't assign Tile assets in the inspector, build them from sprites.
            EnsureTiles();
        }

        private void LateUpdate()
        {
            if (Controller == null || Controller.Session == null)
                return;

            PaintVisibleChunks();
        }

        private void PaintVisibleChunks()
        {
            var session = Controller.Session;
            var chunks = session.Chunks;

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic)
                return;

            Vector3 camPos = cam.transform.position;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            // Determine visible tile bounds
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

                    PaintChunk(session, chunk);
                    _paintedChunks.Add(coord);
                }
        }

        private void PaintChunk(MineIt.Simulation.GameSession session, Chunk chunk)
        {
            int chunkSize = Chunk.CHUNK_SIZE_TILES;
            int baseTx = chunk.Coord.Cx * chunkSize;
            int baseTy = chunk.Coord.Cy * chunkSize;

            for (int ly = 0; ly < chunkSize; ly++)
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    byte t = chunk.GetLocalTile(lx, ly);

                    Vector3Int cell = new Vector3Int(
                        baseTx + lx,
                        baseTy + ly,
                        0);

                    TileBase tile = null;

                    if (t != 0)
                    {
                        // Rock cell: choose deterministic rock variant
                        tile = ChooseRockTile(session, baseTx + lx, baseTy + ly);
                    }

                    _tilemap.SetTile(cell, tile);
                }
        }

        private void EnsureTiles()
        {
            // Rock (variants)
            if (RockTile == null)
            {
                // Build variants from RockSprites if provided
                if (_runtimeRockTiles == null || _runtimeRockTiles.Length == 0)
                {
                    if (RockSprites != null && RockSprites.Length > 0)
                    {
                        _runtimeRockTiles = new TileBase[RockSprites.Length];

                        for (int i = 0; i < RockSprites.Length; i++)
                        {
                            var sp = RockSprites[i];
                            if (sp == null) continue;

                            var t = ScriptableObject.CreateInstance<Tile>();
                            t.sprite = sp;
                            t.colliderType = Tile.ColliderType.None; // visuals only; collision can be added later via TilemapCollider2D
                            _runtimeRockTiles[i] = t;
                        }
                    }
                }
            }
            if (RockTile == null && (RockSprites == null || RockSprites.Length == 0))
                Debug.LogWarning("WorldTilemapRenderer: RockTile is null and RockSprites is empty. Rock cells will render blank.");
        }

        private TileBase ChooseRockTile(MineIt.Simulation.GameSession session, int tx, int ty)
        {
            // If an explicit RockTile asset is assigned, use it (no variants)
            if (RockTile != null)
                return RockTile;

            // Otherwise choose among runtime-built variant tiles
            if (_runtimeRockTiles == null || _runtimeRockTiles.Length == 0)
                return null;

            uint h = Hash3((uint)session.Seed, (uint)tx, (uint)ty);
            int idx = (int)(h % (uint)_runtimeRockTiles.Length);

            // Some entries could be null if the array had empty slots; probe forward
            for (int k = 0; k < _runtimeRockTiles.Length; k++)
            {
                int j = (idx + k) % _runtimeRockTiles.Length;
                if (_runtimeRockTiles[j] != null)
                    return _runtimeRockTiles[j];
            }

            return null;
        }

        private static uint Hash3(uint a, uint b, uint c)
        {
            uint x = 0x9E3779B9u;
            x ^= a + 0x85EBCA6Bu + (x << 6) + (x >> 2);
            x ^= b + 0xC2B2AE35u + (x << 6) + (x >> 2);
            x ^= c + 0x27D4EB2Fu + (x << 6) + (x >> 2);

            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }


    }
}
