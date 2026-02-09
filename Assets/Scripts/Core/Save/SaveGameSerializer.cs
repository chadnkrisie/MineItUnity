using System;
using MineIt.Simulation;
using UnityEngine; // JsonUtility

namespace MineIt.Save
{
    public static class SaveGameSerializer
    {
        public static SaveGameData SaveToData(GameSession s)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            var data = new SaveGameData
            {
                Version = 2, // bumped because format added waypoint fields
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

            data.HasWon = s.HasWon;
            data.VaultAuthInProgress = s.VaultAuthInProgress;
            data.VaultAuthRemainingSeconds = s.VaultAuthRemainingSeconds;


            // Inventory (pairs)
            data.BackpackOre.Clear();
            if (s.Backpack != null)
            {
                foreach (var kv in s.Backpack.OreUnits)
                    data.BackpackOre.Add(new StringIntPair(kv.Key, kv.Value));
            }

            data.BackpackArtifacts.Clear();
            if (s.Backpack != null)
            {
                foreach (var aid in s.Backpack.Artifacts)
                    data.BackpackArtifacts.Add(aid);
            }

            data.TownOre.Clear();
            if (s.TownStorage != null)
            {
                foreach (var kv in s.TownStorage.OreUnits)
                    data.TownOre.Add(new StringIntPair(kv.Key, kv.Value));
            }

            data.TownArtifacts.Clear();
            if (s.TownStorage != null)
            {
                foreach (var aid in s.TownStorage.Artifacts)
                    data.TownArtifacts.Add(aid);
            }

            // Deposits (mutable deltas)
            data.Deposits.Clear();
            foreach (var d in s.Deposits.GetAllDeposits())
            {
                data.Deposits.Add(new DepositSaveData
                {
                    DepositId = d.DepositId,
                    RemainingUnits = d.RemainingUnits,

                    // Artifacts
                    IsArtifact = d.IsArtifact,
                    ArtifactId = d.ArtifactId,

                    ClaimedByPlayer = d.ClaimedByPlayer,
                    ClaimedByNpcId = d.ClaimedByNpcId.HasValue ? d.ClaimedByNpcId.Value : -1,
                    DiscoveredByPlayer = d.DiscoveredByPlayer
                });
            }

            // NPC miners (Phase 1)
            data.NpcMiners.Clear();
            if (s.Npcs != null)
            {
                s.Npcs.SaveTo(data.NpcMiners);
            }

            // Fog discovered bits (uint[] -> byte[] -> base64)
            var bits = s.Fog.CopyDiscoveredBits();
            byte[] bytes = new byte[bits.Length * sizeof(uint)];
            Buffer.BlockCopy(bits, 0, bytes, 0, bytes.Length);
            data.FogDiscoveredBitsBase64 = Convert.ToBase64String(bytes);

            return data;
        }

        public static string ToJson(SaveGameData data, bool prettyPrint = true)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            return JsonUtility.ToJson(data, prettyPrint: prettyPrint);
        }

        // Back-compat wrapper (existing callers still work)
        public static string SaveToJson(GameSession s)
        {
            var data = SaveToData(s);
            return ToJson(data, prettyPrint: true);
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
