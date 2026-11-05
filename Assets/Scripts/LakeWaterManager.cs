using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Controls the visual water material for the lake.
/// Allows seamless 1-click toggling between the original static water and the alive Suimono mobile water.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class LakeWaterManager : MonoBehaviour
{
    [Header("Water Materials")]
    [Tooltip("Original static material from the environment model.")]
    public Material originalMaterial;

    [Tooltip("Alive mobile-optimized water material using Suimono wave normals and reflections.")]
    public Material suimonoMaterial;

    [Header("Settings")]
    [Tooltip("Enable to use alive Suimono water. Uncheck to revert to original static water instantly.")]
    [SerializeField]
    private bool useSuimonoWater = true;

    private MeshRenderer meshRenderer;

    public bool UseSuimonoWater
    {
        get => useSuimonoWater;
        set
        {
            if (useSuimonoWater != value)
            {
                useSuimonoWater = value;
                ApplyMaterial();
            }
        }
    }

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        ApplyMaterial();
    }

    private void OnValidate()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }
        ApplyMaterial();
    }

    [ContextMenu("Apply Suimono Alive Water")]
    public void ApplySuimonoWater()
    {
        useSuimonoWater = true;
        ApplyMaterial();
    }

    [ContextMenu("Revert to Original Static Water")]
    public void RevertToOriginal()
    {
        useSuimonoWater = false;
        ApplyMaterial();
    }

    public void ApplyMaterial()
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
        }

        if (meshRenderer == null) return;

        Material targetMat = useSuimonoWater ? suimonoMaterial : originalMaterial;
        if (targetMat != null && meshRenderer.sharedMaterial != targetMat)
        {
            meshRenderer.sharedMaterial = targetMat;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(meshRenderer);
                EditorUtility.SetDirty(gameObject);
            }
#endif
        }
    }
}
