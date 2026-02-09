using UnityEngine;
using MineIt.Simulation;
using MineIt.Input;
using System.IO;

namespace MineItUnity.Game
{
    public sealed class GameController : MonoBehaviour
    {
        private const double FixedDt = 1.0 / 60.0;
        private double _accumulator;

        private GameSession _session = null!;
        public GameSession Session => _session;

        private Transform _playerMarker = null!;

        private void Awake()
        {
            Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
            Screen.fullScreen = true;

            Application.targetFrameRate = 60;

            _session = new GameSession();
            _session.InitializeNewGame(seed: 12345);

            // Simple player marker (placeholder sprite)
            var go = new GameObject("PlayerMarker");
            _playerMarker = go.transform;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CreateDebugSprite();
            sr.sortingOrder = 10;
        }

        private void Update()
        {
            _accumulator += Time.unscaledDeltaTime;
            if (_accumulator > 0.25) _accumulator = 0.25;

            var input = BuildInputSnapshot();

            // ---- Quit handling (ESC) ----
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
#if UNITY_EDITOR
                Debug.Log("Quit requested (ESC) — ignored in Editor");
#else
            Application.Quit();
#endif
            }

            // ---- Save / Load (F5 / F9) ----
            if (UnityEngine.Input.GetKeyDown(KeyCode.F5))
                SaveGame();

            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
                LoadGame();

            bool oneShotConsumed = false;
            while (_accumulator >= FixedDt)
            {
                var stepInput = input;

                if (oneShotConsumed)
                {
                    stepInput = new InputSnapshot
                    {
                        MoveUp = input.MoveUp,
                        MoveDown = input.MoveDown,
                        MoveLeft = input.MoveLeft,
                        MoveRight = input.MoveRight,

                        ScanPressed = false,
                        ClaimPressed = false,
                        ExtractPressed = false,
                        DepositPressed = false,
                        SellPressed = false,

                        DevSetDetectorTier = 0,
                        DevSetExtractorTier = 0,
                        DevSetBackpackTier = 0,
                    };
                }

                _session.Update(FixedDt, stepInput);
                _accumulator -= FixedDt;
                oneShotConsumed = true;
            }

            // Present: 1 Unity unit = 1 tile (for now)
            _playerMarker.position = new Vector3(
                (float)_session.Player.PositionX,
                (float)_session.Player.PositionY,
                0f);

            // Camera follow
            if (Camera.main != null)
            {
                var cam = Camera.main.transform;
                cam.position = new Vector3(_playerMarker.position.x, _playerMarker.position.y, cam.position.z);
            }

        }


        private string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "MineIt.save.json");

        private void SaveGame()
        {
            try
            {
                string json = MineIt.Save.SaveGameSerializer.SaveToJson(_session);
                System.IO.File.WriteAllText(SavePath, json);

                _session.PostStatus($"SAVE OK ({SavePath})", 1.5);
            }
            catch (System.Exception ex)
            {
                _session.PostStatus($"SAVE FAILED: {ex.Message}", 2.5);
            }
        }

        private void LoadGame()
        {
            try
            {
                if (!System.IO.File.Exists(SavePath))
                {
                    _session.PostStatus("LOAD FAILED: no save file", 2.5);
                    return;
                }

                string json = System.IO.File.ReadAllText(SavePath);
                var data = MineIt.Save.SaveGameSerializer.LoadFromJson(json);

                _session.LoadFromSave(data);

                _session.PostStatus("LOAD OK", 1.5);
            }
            catch (System.Exception ex)
            {
                _session.PostStatus($"LOAD FAILED: {ex.Message}", 2.5);
            }
        }



        private static InputSnapshot BuildInputSnapshot()
        {
            // Unity Y+ is up, core Y+ is down → invert here
            bool up = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
            bool down = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
            bool left = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
            bool right = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);

            bool scan = UnityEngine.Input.GetKeyDown(KeyCode.Space);
            bool claim = UnityEngine.Input.GetKeyDown(KeyCode.C);
            bool extract = UnityEngine.Input.GetKeyDown(KeyCode.E);
            bool deposit = UnityEngine.Input.GetKeyDown(KeyCode.T);
            bool sell = UnityEngine.Input.GetKeyDown(KeyCode.Y);

            int tierKey = KeyToTierNumber();
            int det = 0, ext = 0, bag = 0;

            if (tierKey != 0)
            {
                bool shift = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
                bool ctrl = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);

                if (ctrl) bag = tierKey;
                else if (shift) ext = tierKey;
                else det = tierKey;
            }

            return new InputSnapshot
            {
                MoveUp = up,
                MoveDown = down,
                MoveLeft = left,
                MoveRight = right,

                ScanPressed = scan,
                ClaimPressed = claim,
                ExtractPressed = extract,
                DepositPressed = deposit,
                SellPressed = sell,

                DevSetDetectorTier = det,
                DevSetExtractorTier = ext,
                DevSetBackpackTier = bag,
            };
        }

        private static int KeyToTierNumber()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad1)) return 1;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad2)) return 2;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad3)) return 3;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha4) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad4)) return 4;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha5) || UnityEngine.Input.GetKeyDown(KeyCode.Keypad5)) return 5;
            return 0;
        }

        private static Sprite CreateDebugSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
        }
    }
}
