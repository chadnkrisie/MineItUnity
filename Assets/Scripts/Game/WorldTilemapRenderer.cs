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

        [Header("Tiles")]
        public TileBase GrassTile;
        public TileBase RockTile;

        // Track which chunks have already been painted
        private readonly HashSet<ChunkCoord> _paintedChunks = new HashSet<ChunkCoord>();

        private Tilemap _tilemap;

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
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

                    PaintChunk(chunk);
                    _paintedChunks.Add(coord);
                }
        }

        private void PaintChunk(Chunk chunk)
        {
            int chunkSize = Chunk.CHUNK_SIZE_TILES;
            int baseTx = chunk.Coord.Cx * chunkSize;
            int baseTy = chunk.Coord.Cy * chunkSize;

            for (int ly = 0; ly < chunkSize; ly++)
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    byte t = chunk.GetLocalTile(lx, ly);
                    TileBase tile = (t == 0) ? GrassTile : RockTile;

                    Vector3Int cell = new Vector3Int(
                        baseTx + lx,
                        baseTy + ly,
                        0);

                    _tilemap.SetTile(cell, tile);
                }
        }
    }
}
