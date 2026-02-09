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

        // JsonUtility can't do Dictionary<,> -> store as pairs
        public List<StringIntPair> BackpackOre = new List<StringIntPair>();
        public List<StringIntPair> TownOre = new List<StringIntPair>();

        public List<DepositSaveData> Deposits = new List<DepositSaveData>();

        // Base64 of uint[] discovered bitset
        public string FogDiscoveredBitsBase64 = "";
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

        public bool ClaimedByPlayer;

        // JsonUtility can't do nullable int -> -1 means none
        public int ClaimedByNpcId;

        public bool DiscoveredByPlayer;
    }

    [Serializable]
    public sealed class StringIntPair
    {
        public string Key;
        public int Value;

        public StringIntPair(string key, int value) { Key = key; Value = value; }
    }
}
