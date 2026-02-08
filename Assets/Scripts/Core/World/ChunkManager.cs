using System;
using System.Collections.Generic;

namespace MineIt.World
{
    public sealed class ChunkManager
    {
        public const int CHUNK_SIZE_TILES = Chunk.CHUNK_SIZE_TILES;

        private readonly int _worldWidthTiles;
        private readonly int _worldHeightTiles;

        private readonly WorldGenerator _gen;
        private readonly int _cacheMaxChunks;

        // Cache + LRU
        private readonly Dictionary<ChunkCoord, Chunk> _chunks = new();
        private readonly LinkedList<ChunkCoord> _lru = new(); // most-recent at end
        private readonly Dictionary<ChunkCoord, LinkedListNode<ChunkCoord>> _lruNodes = new();

        public int WorldWidthTiles => _worldWidthTiles;
        public int WorldHeightTiles => _worldHeightTiles;


        public ChunkManager(int seed, int worldWidthTiles, int worldHeightTiles, int cacheMaxChunks = 256)
        {
            _gen = new WorldGenerator(seed);
            _worldWidthTiles = worldWidthTiles;
            _worldHeightTiles = worldHeightTiles;
            _cacheMaxChunks = Math.Max(16, cacheMaxChunks);
        }

        public System.Collections.Generic.IEnumerable<Chunk> GetLoadedChunks()
        {
            // Expose loaded chunk instances for renderers (Unity-side).
            // Safe: caller must not mutate chunk tiles directly.
            return _chunks.Values;
        }

        public static ChunkCoord TileToChunk(int tx, int ty)
            => new ChunkCoord(FloorDiv(tx, CHUNK_SIZE_TILES), FloorDiv(ty, CHUNK_SIZE_TILES));

        public static (int lx, int ly) TileToLocal(int tx, int ty)
        {
            int lx = Mod(tx, CHUNK_SIZE_TILES);
            int ly = Mod(ty, CHUNK_SIZE_TILES);
            return (lx, ly);
        }

        public Chunk GetOrLoadChunk(int cx, int cy)
        {
            // World bounds clamp (finite world MVP).
            int maxCx = (_worldWidthTiles - 1) / CHUNK_SIZE_TILES;
            int maxCy = (_worldHeightTiles - 1) / CHUNK_SIZE_TILES;
            if (cx < 0 || cy < 0 || cx > maxCx || cy > maxCy)
                throw new ArgumentOutOfRangeException($"Chunk {cx},{cy} outside world.");

            var cc = new ChunkCoord(cx, cy);
            if (_chunks.TryGetValue(cc, out var existing))
            {
                Touch(cc);
                return existing;
            }

            var chunk = new Chunk(cc);
            _gen.FillChunkTiles(chunk);

            _chunks[cc] = chunk;
            AddToLru(cc);

            EvictIfNeeded();
            return chunk;
        }

        public void EnsureActiveRadius(double camXTile, double camYTile, int radiusChunks)
        {
            int centerTx = (int)Math.Floor(camXTile);
            int centerTy = (int)Math.Floor(camYTile);
            var center = TileToChunk(centerTx, centerTy);

            for (int dy = -radiusChunks; dy <= radiusChunks; dy++)
                for (int dx = -radiusChunks; dx <= radiusChunks; dx++)
                {
                    int cx = center.Cx + dx;
                    int cy = center.Cy + dy;

                    // Skip outside finite world bounds.
                    int maxCx = (_worldWidthTiles - 1) / CHUNK_SIZE_TILES;
                    int maxCy = (_worldHeightTiles - 1) / CHUNK_SIZE_TILES;
                    if (cx < 0 || cy < 0 || cx > maxCx || cy > maxCy) continue;

                    _ = GetOrLoadChunk(cx, cy);
                }
        }

        public byte GetTile(int tx, int ty)
        {
            if ((uint)tx >= (uint)_worldWidthTiles || (uint)ty >= (uint)_worldHeightTiles) return 1;

            var cc = TileToChunk(tx, ty);
            var chunk = GetOrLoadChunk(cc.Cx, cc.Cy);
            var (lx, ly) = TileToLocal(tx, ty);
            return chunk.GetLocalTile(lx, ly);
        }

        private void EvictIfNeeded()
        {
            while (_chunks.Count > _cacheMaxChunks)
            {
                var oldest = _lru.First!;
                var cc = oldest.Value;

                _lru.RemoveFirst();
                _lruNodes.Remove(cc);
                _chunks.Remove(cc);
            }
        }

        private void Touch(ChunkCoord cc)
        {
            if (_lruNodes.TryGetValue(cc, out var node))
            {
                _lru.Remove(node);
                _lruNodes.Remove(cc);
            }
            AddToLru(cc);
        }

        private void AddToLru(ChunkCoord cc)
        {
            var node = _lru.AddLast(cc);
            _lruNodes[cc] = node;
        }

        private static int FloorDiv(int a, int b)
        {
            // floor division for negative values too (safe future-proofing)
            int q = a / b;
            int r = a % b;
            if (r != 0 && ((r > 0) != (b > 0))) q--;
            return q;
        }

        private static int Mod(int a, int b)
        {
            int m = a % b;
            if (m < 0) m += Math.Abs(b);
            return m;
        }
    }
}
