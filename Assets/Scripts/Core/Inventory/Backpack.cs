using System;
using System.Collections.Generic;

namespace MineIt.Inventory
{
    /// <summary>
    /// MVP backpack: ore-only inventory with mass-based capacity.
    /// Deterministic and allocation-light.
    /// </summary>
    public sealed class Backpack
    {
        private readonly Dictionary<string, int> _oreUnits = new Dictionary<string, int>(StringComparer.Ordinal);

        public double CapacityKg { get; set; } = 50.0;

        // Computed from contents; cached for speed (update on add/remove).
        public double CurrentKg { get; private set; }

        public IReadOnlyDictionary<string, int> OreUnits => _oreUnits;

        public double RemainingKg => Math.Max(0.0, CapacityKg - CurrentKg);

        public bool IsFull => CurrentKg >= CapacityKg - 1e-9;

        public int GetUnits(string oreId)
            => _oreUnits.TryGetValue(oreId, out var u) ? u : 0;

        public bool CanAddOreUnits(string oreId, int units)
        {
            if (units <= 0) return true;
            double addKg = units * OreCatalog.UnitMassKg(oreId);
            return CurrentKg + addKg <= CapacityKg + 1e-9;
        }

        /// <summary>
        /// Adds up to 'unitsRequested' ore units, bounded by capacity.
        /// Returns actual units added.
        /// </summary>
        public int AddOreUnitsClamped(string oreId, int unitsRequested)
        {
            if (unitsRequested <= 0) return 0;

            double unitKg = OreCatalog.UnitMassKg(oreId);
            if (unitKg <= 0) unitKg = 1.0;

            double roomKg = RemainingKg;
            int maxUnitsByWeight = (int)Math.Floor(roomKg / unitKg);
            if (maxUnitsByWeight <= 0) return 0;

            int addUnits = Math.Min(unitsRequested, maxUnitsByWeight);

            if (_oreUnits.TryGetValue(oreId, out var existing))
                _oreUnits[oreId] = existing + addUnits;
            else
                _oreUnits[oreId] = addUnits;

            CurrentKg += addUnits * unitKg;
            if (CurrentKg > CapacityKg) CurrentKg = CapacityKg; // guard

            return addUnits;
        }

        public void Clear()
        {
            _oreUnits.Clear();
            CurrentKg = 0.0;
        }

        public void LoadOreUnits(System.Collections.Generic.IEnumerable<MineIt.Save.StringIntPair> pairs)
        {
            Clear();
            if (pairs == null) return;

            foreach (var p in pairs)
            {
                if (p == null || string.IsNullOrEmpty(p.Key) || p.Value <= 0) continue;

                _oreUnits[p.Key] = p.Value;

                double unitKg = OreCatalog.UnitMassKg(p.Key);
                if (unitKg <= 0) unitKg = 1.0;
                CurrentKg += p.Value * unitKg;
            }

            if (CurrentKg > CapacityKg) CurrentKg = CapacityKg;
        }

        public void LoadOreUnits(System.Collections.Generic.IDictionary<string, int> oreUnits)
        {
            Clear();

            if (oreUnits == null) return;

            foreach (var kv in oreUnits)
            {
                string oreId = kv.Key;
                int units = kv.Value;
                if (units <= 0) continue;

                // Bypass clamping because save data should be authoritative.
                _oreUnits[oreId] = units;

                double unitKg = OreCatalog.UnitMassKg(oreId);
                if (unitKg <= 0) unitKg = 1.0;
                CurrentKg += units * unitKg;
            }

            // Never exceed capacity; keep deterministic and safe
            if (CurrentKg > CapacityKg) CurrentKg = CapacityKg;
        }

    }
}
