using System.Collections.Generic;
using MineIt.Mining;

namespace MineIt.World
{
    public sealed class Chunk
    {
        public const int CHUNK_SIZE_TILES = 32;

        public ChunkCoord Coord { get; }

        private readonly byte[] _tiles; // size = 32*32

        private readonly List<Deposit> _deposits = new List<Deposit>();
        public System.Collections.Generic.IReadOnlyList<Deposit> Deposits => _deposits;

        internal bool DepositsPopulated { get; set; }

        // Core-only accessors (DepositManager / generator use these)
        internal List<Deposit> DepositsMutable => _deposits;

        public Chunk(ChunkCoord coord)
        {
            Coord = coord;
            _tiles = new byte[CHUNK_SIZE_TILES * CHUNK_SIZE_TILES];
        }

        public byte GetLocalTile(int lx, int ly)
        {
            if ((uint)lx >= CHUNK_SIZE_TILES || (uint)ly >= CHUNK_SIZE_TILES) return 1;
            return _tiles[ly * CHUNK_SIZE_TILES + lx];
        }

        internal void SetLocalTile(int lx, int ly, byte v)
        {
            if ((uint)lx >= CHUNK_SIZE_TILES || (uint)ly >= CHUNK_SIZE_TILES) return;
            _tiles[ly * CHUNK_SIZE_TILES + lx] = v;
        }
    }
}
