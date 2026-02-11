using UnityEngine;

namespace MineItUnity.Game.Player
{
    /// <summary>
    /// Presentation-only player sprite animation.
    /// - Idle: single sprite
    /// - Walk: loops through frames while moving
    /// - Facing: rotates sprite to match movement direction (top-down)
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        [Header("Idle Sprites (4-direction)")]
        public Sprite Idle_Left;
        public Sprite Idle_Right;
        public Sprite Idle_Up;
        public Sprite Idle_Down;

        [Header("Move Sprites (4 frames each, 4-direction)")]
        public Sprite[] Move_Left;   // length 4 recommended
        public Sprite[] Move_Right;  // length 4 recommended
        public Sprite[] Move_Up;     // length 4 recommended
        public Sprite[] Move_Down;   // length 4 recommended

        [Header("Animation")]
        [Tooltip("Walk frames per second while moving.")]
        public float WalkFps = 10f;

        [Tooltip("Minimum movement speed (tiles/sec) to consider as 'moving'.")]
        public float MoveThreshold = 0.05f;

        private SpriteRenderer _sr;

        private Vector3 _prevPos;
        private int _frame;
        private float _frameTimer;

        private FacingDir _facing = FacingDir.Down; // default facing on start

        private enum FacingDir
        {
            Down,
            Up,
            Left,
            Right
        }

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            _prevPos = transform.position;

            ApplyIdle(_facing);
        }

        private void LateUpdate()
        {
            Vector3 pos = transform.position;
            Vector3 delta = pos - _prevPos;

            float speed = (Time.unscaledDeltaTime > 1e-6f)
                ? (delta.magnitude / Time.unscaledDeltaTime)
                : 0f;

            bool moving = speed >= MoveThreshold;

            if (moving)
            {
                // Update facing from movement
                _facing = GetFacingFromDelta(delta);

                ApplyMove(_facing);
            }
            else
            {
                ApplyIdle(_facing);
            }

            _prevPos = pos;
        }

        private void ApplyIdle(FacingDir dir)
        {
            if (_sr == null) return;

            // Reset animation so the next time we move, we start at frame 0
            _frameTimer = 0f;
            _frame = 0;

            Sprite idle = dir switch
            {
                FacingDir.Left => Idle_Left,
                FacingDir.Right => Idle_Right,
                FacingDir.Up => Idle_Up,
                _ => Idle_Down
            };

            if (idle != null)
                _sr.sprite = idle;
        }

        private void ApplyMove(FacingDir dir)
        {
            if (_sr == null) return;

            Sprite[] frames = dir switch
            {
                FacingDir.Left => Move_Left,
                FacingDir.Right => Move_Right,
                FacingDir.Up => Move_Up,
                _ => Move_Down
            };

            if (frames == null || frames.Length == 0)
            {
                // Fallback: if move frames missing, show idle
                ApplyIdle(dir);
                return;
            }

            float fps = Mathf.Max(1f, WalkFps);
            float frameDuration = 1f / fps;

            _frameTimer += Time.unscaledDeltaTime;
            while (_frameTimer >= frameDuration)
            {
                _frameTimer -= frameDuration;
                _frame = (_frame + 1) % frames.Length;
            }

            Sprite sp = frames[_frame];
            if (sp != null)
                _sr.sprite = sp;
        }

        private static FacingDir GetFacingFromDelta(Vector3 delta)
        {
            // Choose dominant axis (classic top-down behavior)
            float ax = Mathf.Abs(delta.x);
            float ay = Mathf.Abs(delta.y);

            if (ax >= ay)
                return (delta.x >= 0f) ? FacingDir.Right : FacingDir.Left;
            else
                return (delta.y >= 0f) ? FacingDir.Up : FacingDir.Down;
        }

    }
}
