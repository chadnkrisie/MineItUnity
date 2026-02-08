using System;

namespace MineIt.World
{
    public sealed class WorldGenerator
    {
        private readonly int _seed;

        public WorldGenerator(int seed) => _seed = seed;

        // Deterministic chunk RNG seed (stable across runs)
        public int ChunkSeed(int cx, int cy)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + _seed;
                h = h * 31 + cx;
                h = h * 31 + cy;
                // mix
                h ^= (h << 13);
                h ^= (h >> 17);
                h ^= (h << 5);
                return h;
            }
        }

        // 0 = grass, 1 = rock (placeholder visuals)
        public void FillChunkTiles(Chunk chunk)
        {
            var rng = new Random(ChunkSeed(chunk.Coord.Cx, chunk.Coord.Cy));
            for (int ly = 0; ly < Chunk.CHUNK_SIZE_TILES; ly++)
                for (int lx = 0; lx < Chunk.CHUNK_SIZE_TILES; lx++)
                {
                    // Slightly different pattern than before; still deterministic.
                    int r = rng.Next(100);
                    byte tile = (byte)(r < 14 ? 1 : 0);
                    chunk.SetLocalTile(lx, ly, tile);
                }
        }
    }
}
