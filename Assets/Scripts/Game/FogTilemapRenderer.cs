using UnityEngine;
using UnityEngine.Tilemaps;
using MineIt.Fog;

namespace MineItUnity.Game
{
    public sealed class FogTilemapRenderer : MonoBehaviour
    {
        public GameController Controller;

        public Tilemap FogBlackTilemap; // undiscovered
        public Tilemap FogDimTilemap;   // discovered-but-not-visible

        public TileBase FogTile;        // a solid black tile (sprite can be opaque)
        public int CameraPaddingTiles = 4;

        private void LateUpdate()
        {
            if (Controller == null) return;
            var session = Controller.Session;
            if (session == null) return;

            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            UpdateFogInCameraBounds(session.Fog, session, cam);
        }

        private void UpdateFogInCameraBounds(FogOfWar fog, MineIt.Simulation.GameSession session, Camera cam)
        {
            // Camera bounds in world units (1 Unity unit == 1 tile)
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;

            Vector3 camPos = cam.transform.position;

            int minTx = Mathf.FloorToInt(camPos.x - halfW) - CameraPaddingTiles;
            int maxTx = Mathf.CeilToInt(camPos.x + halfW) + CameraPaddingTiles;
            int minTy = Mathf.FloorToInt(camPos.y - halfH) - CameraPaddingTiles;
            int maxTy = Mathf.CeilToInt(camPos.y + halfH) + CameraPaddingTiles;

            // Clamp to world
            minTx = Mathf.Clamp(minTx, 0, fog.Width - 1);
            maxTx = Mathf.Clamp(maxTx, 0, fog.Width - 1);
            minTy = Mathf.Clamp(minTy, 0, fog.Height - 1);
            maxTy = Mathf.Clamp(maxTy, 0, fog.Height - 1);

            for (int ty = minTy; ty <= maxTy; ty++)
                for (int tx = minTx; tx <= maxTx; tx++)
                {
                    var cell = new Vector3Int(tx, ty, 0);

                    if (!fog.IsDiscovered(tx, ty))
                    {
                        FogBlackTilemap.SetTile(cell, FogTile);
                        FogDimTilemap.SetTile(cell, null);
                    }
                    else if (!fog.IsVisibleNow(tx, ty))
                    {
                        FogBlackTilemap.SetTile(cell, null);
                        FogDimTilemap.SetTile(cell, FogTile);
                    }
                    else
                    {
                        FogBlackTilemap.SetTile(cell, null);
                        FogDimTilemap.SetTile(cell, null);
                    }
                }
        }
    }
}
