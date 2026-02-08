using System;

namespace MineIt.World
{
    public readonly struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public readonly int Cx;
        public readonly int Cy;

        public ChunkCoord(int cx, int cy) { Cx = cx; Cy = cy; }

        public bool Equals(ChunkCoord other) => Cx == other.Cx && Cy == other.Cy;
        public override bool Equals(object? obj) => obj is ChunkCoord other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cx, Cy);
        public override string ToString() => $"({Cx},{Cy})";
    }
}
