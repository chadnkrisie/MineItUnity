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

        // Artifacts are unique quest items; never sold.
        private readonly HashSet<string> _artifacts = new HashSet<string>(StringComparer.Ordinal);

        public System.Collections.Generic.IReadOnlyCollection<string> Artifacts => _artifacts;

        public bool HasArtifact(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId)) return false;
            return _artifacts.Contains(artifactId);
        }

        public bool AddArtifact(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId)) return false;
            return _artifacts.Add(artifactId);
        }

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
        public void ClearOreOnly()
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

            ClearOreOnly();
            return (totalCredits, stacks);
        }

        public void LoadOreUnits(IEnumerable<MineIt.Save.StringIntPair> pairs)
        {
            ClearOreOnly();
            if (pairs == null) return;

            foreach (var p in pairs)
            {
                if (p == null || string.IsNullOrEmpty(p.Key) || p.Value <= 0) continue;
                AddOreUnits(p.Key, p.Value);
            }
        }

        public void LoadOreUnits(IDictionary<string, int> oreUnits)
        {
            ClearOreOnly();
            if (oreUnits == null) return;

            foreach (var kv in oreUnits)
            {
                if (kv.Value <= 0) continue;
                AddOreUnits(kv.Key, kv.Value);
            }
        }

        public void LoadArtifacts(System.Collections.Generic.IEnumerable<string> artifactIds)
        {
            _artifacts.Clear();
            if (artifactIds == null) return;

            foreach (var id in artifactIds)
            {
                if (string.IsNullOrWhiteSpace(id)) continue;
                _artifacts.Add(id);
            }
        }

    }
}
