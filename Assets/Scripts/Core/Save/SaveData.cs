using System;
using System.Collections.Generic;

namespace MineIt.Save
{
    [Serializable]
    public sealed class SaveGameData
    {
        public int Version = 1;

        public int Seed;
        public double TotalRealSeconds;

        public PlayerSaveData Player = new PlayerSaveData();

        public bool HasWon;

        public bool VaultAuthInProgress;
        public double VaultAuthRemainingSeconds;

        // JsonUtility can't do Dictionary<,> -> store as pairs
        public List<StringIntPair> BackpackOre = new List<StringIntPair>();
        public List<StringIntPair> TownOre = new List<StringIntPair>();

        // Artifacts are unique quest items (IDs). Stored separately from ore.
        public List<string> BackpackArtifacts = new List<string>();
        public List<string> TownArtifacts = new List<string>();

        public List<DepositSaveData> Deposits = new List<DepositSaveData>();
        public List<NpcMinerSaveData> NpcMiners = new List<NpcMinerSaveData>();

        // Base64 of uint[] discovered bitset
        public string FogDiscoveredBitsBase64 = "";

        // ===== Waypoint persistence (Unity-owned state, saved here) =====
        public bool HasWaypoint;
        public int WaypointTx;
        public int WaypointTy;
    }

    [Serializable]
    public sealed class NpcMinerSaveData
    {
        public int NpcId;
        public int Tier;

        public int TargetDepositId;

        public double DecisionCooldownRemainingSeconds;
        public double DecisionCooldownMinSeconds;
        public double DecisionCooldownMaxSeconds;

        public double ExtractKgCarry;
    }


    [Serializable]
    public sealed class PlayerSaveData
    {
        public double PositionX;
        public double PositionY;

        public int DetectorTier;
        public int ExtractorTier;

        public int BackpackTier;
        public int Credits;
    }

    [Serializable]
    public sealed class DepositSaveData
    {
        public int DepositId;
        public int RemainingUnits;

        // Artifacts
        public bool IsArtifact;
        public string ArtifactId;

        public bool ClaimedByPlayer;

        // JsonUtility can't do nullable int -> -1 means none
        public int ClaimedByNpcId;

        public bool DiscoveredByPlayer;
        public bool IsDepleted;

    }

    [Serializable]
    public sealed class StringIntPair
    {
        public string Key;
        public int Value;

        public StringIntPair(string key, int value) { Key = key; Value = value; }
    }
}
