using UnityEngine;
using MineItUnity.Game.Audio;

namespace MineItUnity.Game
{
    /// <summary>
    /// Unity presentation-only audio adapter.
    /// Subscribes to core GameSession events and plays AudioClips from a ScriptableObject library.
    /// Easy to swap clips later (Unity Store, etc.) by editing the MineItAudioLibrary asset.
    /// </summary>
    public sealed class AudioController : MonoBehaviour
    {
        [Header("References")]
        public GameController Controller;
        public MineItAudioLibrary Library;

        [Header("Sources")]
        [Tooltip("One-shot SFX source (recommended).")]
        public AudioSource SfxSource;

        // Future-proof: you can add a loop source later if you buy a loop hum
        // public AudioSource LoopSource;

        private bool _subscribed;
        private bool _prevExtracting;

        // Tracks which deposits have already been discovered (so we only play discovery once).
        private readonly System.Collections.Generic.HashSet<int> _knownDiscoveredDeposits
            = new System.Collections.Generic.HashSet<int>();


        private void Awake()
        {
            // Optional convenience: auto-create a source if not assigned
            if (SfxSource == null)
            {
                var go = new GameObject("SfxSource");
                go.transform.SetParent(transform, worldPositionStays: false);
                SfxSource = go.AddComponent<AudioSource>();
                SfxSource.playOnAwake = false;
                SfxSource.spatialBlend = 0f; // 2D
            }
        }

        private void OnEnable()
        {
            TrySubscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            // If we weren't able to subscribe yet (script enable order), keep trying.
            if (!_subscribed)
                TrySubscribe();

            if (Controller == null || Controller.Session == null || Library == null || SfxSource == null)
                return;

            // Edge-detect extraction start/stop (core has no events yet; this is adapter-side)
            bool extracting = Controller.Session.ExtractInProgress;

            if (!_prevExtracting && extracting)
                Play(Library.ExtractStart, Library.MasterSfxVolume * Library.ExtractVolume);

            if (_prevExtracting && !extracting)
                Play(Library.ExtractStop, Library.MasterSfxVolume * Library.ExtractVolume);

            _prevExtracting = extracting;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;
            if (Controller == null || Controller.Session == null) return;

            var s = Controller.Session;

            s.ScanExecuted += OnScanExecuted;
            s.ScanDud += OnScanDud;
            s.ClaimSucceeded += OnClaimSucceeded;
            s.ClaimFailed += OnClaimFailed;

            _prevExtracting = s.ExtractInProgress;
            _subscribed = true;

            // Seed discovered set from currently loaded chunks to avoid false "new discovery" dings.
            _knownDiscoveredDeposits.Clear();
            foreach (var ch in s.Chunks.GetLoadedChunks())
            {
                foreach (var d in ch.Deposits)
                {
                    if (d.DiscoveredByPlayer)
                        _knownDiscoveredDeposits.Add(d.DepositId);
                }
            }

        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (Controller == null || Controller.Session == null) { _subscribed = false; return; }

            var s = Controller.Session;

            s.ScanExecuted -= OnScanExecuted;
            s.ScanDud -= OnScanDud;
            s.ClaimSucceeded -= OnClaimSucceeded;
            s.ClaimFailed -= OnClaimFailed;

            _subscribed = false;
        }

        private void OnScanExecuted()
        {
            // 1) Scan executed sound
            Play(Library.ScanExecuted, Library.MasterSfxVolume * Library.ScanVolume);

            // 2) Discovery sound (if this scan revealed any NEW deposits)
            if (Controller == null || Controller.Session == null || Library == null)
                return;

            var s = Controller.Session;

            int newDiscoveries = 0;

            // LastScanResults is populated before ScanExecuted fires (per your GameSession flow).
            var results = s.LastScanResults;
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    int id = results[i].DepositId;

                    // If it wasn't known before, this scan just discovered it (or at least it's the first time we see it)
                    if (_knownDiscoveredDeposits.Add(id))
                        newDiscoveries++;
                }
            }

            // Play once per scan if any new deposits were discovered (avoids spam)
            if (newDiscoveries > 0)
            {
                Play(Library.DepositDiscovered, Library.MasterSfxVolume * Library.DiscoveryVolume);
            }
        }

        private void OnScanDud()
        {
            Play(Library.ScanBlocked, Library.MasterSfxVolume * Library.ScanVolume);
        }

        private void OnClaimSucceeded()
        {
            Play(Library.ClaimSucceeded, Library.MasterSfxVolume * Library.ClaimVolume);
        }

        private void OnClaimFailed()
        {
            Play(Library.ClaimFailed, Library.MasterSfxVolume * Library.ClaimVolume);
        }

        private void Play(AudioClip clip, float volume01)
        {
            if (clip == null || SfxSource == null) return;
            SfxSource.PlayOneShot(clip, Mathf.Clamp01(volume01));
        }
    }
}
