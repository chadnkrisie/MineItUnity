using System;

namespace MineIt.World
{
    public sealed class TileMap
    {
        public int Width { get; }
        public int Height { get; }

        // 0 = grass, 1 = rock (for now)
        private readonly byte[] _tiles;

        public TileMap(int width, int height)
        {
            Width = width;
            Height = height;
            _tiles = new byte[width * height];
        }

        public byte GetTile(int tx, int ty)
        {
            if ((uint)tx >= (uint)Width || (uint)ty >= (uint)Height) return 1;
            return _tiles[ty * Width + tx];
        }

        public void Generate(int seed)
        {
            // Simple deterministic pattern (placeholder for noise/biomes later).
            var rng = new Random(seed);

            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    // Light “rock speckle” pattern.
                    int r = rng.Next(100);
                    _tiles[y * Width + x] = (byte)(r < 12 ? 1 : 0);
                }
        }
    }
}
