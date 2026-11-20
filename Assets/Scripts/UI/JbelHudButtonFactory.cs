using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Builds new HUD buttons that look exactly like the existing round ones (BAG / JUMP / MAP...).
    /// Those buttons use an image for their label, so the copy swaps it for real text in the HUD font.
    /// </summary>
    public static class JbelHudButtonFactory
    {
        public static readonly Color LabelColor = new Color(0.96f, 0.89f, 0.74f);

        /// <summary>Copy <paramref name="template"/> under <paramref name="parent"/> with a text label.</summary>
        public static Button Create(Button template, Transform parent, string name, string label)
        {
            var go = Object.Instantiate(template.gameObject, parent);
            go.name = name;
            go.SetActive(true);
            go.transform.localScale = Vector3.one;

            var btn = go.GetComponent<Button>();
            btn.onClick = new Button.ButtonClickedEvent();

            // remove the baked label image(s) / old texts, keep the round background
            for (int i = go.transform.childCount - 1; i >= 0; i--)
            {
                var c = go.transform.GetChild(i);
                if (c.name.Contains("Label") || c.name.Contains("Icon") || c.GetComponent<TMP_Text>() != null)
                    Object.Destroy(c.gameObject);
            }

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = new Vector2(0.1f, 0.18f);
            rt.anchorMax = new Vector2(0.9f, 0.82f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = textGo.GetComponent<TextMeshProUGUI>();
            var font = HudFont();
            if (font != null) t.font = font;
            t.text = label;
            t.enableAutoSizing = true;
            t.fontSizeMin = 14f;
            t.fontSizeMax = 44f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = LabelColor;
            t.raycastTarget = false;
            t.characterSpacing = 2f;
            return btn;
        }

        /// <summary>The font used by the HUD's text (objective banner / survivor card).</summary>
        public static TMP_FontAsset HudFont()
        {
            var panel = GameObject.Find("JbelHUD_Panel");
            if (panel != null)
            {
                var t = panel.GetComponentInChildren<TMP_Text>(true);
                if (t != null) return t.font;
            }
            var any = Object.FindAnyObjectByType<TextMeshProUGUI>();
            return any != null ? any.font : null;
        }
    }
}
