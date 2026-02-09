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

        /// <summary>
        /// Computes total credits for all stored ore using OreCatalog prices, then clears storage.
        /// Returns (creditsGained, stacksSold).
        /// Deterministic, single-pass, and centralizes sell logic.
        /// </summary>
        public (int creditsGained, int stacksSold) ComputeSaleValueAndClear()
        {
            if (_oreUnits.Count == 0)
                return (0, 0);

            int totalCredits = 0;
            int stacks = 0;

            foreach (var kv in _oreUnits)
            {
                int units = kv.Value;
                if (units <= 0) continue;

                int price = OreCatalog.BasePricePerUnit(kv.Key);
                totalCredits += units * price;
                stacks++;
            }

            Clear();
            return (totalCredits, stacks);
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
