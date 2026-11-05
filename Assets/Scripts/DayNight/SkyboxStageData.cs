using System;
using UnityEngine;

namespace JbelIghoud.DayNight
{
    /// <summary>
    /// Represents a single stage in the 24-hour day/night cycle, associated with an AllSky skybox.
    /// </summary>
    [Serializable]
    public class SkyboxStageData
    {
        [Tooltip("Descriptive name of this time phase (e.g., Morning, Sunset, Midnight)")]
        public string stageName = "Day Phase";

        [Tooltip("Start time in 24-hour decimal format (e.g., 7.0 = 07:00, 14.5 = 14:30)")]
        [Range(0f, 24f)]
        public float startHour = 7.0f;

        [Tooltip("Equirect/Cubemap Skybox Material from AllSkyFree")]
        public Material skyboxMaterial;

        [Tooltip("Cubemap texture used for seamless crossfade blending")]
        public Cubemap cubemapTexture;

        [Header("Lighting Settings")]
        [Tooltip("Directional sun/moon light color during this phase")]
        public Color sunColor = Color.white;

        [Tooltip("Directional light intensity during this phase")]
        [Range(0f, 3f)]
        public float sunIntensity = 1.0f;

        [Tooltip("Ambient environment lighting color")]
        public Color ambientColor = new Color(0.25f, 0.25f, 0.3f);

        [Tooltip("Atmospheric fog color")]
        public Color fogColor = new Color(0.6f, 0.7f, 0.8f);

        [Header("Skybox Appearance")]
        [Tooltip("Exposure multiplier for the skybox")]
        [Range(0.1f, 4f)]
        public float exposure = 1.0f;

        [Tooltip("Tint color applied to the skybox")]
        public Color tint = Color.white;

        [Tooltip("Skybox rotation offset in degrees")]
        [Range(0f, 360f)]
        public float rotation = 0f;

        public SkyboxStageData() { }

        public SkyboxStageData(string name, float hour, Material mat, Cubemap cube, Color lightCol, float lightInt, Color ambCol, Color fogCol, float exp = 1.0f)
        {
            stageName = name;
            startHour = hour;
            skyboxMaterial = mat;
            cubemapTexture = cube;
            sunColor = lightCol;
            sunIntensity = lightInt;
            ambientColor = ambCol;
            fogColor = fogCol;
            exposure = exp;
            tint = Color.white;
            rotation = 0f;
        }
    }
}
