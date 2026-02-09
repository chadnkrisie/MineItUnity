using UnityEngine;

namespace MineItUnity.Game.Audio
{
    [CreateAssetMenu(fileName = "MineItAudioLibrary", menuName = "MineIt/Audio Library", order = 10)]
    public sealed class MineItAudioLibrary : ScriptableObject
    {
        [Header("Scan")]
        public AudioClip ScanExecuted;
        public AudioClip ScanBlocked;

        [Header("Claim")]
        public AudioClip ClaimSucceeded;
        public AudioClip ClaimFailed;

        [Header("Extract")]
        public AudioClip ExtractStart;
        public AudioClip ExtractStop;

        [Header("Discovery")]
        public AudioClip DepositDiscovered;

        [Header("Tuning")]
        [Range(0f, 1f)] public float MasterSfxVolume = 0.8f;
        [Range(0f, 1f)] public float ScanVolume = 0.9f;
        [Range(0f, 1f)] public float ClaimVolume = 0.9f;
        [Range(0f, 1f)] public float ExtractVolume = 0.7f;
        [Range(0f, 1f)] public float DiscoveryVolume = 0.85f;

    }
}
