using UnityEngine;
using UnityEngine.UI;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Keeps a Sliced Image's corners at the sprite's natural proportions: the sprite is drawn at
    /// (rectHeight / spriteHeight) scale and only the middle stretches horizontally.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class JbelSlicedHeightScaler : MonoBehaviour
    {
        private Image img;
        private RectTransform rt;

        private void OnEnable() { img = GetComponent<Image>(); rt = transform as RectTransform; Apply(); }
        private void OnRectTransformDimensionsChange() { Apply(); }
        private void LateUpdate() { Apply(); }

        private void Apply()
        {
            if (img == null) img = GetComponent<Image>();
            if (rt == null) rt = transform as RectTransform;
            if (img == null || img.sprite == null || img.type != Image.Type.Sliced) return;
            float h = rt.rect.height;
            if (h < 1f) return;
            float refPpu = (img.canvas != null) ? img.canvas.referencePixelsPerUnit : 100f;
            float basePpu = img.sprite.pixelsPerUnit / refPpu;
            float m = img.sprite.rect.height / (h * basePpu);
            m = Mathf.Max(0.05f, m);
            if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, m)) img.pixelsPerUnitMultiplier = m;
        }
    }
}
