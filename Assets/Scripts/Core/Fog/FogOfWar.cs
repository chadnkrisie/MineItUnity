using System;

namespace MineIt.Fog
{
    public sealed class FogOfWar
    {
        private readonly int _w;
        private readonly int _h;

        // Discovered persists (bitset)
        private readonly uint[] _discoveredBits;

        // VisibleNow is per-frame (bool array is fine at 512x512)
        private readonly bool[] _visibleNow;

        public int Width => _w;
        public int Height => _h;

        public FogOfWar(int width, int height)
        {
            _w = width;
            _h = height;

            int bitCount = width * height;
            _discoveredBits = new uint[(bitCount + 31) / 32];
            _visibleNow = new bool[bitCount];
        }

        public void ClearVisibleNow()
        {
            Array.Clear(_visibleNow, 0, _visibleNow.Length);
        }

        public bool IsDiscovered(int tx, int ty)
        {
            if ((uint)tx >= (uint)_w || (uint)ty >= (uint)_h) return false;
            int i = ty * _w + tx;
            int word = i >> 5;
            int bit = i & 31;
            return ((_discoveredBits[word] >> bit) & 1u) != 0;
        }

        public bool IsVisibleNow(int tx, int ty)
        {
            if ((uint)tx >= (uint)_w || (uint)ty >= (uint)_h) return false;
            return _visibleNow[ty * _w + tx];
        }

        public void RevealCircle(int cx, int cy, int radiusTiles)
        {
            int r2 = radiusTiles * radiusTiles;

            int minX = Math.Max(0, cx - radiusTiles);
            int maxX = Math.Min(_w - 1, cx + radiusTiles);
            int minY = Math.Max(0, cy - radiusTiles);
            int maxY = Math.Min(_h - 1, cy + radiusTiles);

            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy > r2) continue;

                    // visible now
                    _visibleNow[y * _w + x] = true;

                    // discovered persistent
                    int i = y * _w + x;
                    int word = i >> 5;
                    int bit = i & 31;
                    _discoveredBits[word] |= (1u << bit);
                }
        }
    }
}
