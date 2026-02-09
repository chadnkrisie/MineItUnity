using System;
using MineIt.Simulation;
using UnityEngine; // JsonUtility

namespace MineIt.Save
{
    public static class SaveGameSerializer
    {
        public static string SaveToJson(GameSession s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var data = new SaveGameData
            {
                Version = 1,
                Seed = s.Seed,
                TotalRealSeconds = s.Clock.TotalRealSeconds
            };

            // Player
            data.Player.PositionX = s.Player.PositionX;
            data.Player.PositionY = s.Player.PositionY;
            data.Player.DetectorTier = s.Player.DetectorTier;
            data.Player.ExtractorTier = s.Player.ExtractorTier;
            data.Player.BackpackTier = s.BackpackTier;
            data.Player.Credits = s.Credits;

            // Inventory (pairs)
            data.BackpackOre.Clear();
            if (s.Backpack != null)
            {
                foreach (var kv in s.Backpack.OreUnits)
                    data.BackpackOre.Add(new StringIntPair(kv.Key, kv.Value));
            }

            data.TownOre.Clear();
            if (s.TownStorage != null)
            {
                foreach (var kv in s.TownStorage.OreUnits)
                    data.TownOre.Add(new StringIntPair(kv.Key, kv.Value));
            }

            // Deposits (mutable deltas)
            data.Deposits.Clear();
            foreach (var d in s.Deposits.GetAllDeposits())
            {
                data.Deposits.Add(new DepositSaveData
                {
                    DepositId = d.DepositId,
                    RemainingUnits = d.RemainingUnits,
                    ClaimedByPlayer = d.ClaimedByPlayer,
                    ClaimedByNpcId = d.ClaimedByNpcId.HasValue ? d.ClaimedByNpcId.Value : -1,
                    DiscoveredByPlayer = d.DiscoveredByPlayer
                });
            }

            // Fog discovered bits (uint[] -> byte[] -> base64)
            var bits = s.Fog.CopyDiscoveredBits();
            byte[] bytes = new byte[bits.Length * sizeof(uint)];
            Buffer.BlockCopy(bits, 0, bytes, 0, bytes.Length);
            data.FogDiscoveredBitsBase64 = Convert.ToBase64String(bytes);

            // Pretty print = true for readability
            return JsonUtility.ToJson(data, prettyPrint: true);
        }

        public static SaveGameData LoadFromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Empty save json", nameof(json));

            var data = JsonUtility.FromJson<SaveGameData>(json);
            if (data == null) throw new InvalidOperationException("Failed to deserialize save data.");
            return data;
        }
    }
}
