using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using MineIt.World;

namespace MineItUnity.Game
{
    /// <summary>
    /// Paints sparse detail decals (grass tufts, pebbles, cracks) on a dedicated tilemap.
    /// Deterministic by world seed + tile coord. No saved state needed.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public sealed class DecalTilemapRenderer : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;

        [Header("Decal Sprites")]
        [Tooltip("Small transparent sprites (32x32) like tufts/pebbles/cracks. 2-8 recommended.")]
        public Sprite[] DecalSprites;

        [Header("Density")]
        [Range(0f, 1f)]
        [Tooltip("Probability a grass tile gets a decal. Start small: 0.02..0.08.")]
        public float DecalProbability = 0.05f;

        [Tooltip("Extra tiles beyond camera bounds to ensure no pop-in.")]
        public int CameraPaddingTiles = 2;

        private Tilemap _tilemap;
        private TileBase[] _runtimeDecalTiles;

        // Track painted chunks (we paint per chunk for performance)
        private readonly HashSet<ChunkCoord> _paintedChunks = new HashSet<ChunkCoord>();

        private void Awake()
        {
            _tilemap = GetComponent<Tilemap>();
            BuildRuntimeTiles();
            ForceRepaintAll();
        }

        private void LateUpdate()
        {
            if (Controller == null || Controller.Session == null) return;

            if (_runtimeDecalTiles == null || _runtimeDecalTiles.Length == 0)
                BuildRuntimeTiles();

            PaintVisibleChunks();
        }

        public void ForceRepaintAll()
        {
            _paintedChunks.Clear();
            if (_tilemap != null)
                _tilemap.ClearAllTiles();
        }

        private void BuildRuntimeTiles()
        {
            if (DecalSprites == null || DecalSprites.Length == 0)
            {
                _runtimeDecalTiles = null;
                return;
            }

            _runtimeDecalTiles = new TileBase[DecalSprites.Length];

            for (int i = 0; i < DecalSprites.Length; i++)
            {
                var sp = DecalSprites[i];
                if (sp == null) continue;

                var t = ScriptableObject.CreateInstance<Tile>();
                t.sprite = sp;
                t.colliderType = Tile.ColliderType.None;
                _runtimeDecalTiles[i] = t;
            }
        }

        private void PaintVisibleChunks()
        {
            var s = Controller.Session;
            var chunks = s.Chunks;

            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 camPos = cam.transform.position;

            int minTx = Mathf.FloorToInt(camPos.x - halfW) - CameraPaddingTiles;
            int maxTx = Mathf.CeilToInt(camPos.x + halfW) + CameraPaddingTiles;
            int minTy = Mathf.FloorToInt(camPos.y - halfH) - CameraPaddingTiles;
            int maxTy = Mathf.CeilToInt(camPos.y + halfH) + CameraPaddingTiles;

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
                    var cc = new ChunkCoord(cx, cy);
                    if (_paintedChunks.Contains(cc)) continue;

                    Chunk ch;
                    try { ch = chunks.GetOrLoadChunk(cx, cy); }
                    catch { continue; }

                    PaintChunkDecals(s.Seed, ch);
                    _paintedChunks.Add(cc);
                }
        }

        private void PaintChunkDecals(int seed, Chunk ch)
        {
            if (_runtimeDecalTiles == null || _runtimeDecalTiles.Length == 0) return;
            if (DecalProbability <= 0f) return;

            int chunkSize = Chunk.CHUNK_SIZE_TILES;
            int baseTx = ch.Coord.Cx * chunkSize;
            int baseTy = ch.Coord.Cy * chunkSize;

            for (int ly = 0; ly < chunkSize; ly++)
                for (int lx = 0; lx < chunkSize; lx++)
                {
                    // Only place decals on grass cells (t==0). Skip rocks (t==1).
                    byte t = ch.GetLocalTile(lx, ly);
                    if (t != 0) continue;

                    int tx = baseTx + lx;
                    int ty = baseTy + ly;

                    // Deterministic random for this tile
                    uint h = Hash3((uint)seed, (uint)tx, (uint)ty);

                    // Probability test
                    float u01 = (h & 0x00FFFFFFu) / (float)0x01000000;
                    if (u01 > DecalProbability) continue;

                    int idx = (int)((h >> 24) % (uint)_runtimeDecalTiles.Length);
                    TileBase decal = _runtimeDecalTiles[idx];
                    if (decal == null) continue;

                    _tilemap.SetTile(new Vector3Int(tx, ty, 0), decal);
                }
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
