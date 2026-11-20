using UnityEngine;
using Unity.Cinemachine;

namespace JbelIghoud.CameraControl
{
    /// <summary>
    /// Keeps the Cinemachine camera above any WaterBody surface so it never dips underwater.
    /// While the player swims or dives, the camera stays on top of the water and looks down at the character.
    /// Runs after the Body stage, so the Aim stage re-aims from the corrected position (no off-centre framing).
    /// </summary>
    [AddComponentMenu("Cinemachine/Jbel Water Surface Clamp")]
    [ExecuteAlways]
    [SaveDuringPlay]
    public class CinemachineWaterSurfaceClamp : CinemachineExtension
    {
        [Tooltip("Minimum height the camera keeps above the water surface (metres).")]
        public float minHeightAboveWater = 0.45f;

        [Tooltip("Extra horizontal margin used when checking whether the camera is over a water body.")]
        public float boundsMargin = 2f;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Body) return;
            if (WaterBody.All == null || WaterBody.All.Count == 0) return;

            Vector3 pos = state.GetCorrectedPosition();
            float highest = float.NegativeInfinity;

            for (int i = 0; i < WaterBody.All.Count; i++)
            {
                var wb = WaterBody.All[i];
                if (wb == null || !wb.isActiveAndEnabled) continue;
                if (!wb.IsPositionInHorizontalBounds(pos, boundsMargin)) continue;
                if (wb.TryGetWaterSurfaceY(pos, out float surfaceY) && surfaceY > highest)
                    highest = surfaceY;
            }

            if (float.IsNegativeInfinity(highest)) return;

            float minY = highest + minHeightAboveWater;
            if (pos.y < minY)
                state.PositionCorrection += new Vector3(0f, minY - pos.y, 0f);
        }
    }
}
