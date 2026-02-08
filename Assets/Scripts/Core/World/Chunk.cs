using System.Collections.Generic;
using MineIt.Mining;

namespace MineIt.World
{
    public sealed class Chunk
    {
        public const int CHUNK_SIZE_TILES = 32;

        public ChunkCoord Coord { get; }
        public byte[] Tiles { get; } // size = 32*32
        public List<Deposit> Deposits { get; } = new List<Deposit>();
        public bool DepositsPopulated { get; set; }

        public Chunk(ChunkCoord coord)
        {
            Coord = coord;
            Tiles = new byte[CHUNK_SIZE_TILES * CHUNK_SIZE_TILES];
        }

        public byte GetLocalTile(int lx, int ly)
        {
            if ((uint)lx >= CHUNK_SIZE_TILES || (uint)ly >= CHUNK_SIZE_TILES) return 1;
            return Tiles[ly * CHUNK_SIZE_TILES + lx];
        }

        public void SetLocalTile(int lx, int ly, byte v)
        {
            if ((uint)lx >= CHUNK_SIZE_TILES || (uint)ly >= CHUNK_SIZE_TILES) return;
            Tiles[ly * CHUNK_SIZE_TILES + lx] = v;
        }
    }
}
