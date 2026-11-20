using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using JbelIghoud.UI;

namespace JbelIghoud.EditorScripts
{
    public static class JbelSamplesInventoryBuilderEditor
    {
        private const string INV_BG_PATH = "Assets/UI/inv/inv's bg.png";
        private const string INFOS_BG_PATH = "Assets/UI/inv/infos about chosed object.png";
        private const string SAMPLE_BADGE_PATH = "Assets/UI/inv/sample.png";
        private const string WINDOW_OBJECT_PATH = "Assets/UI/inv/window object.png";
        private const string CHOSED_BG_PATH = "Assets/UI/inv/chosed object's bg.png";
        private const string FILL_BORDER_PATH = "Assets/UI/inv/fill's border.png";
        private const string FILL_PATH = "Assets/UI/inv/fill.png";
        private const string INSPECT_BTN_PATH = "Assets/UI/inv/inspect's btn.png";
        private const string RADIO_BTN_PATH = "Assets/UI/settings/settings/subtitle radio btn.png";
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

        [MenuItem("Tools/Jbel Ighoud/Build Complete Samples Inventory Modal")]
        public static void BuildCompleteSamplesInventory()
        {
            Undo.SetCurrentGroupName("Build Jbel Samples Inventory Modal");
            int undoGroup = Undo.GetCurrentGroup();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var invBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(INV_BG_PATH);
            var infosBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(INFOS_BG_PATH);
            var sampleBadgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SAMPLE_BADGE_PATH);
            var windowObjectSprite = AssetDatabase.LoadAssetAtPath<Sprite>(WINDOW_OBJECT_PATH);
            var chosedBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CHOSED_BG_PATH);
            var fillBorderSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FILL_BORDER_PATH);
            var fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FILL_PATH);
            var inspectBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(INSPECT_BTN_PATH);
            var radioBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RADIO_BTN_PATH);

            var hudCanvas = GameObject.Find("MobileHUD_Canvas");
            if (hudCanvas == null)
            {
                Debug.LogWarning("[Jbel Inventory Builder] MobileHUD_Canvas not found in active scene.");
                return;
            }

            // Disable old ExpeditionInventoryModal if present
            var oldModal = hudCanvas.transform.Find("ExpeditionInventoryModal");
            if (oldModal != null)
            {
                oldModal.gameObject.SetActive(false);
            }

            // 1. Root Modal
            GameObject modalGO = GetOrCreateUIChild(hudCanvas.transform, "SamplesInventoryModal");
            var modalRT = modalGO.GetComponent<RectTransform>();
            modalRT.anchorMin = Vector2.zero;
            modalRT.anchorMax = Vector2.one;
            modalRT.sizeDelta = Vector2.zero;
            modalRT.anchoredPosition = Vector2.zero;

            var modalCG = GetOrAddComponent<CanvasGroup>(modalGO);
            modalCG.alpha = 0f;
            modalCG.blocksRaycasts = false;
            modalCG.interactable = false;

            // 2. Dim Backdrop
            GameObject dimGO = GetOrCreateUIChild(modalGO.transform, "DimBackdrop");
            var dimRT = dimGO.GetComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.sizeDelta = Vector2.zero;
            dimRT.anchoredPosition = Vector2.zero;

            var dimImg = GetOrAddComponent<Image>(dimGO);
            dimImg.color = new Color(0f, 0f, 0f, 0.72f);
            dimImg.raycastTarget = true;

            // 3. Container
            GameObject containerGO = GetOrCreateUIChild(modalGO.transform, "Container");
            var containerRT = containerGO.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0.5f);
            containerRT.anchorMax = new Vector2(0.5f, 0.5f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);
            containerRT.anchoredPosition = Vector2.zero;
            containerRT.sizeDelta = new Vector2(1016f, 410f);

            // 4. LEFT PANEL: SAMPLES (Width 640, Position -175 -> Gap 20px to Right Panel)
            GameObject leftPanelGO = GetOrCreateUIChild(containerGO.transform, "LeftPanel_Samples");
            var lpRT = leftPanelGO.GetComponent<RectTransform>();
            lpRT.anchorMin = new Vector2(0.5f, 0.5f);
            lpRT.anchorMax = new Vector2(0.5f, 0.5f);
            lpRT.pivot = new Vector2(0.5f, 0.5f);
            lpRT.anchoredPosition = new Vector2(-175f, 0f);
            lpRT.sizeDelta = new Vector2(640f, 410f);

            var lpImg = GetOrAddComponent<Image>(leftPanelGO);
            lpImg.sprite = invBgSprite;
            lpImg.type = Image.Type.Sliced;
            lpImg.color = Color.white;

            // Header Pill "SAMPLES"
            GameObject pillGO = GetOrCreateUIChild(leftPanelGO.transform, "HeaderPill_Samples");
            var pillRT = pillGO.GetComponent<RectTransform>();
            pillRT.anchorMin = new Vector2(0f, 1f);
            pillRT.anchorMax = new Vector2(0f, 1f);
            pillRT.pivot = new Vector2(0.5f, 0.5f);
            pillRT.anchoredPosition = new Vector2(85f, 8f);
            pillRT.sizeDelta = new Vector2(130f, 42f);

            var pillImg = GetOrAddComponent<Image>(pillGO);
            pillImg.sprite = sampleBadgeSprite;
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
            ptTMP.text = "SAMPLES";

            // Slots Grid
            GameObject gridGO = GetOrCreateUIChild(leftPanelGO.transform, "SlotsGrid");
            var gridRT = gridGO.GetComponent<RectTransform>();
            gridRT.anchorMin = Vector2.zero;
            gridRT.anchorMax = Vector2.one;
            gridRT.sizeDelta = Vector2.zero;
            gridRT.anchoredPosition = Vector2.zero;

            // 12 Slots: 4 cols x 3 rows
            float[] colX = new float[] { -225f, -75f, 75f, 225f };
            float[] rowY = new float[] { 98f, -4f, -106f };

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int index = r * 4 + c;
                    GameObject slotGO = GetOrCreateUIChild(gridGO.transform, "Slot_" + index);
                    var sRT = slotGO.GetComponent<RectTransform>();
                    sRT.anchorMin = new Vector2(0.5f, 0.5f);
                    sRT.anchorMax = new Vector2(0.5f, 0.5f);
                    sRT.pivot = new Vector2(0.5f, 0.5f);
                    sRT.anchoredPosition = new Vector2(colX[c], rowY[r]);
                    sRT.sizeDelta = new Vector2(136f, 90f);

                    var sImg = GetOrAddComponent<Image>(slotGO);
                    sImg.sprite = windowObjectSprite;
                    sImg.type = Image.Type.Sliced;
                    sImg.color = Color.white;

                    var sBtn = GetOrAddComponent<Button>(slotGO);
                    ColorBlock cb = sBtn.colors;
                    cb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
                    cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                    sBtn.colors = cb;

                    // Selected Highlight
                    GameObject hlGO = GetOrCreateUIChild(slotGO.transform, "SelectedHighlight");
                    var hlRT = hlGO.GetComponent<RectTransform>();
                    hlRT.anchorMin = Vector2.zero;
                    hlRT.anchorMax = Vector2.one;
                    hlRT.sizeDelta = Vector2.zero;
                    hlRT.anchoredPosition = Vector2.zero;

                    var hlImg = GetOrAddComponent<Image>(hlGO);
                    hlImg.sprite = windowObjectSprite;
                    hlImg.type = Image.Type.Sliced;
                    hlImg.color = new Color(1.0f, 0.88f, 0.55f, 0.95f);
                    hlImg.raycastTarget = false;
                    hlGO.SetActive(false);

                    // Item Icon
                    GameObject iconGO = GetOrCreateUIChild(slotGO.transform, "ItemIcon");
                    var iRT = iconGO.GetComponent<RectTransform>();
                    iRT.anchorMin = new Vector2(0.5f, 0.5f);
                    iRT.anchorMax = new Vector2(0.5f, 0.5f);
                    iRT.pivot = new Vector2(0.5f, 0.5f);
                    iRT.anchoredPosition = Vector2.zero;
                    iRT.sizeDelta = new Vector2(76f, 76f);

                    var iconImg = GetOrAddComponent<Image>(iconGO);
                    iconImg.preserveAspect = true;
                    iconImg.raycastTarget = false;
                    iconGO.SetActive(false);
                }
            }

            // 5. RIGHT PANEL: INFOS ABOUT CHOSED OBJECT (Width 340, Position 335)
            GameObject rightPanelGO = GetOrCreateUIChild(containerGO.transform, "RightPanel_Infos");
            var rpRT = rightPanelGO.GetComponent<RectTransform>();
            rpRT.anchorMin = new Vector2(0.5f, 0.5f);
            rpRT.anchorMax = new Vector2(0.5f, 0.5f);
            rpRT.pivot = new Vector2(0.5f, 0.5f);
            rpRT.anchoredPosition = new Vector2(335f, 0f);
            rpRT.sizeDelta = new Vector2(340f, 410f);

            var rpImg = GetOrAddComponent<Image>(rightPanelGO);
            rpImg.sprite = infosBgSprite;
            rpImg.type = Image.Type.Sliced;
            rpImg.color = Color.white;

            // Close Button 'X' (symmetric to top pill)
            GameObject closeGO = GetOrCreateUIChild(rightPanelGO.transform, "CloseButton");
            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 1f);
            closeRT.anchorMax = new Vector2(1f, 1f);
            closeRT.pivot = new Vector2(0.5f, 0.5f);
            closeRT.anchoredPosition = new Vector2(-15f, 8f);
            closeRT.sizeDelta = new Vector2(44f, 44f);

            var closeImg = GetOrAddComponent<Image>(closeGO);
            closeImg.sprite = radioBtnSprite;
            closeImg.type = Image.Type.Simple;
            closeImg.color = Color.white;

            var closeBtn = GetOrAddComponent<Button>(closeGO);

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

            // Item Header (Thumbnail Box + Title + Subtitle)
            GameObject headerGO = GetOrCreateUIChild(rightPanelGO.transform, "ItemHeader");
            var hRT = headerGO.GetComponent<RectTransform>();
            hRT.anchorMin = new Vector2(0f, 0.5f);
            hRT.anchorMax = new Vector2(1f, 0.5f);
            hRT.pivot = new Vector2(0.5f, 0.5f);
            hRT.anchoredPosition = new Vector2(0f, 128f);
            hRT.sizeDelta = new Vector2(0f, 80f);

            // Thumbnail Card
            GameObject cardBgGO = GetOrCreateUIChild(headerGO.transform, "ThumbnailCard");
            var cbgRT = cardBgGO.GetComponent<RectTransform>();
            cbgRT.anchorMin = new Vector2(0f, 0.5f);
            cbgRT.anchorMax = new Vector2(0f, 0.5f);
            cbgRT.pivot = new Vector2(0.5f, 0.5f);
            cbgRT.anchoredPosition = new Vector2(58f, 0f);
            cbgRT.sizeDelta = new Vector2(70f, 70f);

            var cbgImg = GetOrAddComponent<Image>(cardBgGO);
            cbgImg.sprite = chosedBgSprite;
            cbgImg.type = Image.Type.Simple;
            cbgImg.color = Color.white;

            GameObject thumbIconGO = GetOrCreateUIChild(cardBgGO.transform, "ThumbnailIcon");
            var tiRT = thumbIconGO.GetComponent<RectTransform>();
            tiRT.anchorMin = new Vector2(0.5f, 0.5f);
            tiRT.anchorMax = new Vector2(0.5f, 0.5f);
            tiRT.pivot = new Vector2(0.5f, 0.5f);
            tiRT.anchoredPosition = Vector2.zero;
            tiRT.sizeDelta = new Vector2(56f, 56f);

            var tiImg = GetOrAddComponent<Image>(thumbIconGO);
            tiImg.preserveAspect = true;
            tiImg.raycastTarget = false;

            // Title Text
            GameObject titleGO = GetOrCreateUIChild(headerGO.transform, "TitleText");
            var titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 0.5f);
            titleRT.anchorMax = new Vector2(0f, 0.5f);
            titleRT.pivot = new Vector2(0f, 0.5f);
            titleRT.anchoredPosition = new Vector2(105f, 14f);
            titleRT.sizeDelta = new Vector2(230f, 32f);

            var titleTMP = GetOrAddComponent<TextMeshProUGUI>(titleGO);
            titleTMP.font = font;
            titleTMP.fontSize = 24f;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.color = new Color(0.98f, 0.96f, 0.92f, 1f);
            titleTMP.alignment = TextAlignmentOptions.Left;
            titleTMP.text = "BASALT";

            // Subtitle Text
            GameObject subGO = GetOrCreateUIChild(headerGO.transform, "SubtitleText");
            var subRT = subGO.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0f, 0.5f);
            subRT.anchorMax = new Vector2(0f, 0.5f);
            subRT.pivot = new Vector2(0f, 0.5f);
            subRT.anchoredPosition = new Vector2(106f, -14f);
            subRT.sizeDelta = new Vector2(230f, 20f);

            var subTMP = GetOrAddComponent<TextMeshProUGUI>(subGO);
            subTMP.font = font;
            subTMP.fontSize = 11.5f;
            subTMP.fontStyle = FontStyles.Normal;
            subTMP.color = new Color(0.76f, 0.64f, 0.47f, 1f);
            subTMP.alignment = TextAlignmentOptions.Left;
            subTMP.text = "THE GAME PRIMARY UNDERGROUND";

            // Stats (Hardness + Rarity)
            GameObject statsGO = GetOrCreateUIChild(rightPanelGO.transform, "Stats");
            var stRT = statsGO.GetComponent<RectTransform>();
            stRT.anchorMin = new Vector2(0f, 0.5f);
            stRT.anchorMax = new Vector2(1f, 0.5f);
            stRT.pivot = new Vector2(0.5f, 0.5f);
            stRT.anchoredPosition = new Vector2(0f, 30f);
            stRT.sizeDelta = new Vector2(0f, 100f);

            // HARDNESS
            GameObject hlLabelGO = GetOrCreateUIChild(statsGO.transform, "HardnessLabel");
            var hllRT = hlLabelGO.GetComponent<RectTransform>();
            hllRT.anchorMin = new Vector2(0f, 0.5f);
            hllRT.anchorMax = new Vector2(0f, 0.5f);
            hllRT.pivot = new Vector2(0f, 0.5f);
            hllRT.anchoredPosition = new Vector2(25f, 38f);
            hllRT.sizeDelta = new Vector2(200f, 18f);

            var hlTMP = GetOrAddComponent<TextMeshProUGUI>(hlLabelGO);
            hlTMP.font = font;
            hlTMP.fontSize = 13f;
            hlTMP.fontStyle = FontStyles.Normal;
            hlTMP.color = new Color(0.84f, 0.77f, 0.68f, 1f);
            hlTMP.alignment = TextAlignmentOptions.Left;
            hlTMP.text = "HARDNESS";

            GameObject hgGO = GetOrCreateUIChild(statsGO.transform, "HardnessBar_Groove");
            var hgRT = hgGO.GetComponent<RectTransform>();
            hgRT.anchorMin = new Vector2(0.5f, 0.5f);
            hgRT.anchorMax = new Vector2(0.5f, 0.5f);
            hgRT.pivot = new Vector2(0.5f, 0.5f);
            hgRT.anchoredPosition = new Vector2(0f, 18f);
            hgRT.sizeDelta = new Vector2(300f, 16f);

            var hgImg = GetOrAddComponent<Image>(hgGO);
            hgImg.sprite = fillBorderSprite;
            hgImg.type = Image.Type.Sliced;
            hgImg.color = Color.white;

            GameObject hfGO = GetOrCreateUIChild(hgGO.transform, "Fill");
            var hfRT = hfGO.GetComponent<RectTransform>();
            hfRT.anchorMin = Vector2.zero;
            hfRT.anchorMax = Vector2.one;
            hfRT.sizeDelta = new Vector2(-4f, -4f);
            hfRT.anchoredPosition = Vector2.zero;

            var hfImg = GetOrAddComponent<Image>(hfGO);
            hfImg.sprite = fillSprite;
            hfImg.type = Image.Type.Filled;
            hfImg.fillMethod = Image.FillMethod.Horizontal;
            hfImg.fillAmount = 0.85f;
            hfImg.color = new Color(0.88f, 0.52f, 0.23f, 1f);

            // RARITY
            GameObject rlLabelGO = GetOrCreateUIChild(statsGO.transform, "RarityLabel");
            var rllRT = rlLabelGO.GetComponent<RectTransform>();
            rllRT.anchorMin = new Vector2(0f, 0.5f);
            rllRT.anchorMax = new Vector2(0f, 0.5f);
            rllRT.pivot = new Vector2(0f, 0.5f);
            rllRT.anchoredPosition = new Vector2(25f, -8f);
            rllRT.sizeDelta = new Vector2(200f, 18f);

            var rlTMP = GetOrAddComponent<TextMeshProUGUI>(rlLabelGO);
            rlTMP.font = font;
            rlTMP.fontSize = 13f;
            rlTMP.fontStyle = FontStyles.Normal;
            rlTMP.color = new Color(0.84f, 0.77f, 0.68f, 1f);
            rlTMP.alignment = TextAlignmentOptions.Left;
            rlTMP.text = "RARITY";

            GameObject rgGO = GetOrCreateUIChild(statsGO.transform, "RarityBar_Groove");
            var rgRT = rgGO.GetComponent<RectTransform>();
            rgRT.anchorMin = new Vector2(0.5f, 0.5f);
            rgRT.anchorMax = new Vector2(0.5f, 0.5f);
            rgRT.pivot = new Vector2(0.5f, 0.5f);
            rgRT.anchoredPosition = new Vector2(0f, -28f);
            rgRT.sizeDelta = new Vector2(300f, 16f);

            var rgImg = GetOrAddComponent<Image>(rgGO);
            rgImg.sprite = fillBorderSprite;
            rgImg.type = Image.Type.Sliced;
            rgImg.color = Color.white;

            GameObject rfGO = GetOrCreateUIChild(rgGO.transform, "Fill");
            var rfRT = rfGO.GetComponent<RectTransform>();
            rfRT.anchorMin = Vector2.zero;
            rfRT.anchorMax = Vector2.one;
            rfRT.sizeDelta = new Vector2(-4f, -4f);
            rfRT.anchoredPosition = Vector2.zero;

            var rfImg = GetOrAddComponent<Image>(rfGO);
            rfImg.sprite = fillSprite;
            rfImg.type = Image.Type.Filled;
            rfImg.fillMethod = Image.FillMethod.Horizontal;
            rfImg.fillAmount = 0.60f;
            rfImg.color = new Color(0.88f, 0.52f, 0.23f, 1f);

            // Description Text
            GameObject descGO = GetOrCreateUIChild(rightPanelGO.transform, "DescriptionText");
            var descRT = descGO.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.5f, 0.5f);
            descRT.anchorMax = new Vector2(0.5f, 0.5f);
            descRT.pivot = new Vector2(0.5f, 0.5f);
            descRT.anchoredPosition = new Vector2(0f, -55f);
            descRT.sizeDelta = new Vector2(300f, 75f);

            var descTMP = GetOrAddComponent<TextMeshProUGUI>(descGO);
            descTMP.font = font;
            descTMP.fontSize = 14f;
            descTMP.fontStyle = FontStyles.Normal;
            descTMP.color = new Color(0.85f, 0.79f, 0.71f, 1f);
            descTMP.lineSpacing = 12f;
            descTMP.enableWordWrapping = true;
            descTMP.alignment = TextAlignmentOptions.TopLeft;
            descTMP.text = "Dark volcanic rock. Used as forge flux and for heavy tools.";

            // INSPECT Button
            GameObject inspectGO = GetOrCreateUIChild(rightPanelGO.transform, "InspectButton");
            var insRT = inspectGO.GetComponent<RectTransform>();
            insRT.anchorMin = new Vector2(0.5f, 0.5f);
            insRT.anchorMax = new Vector2(0.5f, 0.5f);
            insRT.pivot = new Vector2(0.5f, 0.5f);
            insRT.anchoredPosition = new Vector2(0f, -145f);
            insRT.sizeDelta = new Vector2(260f, 50f);

            var insImg = GetOrAddComponent<Image>(inspectGO);
            insImg.sprite = inspectBtnSprite;
            insImg.type = Image.Type.Sliced;
            insImg.color = Color.white;

            var insBtn = GetOrAddComponent<Button>(inspectGO);
            ColorBlock icb = insBtn.colors;
            icb.highlightedColor = new Color(1.2f, 1.2f, 1.2f, 1f);
            icb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            insBtn.colors = icb;

            GameObject insTextGO = GetOrCreateUIChild(inspectGO.transform, "Text");
            var itRT = insTextGO.GetComponent<RectTransform>();
            itRT.anchorMin = Vector2.zero;
            itRT.anchorMax = Vector2.one;
            itRT.sizeDelta = Vector2.zero;
            itRT.anchoredPosition = Vector2.zero;

            var itTMP = GetOrAddComponent<TextMeshProUGUI>(insTextGO);
            itTMP.font = font;
            itTMP.fontSize = 22f;
            itTMP.fontStyle = FontStyles.Bold;
            itTMP.color = new Color(0.98f, 0.96f, 0.88f, 1f);
            itTMP.alignment = TextAlignmentOptions.Center;
            itTMP.text = "INSPECT";

            // Attach & AutoWire Controller
            var samplesModal = GetOrAddComponent<JbelSamplesInventoryModal>(modalGO);
            samplesModal.AutoWire();

            // Register buttons to GameAudioManager
            if (JbelIghoud.Audio.GameAudioManager.Instance != null)
            {
                JbelIghoud.Audio.GameAudioManager.Instance.RegisterAllButtonsInScene();
            }

            modalGO.SetActive(false);

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("<color=green><b>[Jbel Inventory Builder]</b> Successfully built and integrated Samples Inventory Modal!</color>");
        }
    }
}
