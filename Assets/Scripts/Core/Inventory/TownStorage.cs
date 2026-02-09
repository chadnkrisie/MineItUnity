using System;
using System.Collections.Generic;

namespace MineIt.Inventory
{
    /// <summary>
    /// MVP town storage: ore-only inventory with no capacity limit.
    /// </summary>
    public sealed class TownStorage
    {
        private readonly Dictionary<string, int> _oreUnits = new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, int> OreUnits => _oreUnits;

        public int GetUnits(string oreId)
            => _oreUnits.TryGetValue(oreId, out var u) ? u : 0;

        public void AddOreUnits(string oreId, int units)
        {
            if (units <= 0) return;

            if (_oreUnits.TryGetValue(oreId, out var existing))
                _oreUnits[oreId] = existing + units;
            else
                _oreUnits[oreId] = units;
        }
        public void Clear()
        {
            _oreUnits.Clear();
        }

        public void LoadOreUnits(System.Collections.Generic.IEnumerable<MineIt.Save.StringIntPair> pairs)
        {
            Clear();
            if (pairs == null) return;

            foreach (var p in pairs)
            {
                if (p == null || string.IsNullOrEmpty(p.Key) || p.Value <= 0) continue;
                AddOreUnits(p.Key, p.Value);
            }
        }

        public void LoadOreUnits(System.Collections.Generic.IDictionary<string, int> oreUnits)
        {
            Clear();
            if (oreUnits == null) return;

            foreach (var kv in oreUnits)
            {
                if (kv.Value <= 0) continue;
                AddOreUnits(kv.Key, kv.Value);
            }
        }

    }
}
