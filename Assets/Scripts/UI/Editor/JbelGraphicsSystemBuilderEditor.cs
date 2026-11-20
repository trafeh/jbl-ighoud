using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using JbelIghoud.UI;

namespace JbelIghoud.EditorScripts
{
    public static class JbelGraphicsSystemBuilderEditor
    {
        private const string GENERAL_BG_PATH = "Assets/UI/graphics/general bg.png";
        private const string GRAPHICS_BADGE_PATH = "Assets/UI/graphics/graphics.png";
        private const string OPTIONS_BG_PATH = "Assets/UI/graphics/options bg.png";
        private const string RADIO_BTN_PATH = "Assets/UI/settings/settings/subtitle radio btn.png";
        private const string RADIO_DOT_PATH = "Assets/UI/graphics/radio_dot.png";
        private const string FONT_PATH = "Assets/Fonts/IM_FELL_English_SC SDF.asset";

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static GameObject GetOrCreateUIChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                if (existing.GetComponent<RectTransform>() != null)
                    return existing.gameObject;
                Object.DestroyImmediate(existing.gameObject);
            }
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        [MenuItem("Tools/Jbel Ighoud/Build Complete Graphics Settings Modal")]
        public static void BuildCompleteGraphicsSystem()
        {
            Undo.SetCurrentGroupName("Build Jbel Graphics Settings Modal");
            int undoGroup = Undo.GetCurrentGroup();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var generalBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GENERAL_BG_PATH);
            var graphicsBadgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(GRAPHICS_BADGE_PATH);
            var optionsBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OPTIONS_BG_PATH);
            var radioBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RADIO_BTN_PATH);
            var radioDotSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RADIO_DOT_PATH);

            // 1. Build GraphicsSettingsModal in active scene
            BuildGraphicsSettingsModal(font, generalBgSprite, graphicsBadgeSprite, optionsBgSprite, radioBtnSprite, radioDotSprite);

            // 2. Upgrade SoundSettingsModal Subtitle toggle to circular radio dot
            UpgradeSoundSettingsModalRadioDot(radioDotSprite);

            // 3. Scan & register all buttons to GameAudioManager
            if (JbelIghoud.Audio.GameAudioManager.Instance != null)
            {
                JbelIghoud.Audio.GameAudioManager.Instance.RegisterAllButtonsInScene();
            }

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("<color=green><b>[Jbel Graphics Builder]</b> Successfully built and integrated Graphics Settings Modal & Radio Dot UI!</color>");
        }

        public static void BuildGraphicsSettingsModal(
            TMP_FontAsset font,
            Sprite modalBg,
            Sprite graphicsBadgeBg,
            Sprite optionsBg,
            Sprite radioBtnBg,
            Sprite radioDotBg)
        {
            var settingsModal = GameObject.Find("MobileHUD_Canvas/SettingsScreenModal");
            if (settingsModal == null)
            {
                Debug.LogWarning("[Jbel Graphics Builder] MobileHUD_Canvas/SettingsScreenModal not found in active scene.");
                return;
            }

            GameObject modalGO = GetOrCreateUIChild(settingsModal.transform, "GraphicsSettingsModal");

            var modalRT = modalGO.GetComponent<RectTransform>();
            modalRT.anchorMin = Vector2.zero;
            modalRT.anchorMax = Vector2.one;
            modalRT.sizeDelta = Vector2.zero;
            modalRT.anchoredPosition = Vector2.zero;

            var modalCG = GetOrAddComponent<CanvasGroup>(modalGO);
            modalCG.alpha = 0f;
            modalCG.blocksRaycasts = false;
            modalCG.interactable = false;

            // Dialog Card
            GameObject cardGO = GetOrCreateUIChild(modalGO.transform, "DialogCard");
            var cardRT = cardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.5f, 0.5f);
            cardRT.anchorMax = new Vector2(0.5f, 0.5f);
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            cardRT.anchoredPosition = Vector2.zero;
            cardRT.sizeDelta = new Vector2(880f, 380f);

            var cardImg = GetOrAddComponent<Image>(cardGO);
            cardImg.sprite = modalBg;
            cardImg.type = Image.Type.Sliced;
            cardImg.color = Color.white;

            // 1. Header Pill "GRAPHICS"
            GameObject pillGO = GetOrCreateUIChild(cardGO.transform, "HeaderPill_Graphics");
            var pillRT = pillGO.GetComponent<RectTransform>();
            pillRT.anchorMin = new Vector2(0f, 1f);
            pillRT.anchorMax = new Vector2(0f, 1f);
            pillRT.pivot = new Vector2(0.5f, 0.5f);
            pillRT.anchoredPosition = new Vector2(65f, 0f);
            pillRT.sizeDelta = new Vector2(115f, 44f);

            var pillImg = GetOrAddComponent<Image>(pillGO);
            pillImg.sprite = graphicsBadgeBg;
            pillImg.type = Image.Type.Sliced;
            pillImg.color = Color.white;

            GameObject pillTextGO = GetOrCreateUIChild(pillGO.transform, "Text");
            var ptRT = pillTextGO.GetComponent<RectTransform>();
            ptRT.anchorMin = Vector2.zero;
            ptRT.anchorMax = Vector2.one;
            ptRT.sizeDelta = Vector2.zero;
            ptRT.anchoredPosition = Vector2.zero;

            var ptTMP = GetOrAddComponent<TextMeshProUGUI>(pillTextGO);
            ptTMP.font = font;
            ptTMP.fontSize = 17f;
            ptTMP.fontStyle = FontStyles.Bold;
            ptTMP.color = new Color(0.96f, 0.92f, 0.84f, 1f);
            ptTMP.alignment = TextAlignmentOptions.Center;
            ptTMP.text = "GRAPHICS";

            // 2. Close Button 'X'
            GameObject closeGO = GetOrCreateUIChild(cardGO.transform, "CloseButton");
            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 1f);
            closeRT.anchorMax = new Vector2(1f, 1f);
            closeRT.pivot = new Vector2(0.5f, 0.5f);
            closeRT.anchoredPosition = new Vector2(-24f, 0f);
            closeRT.sizeDelta = new Vector2(46f, 46f);

            var closeImg = GetOrAddComponent<Image>(closeGO);
            closeImg.sprite = radioBtnBg;
            closeImg.type = Image.Type.Simple;
            closeImg.color = Color.white;

            var closeBtn = GetOrAddComponent<Button>(closeGO);
            ColorBlock cb = closeBtn.colors;
            cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            closeBtn.colors = cb;

            GameObject closeTextGO = GetOrCreateUIChild(closeGO.transform, "Text_X");
            var ctRT = closeTextGO.GetComponent<RectTransform>();
            ctRT.anchorMin = Vector2.zero;
            ctRT.anchorMax = Vector2.one;
            ctRT.sizeDelta = Vector2.zero;
            ctRT.anchoredPosition = Vector2.zero;

            var ctTMP = GetOrAddComponent<TextMeshProUGUI>(closeTextGO);
            ctTMP.font = font;
            ctTMP.fontSize = 24f;
            ctTMP.fontStyle = FontStyles.Bold;
            ctTMP.color = Color.white;
            ctTMP.alignment = TextAlignmentOptions.Center;
            ctTMP.text = "X";

            // 3. Row 1: Quality Options (Low, Medium, High, Realistic)
            GameObject rowQualityGO = GetOrCreateUIChild(cardGO.transform, "Row_Quality");
            var rqRT = rowQualityGO.GetComponent<RectTransform>();
            rqRT.anchorMin = new Vector2(0.5f, 0.5f);
            rqRT.anchorMax = new Vector2(0.5f, 0.5f);
            rqRT.pivot = new Vector2(0.5f, 0.5f);
            rqRT.anchoredPosition = new Vector2(0f, 65f);
            rqRT.sizeDelta = new Vector2(790f, 54f);

            CreatePillButton(rowQualityGO.transform, "Btn_Low", "Low", new Vector2(-294f, 0f), new Vector2(186f, 52f), optionsBg, font);
            CreatePillButton(rowQualityGO.transform, "Btn_Medium", "Medium", new Vector2(-98f, 0f), new Vector2(186f, 52f), optionsBg, font);
            CreatePillButton(rowQualityGO.transform, "Btn_High", "High", new Vector2(98f, 0f), new Vector2(186f, 52f), optionsBg, font);
            CreatePillButton(rowQualityGO.transform, "Btn_Realistic", "Realistic", new Vector2(294f, 0f), new Vector2(186f, 52f), optionsBg, font);

            // 4. Row 2: FPS Limiter (30 FPS, 60 FPS)
            GameObject rowFpsGO = GetOrCreateUIChild(cardGO.transform, "Row_FPS");
            var rfRT = rowFpsGO.GetComponent<RectTransform>();
            rfRT.anchorMin = new Vector2(0.5f, 0.5f);
            rfRT.anchorMax = new Vector2(0.5f, 0.5f);
            rfRT.pivot = new Vector2(0.5f, 0.5f);
            rfRT.anchoredPosition = new Vector2(0f, 2f);
            rfRT.sizeDelta = new Vector2(790f, 54f);

            CreatePillButton(rowFpsGO.transform, "Btn_30FPS", "30 FPS", new Vector2(-196f, 0f), new Vector2(382f, 52f), optionsBg, font);
            CreatePillButton(rowFpsGO.transform, "Btn_60FPS", "60 FPS", new Vector2(196f, 0f), new Vector2(382f, 52f), optionsBg, font);

            // 5. Row 3: Water Effects Toggle (HTML-Style Circular Radio Button)
            GameObject rowWaterGO = GetOrCreateUIChild(cardGO.transform, "Row_Water");
            var rwRT = rowWaterGO.GetComponent<RectTransform>();
            rwRT.anchorMin = new Vector2(0.5f, 0.5f);
            rwRT.anchorMax = new Vector2(0.5f, 0.5f);
            rwRT.pivot = new Vector2(0.5f, 0.5f);
            rwRT.anchoredPosition = new Vector2(0f, -68f);
            rwRT.sizeDelta = new Vector2(790f, 54f);

            // Label "WATER EFFECTS"
            GameObject waterLabelGO = GetOrCreateUIChild(rowWaterGO.transform, "WaterLabel");
            var wlRT = waterLabelGO.GetComponent<RectTransform>();
            wlRT.anchorMin = new Vector2(0f, 0.5f);
            wlRT.anchorMax = new Vector2(0f, 0.5f);
            wlRT.pivot = new Vector2(0f, 0.5f);
            wlRT.anchoredPosition = new Vector2(45f, 0f);
            wlRT.sizeDelta = new Vector2(210f, 38f);

            var wlTMP = GetOrAddComponent<TextMeshProUGUI>(waterLabelGO);
            wlTMP.font = font;
            wlTMP.fontSize = 23f;
            wlTMP.fontStyle = FontStyles.Normal;
            wlTMP.color = new Color(0.95f, 0.89f, 0.81f, 1f);
            wlTMP.alignment = TextAlignmentOptions.Left;
            wlTMP.text = "WATER EFFECTS";

            // Circular Radio Button
            GameObject waterRadioGO = GetOrCreateUIChild(rowWaterGO.transform, "WaterRadioBtn");
            var wrRT = waterRadioGO.GetComponent<RectTransform>();
            wrRT.anchorMin = new Vector2(0f, 0.5f);
            wrRT.anchorMax = new Vector2(0f, 0.5f);
            wrRT.pivot = new Vector2(0.5f, 0.5f);
            wrRT.anchoredPosition = new Vector2(275f, 0f);
            wrRT.sizeDelta = new Vector2(44f, 44f);

            var wrImg = GetOrAddComponent<Image>(waterRadioGO);
            wrImg.sprite = radioBtnBg;
            wrImg.type = Image.Type.Simple;
            wrImg.color = Color.white;

            var wrBtn = GetOrAddComponent<Button>(waterRadioGO);

            // Inner Circle Dot (Visible when checked)
            GameObject waterDotGO = GetOrCreateUIChild(waterRadioGO.transform, "RadioDot");
            var wdRT = waterDotGO.GetComponent<RectTransform>();
            wdRT.anchorMin = new Vector2(0.5f, 0.5f);
            wdRT.anchorMax = new Vector2(0.5f, 0.5f);
            wdRT.pivot = new Vector2(0.5f, 0.5f);
            wdRT.anchoredPosition = Vector2.zero;
            wdRT.sizeDelta = new Vector2(22f, 22f);

            var wdImg = GetOrAddComponent<Image>(waterDotGO);
            wdImg.sprite = radioDotBg;
            wdImg.type = Image.Type.Simple;
            wdImg.color = new Color(0.98f, 0.95f, 0.88f, 1f);

            // Attach & AutoWire JbelGraphicsSettingsModal
            var graphicsModal = GetOrAddComponent<JbelGraphicsSettingsModal>(modalGO);
            graphicsModal.AutoWire();

            // Wire to JbelSettingsMenuController
            var settingsCtrl = settingsModal.GetComponent<JbelSettingsMenuController>();
            if (settingsCtrl != null)
            {
                var sCtrlSO = new SerializedObject(settingsCtrl);
                sCtrlSO.FindProperty("graphicsSettingsModal").objectReferenceValue = graphicsModal;
                sCtrlSO.ApplyModifiedProperties();
            }

            modalGO.SetActive(false);
        }

        private static void CreatePillButton(
            Transform parent,
            string btnName,
            string labelText,
            Vector2 anchoredPos,
            Vector2 size,
            Sprite bgSprite,
            TMP_FontAsset font)
        {
            GameObject btnGO = GetOrCreateUIChild(parent, btnName);
            var rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = GetOrAddComponent<Image>(btnGO);
            img.sprite = bgSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var btn = GetOrAddComponent<Button>(btnGO);
            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cb;

            GameObject textGO = GetOrCreateUIChild(btnGO.transform, "Text");
            var tRT = textGO.GetComponent<RectTransform>();
            tRT.anchorMin = Vector2.zero;
            tRT.anchorMax = Vector2.one;
            tRT.sizeDelta = Vector2.zero;
            tRT.anchoredPosition = Vector2.zero;

            var tmp = GetOrAddComponent<TextMeshProUGUI>(textGO);
            tmp.font = font;
            tmp.fontSize = 20f;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = new Color(0.72f, 0.63f, 0.52f, 0.90f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = labelText;
        }

        public static void UpgradeSoundSettingsModalRadioDot(Sprite radioDotBg)
        {
            var soundModal = GameObject.Find("MobileHUD_Canvas/SettingsScreenModal/SoundSettingsModal");
            if (soundModal == null) return;

            var radioBtn = soundModal.transform.Find("DialogCard/Row_Voice/SubtitlesGroup/SubtitleRadioBtn");
            if (radioBtn == null) return;

            // Hide or remove old CheckX
            var checkX = radioBtn.Find("CheckX");
            if (checkX != null)
            {
                checkX.gameObject.SetActive(false);
            }

            // Create RadioDot
            GameObject dotGO = GetOrCreateUIChild(radioBtn, "RadioDot");
            var dRT = dotGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0.5f, 0.5f);
            dRT.anchorMax = new Vector2(0.5f, 0.5f);
            dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.anchoredPosition = Vector2.zero;
            dRT.sizeDelta = new Vector2(22f, 22f);

            var dImg = GetOrAddComponent<Image>(dotGO);
            dImg.sprite = radioDotBg;
            dImg.type = Image.Type.Simple;
            dImg.color = new Color(0.98f, 0.95f, 0.88f, 1f);

            var soundCtrl = soundModal.GetComponent<JbelSoundSettingsModal>();
            if (soundCtrl != null)
            {
                soundCtrl.AutoWire();
                soundCtrl.SyncUIFromAudioSettings();
            }
        }
    }
}
