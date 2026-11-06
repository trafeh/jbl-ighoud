using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using System.Collections.Generic;

namespace JbelIghoud.UI.Editor
{
    public static class StartMenuSceneBuilderEditor
    {
        private const string LOADING_SCENE_PATH = "Assets/Scenes/LoadingScene.unity";

        [MenuItem("Tools/Jbel Ighoud/Build Full Startup & Menu Scene")]
        public static void BuildFullStartupScene()
        {
            Debug.Log("[StartMenuSceneBuilder] Constructing complete Startup & Menu Scene...");

            // 1. Create clean scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Camera
            GameObject camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            Camera cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.07f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGO.transform.position = new Vector3(0, 0, -10);

            // 3. EventSystem
            GameObject esGO = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // 4. Canvas setup (ScreenSpaceCamera)
            GameObject canvasGO = new GameObject("LoadingCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Load Fonts & Sprites & Materials
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IM_FELL_English_SC SDF.asset");
            if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

            Sprite sBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading bg.png");
            Sprite sArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_artwork.png");
            Sprite sTrack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_bar_track.png");
            Sprite sFill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_bar_fill.png");

            Sprite sPillBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/btn_pill_bg.png");
            Sprite sPillStroke = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/btn_pill_stroke.png");
            Sprite sSplitBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/btn_split_bg.png");
            Sprite sSplitStroke = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/btn_split_stroke.png");
            Sprite sArBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/btn_ar_bg.png");

            Sprite sIconHand = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/icon_hand.png");
            Sprite sIconSpears = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/icon_spears.png");
            Sprite sIconGear = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/menu/icon_gear.png");

            Material matSolarFlare = AssetDatabase.LoadAssetAtPath<Material>("Assets/Shaders/M_SolarFlareStroke.mat");

            // Attach Master Controller to Canvas
            JbelStartMenuController controller = canvasGO.AddComponent<JbelStartMenuController>();

            // 5. Fullscreen Background
            GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtBg = bgGO.GetComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.offsetMin = Vector2.zero;
            rtBg.offsetMax = Vector2.zero;
            Image imgBg = bgGO.GetComponent<Image>();
            imgBg.sprite = sBg;
            imgBg.color = Color.white;

            // Vignette Tint for readability
            GameObject tintGO = new GameObject("VignetteTint", typeof(RectTransform), typeof(Image));
            tintGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtTint = tintGO.GetComponent<RectTransform>();
            rtTint.anchorMin = Vector2.zero;
            rtTint.anchorMax = Vector2.one;
            rtTint.offsetMin = Vector2.zero;
            rtTint.offsetMax = Vector2.zero;
            Image imgTint = tintGO.GetComponent<Image>();
            imgTint.color = new Color(0f, 0f, 0f, 0.18f);

            // 6. Center Artwork (Golden Handprint & Jbel Ighoud Title)
            GameObject artGO = new GameObject("CenterArtwork", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            artGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtArt = artGO.GetComponent<RectTransform>();
            rtArt.anchorMin = new Vector2(0.5f, 0.5f);
            rtArt.anchorMax = new Vector2(0.5f, 0.5f);
            rtArt.pivot = new Vector2(0.5f, 0.5f);
            rtArt.anchoredPosition = new Vector2(0f, 100f);
            rtArt.sizeDelta = new Vector2(580f, 435f);
            Image imgArt = artGO.GetComponent<Image>();
            imgArt.sprite = sArt;
            imgArt.preserveAspect = true;
            imgArt.color = Color.white;
            CanvasGroup cgArt = artGO.GetComponent<CanvasGroup>();

            // Shadow / contrast underlay for artwork
            GameObject artShadowGO = new GameObject("ArtworkShadowUnderlay", typeof(RectTransform), typeof(Image));
            artShadowGO.transform.SetParent(artGO.transform, false);
            artShadowGO.transform.SetAsFirstSibling();
            RectTransform rtArtShadow = artShadowGO.GetComponent<RectTransform>();
            rtArtShadow.anchorMin = Vector2.zero;
            rtArtShadow.anchorMax = Vector2.one;
            rtArtShadow.offsetMin = new Vector2(-4f, -4f);
            rtArtShadow.offsetMax = new Vector2(4f, 4f);
            Image imgArtShadow = artShadowGO.GetComponent<Image>();
            imgArtShadow.sprite = sArt;
            imgArtShadow.preserveAspect = true;
            imgArtShadow.color = new Color(0.12f, 0.06f, 0.02f, 0.65f); // Rich warm ambient occlusion

            // 7. LoadingScreen_Panel (Track + Fill + Percentage + Rotating Tips)
            GameObject loadPanelGO = new GameObject("LoadingScreen_Panel", typeof(RectTransform), typeof(CanvasGroup));
            loadPanelGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtLoadPanel = loadPanelGO.GetComponent<RectTransform>();
            rtLoadPanel.anchorMin = Vector2.zero;
            rtLoadPanel.anchorMax = Vector2.one;
            rtLoadPanel.offsetMin = Vector2.zero;
            rtLoadPanel.offsetMax = Vector2.zero;
            CanvasGroup cgLoad = loadPanelGO.GetComponent<CanvasGroup>();

            // Progress Bar Track
            GameObject trackGO = new GameObject("ProgressBar_Track", typeof(RectTransform), typeof(Image));
            trackGO.transform.SetParent(rtLoadPanel, false);
            RectTransform rtTrack = trackGO.GetComponent<RectTransform>();
            rtTrack.anchorMin = new Vector2(0.5f, 0.5f);
            rtTrack.anchorMax = new Vector2(0.5f, 0.5f);
            rtTrack.pivot = new Vector2(0.5f, 0.5f);
            rtTrack.anchoredPosition = new Vector2(0f, -170f);
            rtTrack.sizeDelta = new Vector2(740f, 36f);
            Image imgTrack = trackGO.GetComponent<Image>();
            imgTrack.sprite = sTrack;
            imgTrack.preserveAspect = true;

            // Progress Bar Fill
            GameObject fillGO = new GameObject("ProgressBar_Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(rtTrack, false);
            RectTransform rtFill = fillGO.GetComponent<RectTransform>();
            rtFill.anchorMin = Vector2.zero;
            rtFill.anchorMax = Vector2.one;
            rtFill.offsetMin = new Vector2(6f, 5f);
            rtFill.offsetMax = new Vector2(-6f, -5f);
            Image imgFill = fillGO.GetComponent<Image>();
            imgFill.sprite = sFill;
            imgFill.type = Image.Type.Filled;
            imgFill.fillMethod = Image.FillMethod.Horizontal;
            imgFill.fillOrigin = 0;
            imgFill.fillAmount = 0f;

            // Progress Percent Text
            GameObject pctGO = new GameObject("ProgressPercentText", typeof(RectTransform), typeof(TextMeshProUGUI));
            pctGO.transform.SetParent(rtTrack, false);
            RectTransform rtPct = pctGO.GetComponent<RectTransform>();
            rtPct.anchorMin = Vector2.zero;
            rtPct.anchorMax = Vector2.one;
            rtPct.sizeDelta = Vector2.zero;
            TextMeshProUGUI tmpPct = pctGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpPct.font = font;
            tmpPct.text = "0%";
            tmpPct.fontSize = 17;
            tmpPct.fontStyle = FontStyles.Bold;
            tmpPct.alignment = TextAlignmentOptions.Center;
            tmpPct.color = new Color(0.976f, 0.945f, 0.875f);

            // Dynamic Tip Text
            GameObject tipGO = new GameObject("DynamicTip", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            tipGO.transform.SetParent(rtLoadPanel, false);
            RectTransform rtTip = tipGO.GetComponent<RectTransform>();
            rtTip.anchorMin = new Vector2(0.5f, 0.5f);
            rtTip.anchorMax = new Vector2(0.5f, 0.5f);
            rtTip.pivot = new Vector2(0.5f, 0.5f);
            rtTip.anchoredPosition = new Vector2(0f, -225f);
            rtTip.sizeDelta = new Vector2(900f, 50f);
            TextMeshProUGUI tmpTip = tipGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpTip.font = font;
            tmpTip.text = "TIPS: HOLD RUN BUTTON TO SPRINT ACROSS THE TERRAIN.";
            tmpTip.fontSize = 17;
            tmpTip.fontStyle = FontStyles.Bold;
            tmpTip.characterSpacing = 3;
            tmpTip.alignment = TextAlignmentOptions.Center;
            tmpTip.color = new Color(0.925f, 0.886f, 0.808f);
            CanvasGroup cgTip = tipGO.GetComponent<CanvasGroup>();

            // 8. Flare Flash Overlay (Golden-White solar dive flare)
            GameObject flashGO = new GameObject("FlareFlash_Overlay", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            flashGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtFlash = flashGO.GetComponent<RectTransform>();
            rtFlash.anchorMin = Vector2.zero;
            rtFlash.anchorMax = Vector2.one;
            rtFlash.offsetMin = Vector2.zero;
            rtFlash.offsetMax = Vector2.zero;
            Image imgFlash = flashGO.GetComponent<Image>();
            imgFlash.color = new Color(1.0f, 0.93f, 0.78f, 1f); // Warm solar flare white-gold
            CanvasGroup cgFlash = flashGO.GetComponent<CanvasGroup>();
            cgFlash.alpha = 0f;
            flashGO.SetActive(false);

            // 9. StartGameMenu_Panel (Main Menu with Solar Flare Buttons)
            GameObject menuPanelGO = new GameObject("StartGameMenu_Panel", typeof(RectTransform), typeof(CanvasGroup));
            menuPanelGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtMenuPanel = menuPanelGO.GetComponent<RectTransform>();
            rtMenuPanel.anchorMin = Vector2.zero;
            rtMenuPanel.anchorMax = Vector2.one;
            rtMenuPanel.offsetMin = Vector2.zero;
            rtMenuPanel.offsetMax = Vector2.zero;
            CanvasGroup cgMenu = menuPanelGO.GetComponent<CanvasGroup>();
            cgMenu.alpha = 0f;
            menuPanelGO.SetActive(false);

            // Buttons Container on the Left
            GameObject btnsContainerGO = new GameObject("MenuButtonsContainer", typeof(RectTransform));
            btnsContainerGO.transform.SetParent(rtMenuPanel, false);
            RectTransform rtBtnsContainer = btnsContainerGO.GetComponent<RectTransform>();
            rtBtnsContainer.anchorMin = new Vector2(0f, 0.5f);
            rtBtnsContainer.anchorMax = new Vector2(0f, 0.5f);
            rtBtnsContainer.pivot = new Vector2(0f, 0.5f);
            rtBtnsContainer.anchoredPosition = new Vector2(70f, -50f);
            rtBtnsContainer.sizeDelta = new Vector2(530f, 350f);

            // Button 1: CONTINUE
            Button btnContinue = CreateMenuButton(
                btnsContainerGO.transform,
                "ContinueButton",
                new Vector2(0f, 110f),
                new Vector2(520f, 94f),
                sPillBg,
                sPillStroke,
                matSolarFlare,
                sIconHand,
                new Vector2(48f, 56f),
                new Vector2(-170f, 0f),
                "CONTINUE",
                font,
                24,
                new Vector2(25f, 0f)
            );

            // Button 2: MODE SURVIE
            Button btnModeSurvie = CreateMenuButton(
                btnsContainerGO.transform,
                "ModeSurvieButton",
                new Vector2(0f, 0f),
                new Vector2(520f, 94f),
                sPillBg,
                sPillStroke,
                matSolarFlare,
                sIconSpears,
                new Vector2(52f, 52f),
                new Vector2(-170f, 0f),
                "MODE SURVIE",
                font,
                24,
                new Vector2(25f, 0f)
            );

            // Sub Buttons Row: OPTION & CREDITS
            GameObject subRowGO = new GameObject("SubButtonsRow", typeof(RectTransform));
            subRowGO.transform.SetParent(btnsContainerGO.transform, false);
            RectTransform rtSubRow = subRowGO.GetComponent<RectTransform>();
            rtSubRow.anchorMin = new Vector2(0.5f, 0.5f);
            rtSubRow.anchorMax = new Vector2(0.5f, 0.5f);
            rtSubRow.pivot = new Vector2(0.5f, 0.5f);
            rtSubRow.anchoredPosition = new Vector2(0f, -110f);
            rtSubRow.sizeDelta = new Vector2(520f, 94f);

            // Option Button
            Button btnOption = CreateMenuButton(
                subRowGO.transform,
                "OptionButton",
                new Vector2(-135f, 0f),
                new Vector2(245f, 94f),
                sSplitBg,
                sSplitStroke,
                matSolarFlare,
                sIconGear,
                new Vector2(44f, 44f),
                new Vector2(-60f, 0f),
                "OPTION",
                font,
                22,
                new Vector2(25f, 0f)
            );

            // Credits Button
            Button btnCredits = CreateMenuButton(
                subRowGO.transform,
                "CreditsButton",
                new Vector2(135f, 0f),
                new Vector2(245f, 94f),
                sSplitBg,
                sSplitStroke,
                matSolarFlare,
                null,
                Vector2.zero,
                Vector2.zero,
                "CREDITS",
                font,
                22,
                Vector2.zero
            );

            // AR Badge in Top-Right
            GameObject arGO = new GameObject("AR_Badge", typeof(RectTransform), typeof(Image), typeof(Button));
            arGO.transform.SetParent(rtMenuPanel, false);
            RectTransform rtAr = arGO.GetComponent<RectTransform>();
            rtAr.anchorMin = new Vector2(1f, 1f);
            rtAr.anchorMax = new Vector2(1f, 1f);
            rtAr.pivot = new Vector2(1f, 1f);
            rtAr.anchoredPosition = new Vector2(-60f, -50f);
            rtAr.sizeDelta = new Vector2(105f, 105f);
            Image imgAr = arGO.GetComponent<Image>();
            imgAr.sprite = sArBg;
            imgAr.preserveAspect = true;
            Button btnAr = arGO.GetComponent<Button>();

            // AR Text
            GameObject arTxtGO = new GameObject("AR_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            arTxtGO.transform.SetParent(rtAr, false);
            RectTransform rtArTxt = arTxtGO.GetComponent<RectTransform>();
            rtArTxt.anchorMin = Vector2.zero;
            rtArTxt.anchorMax = Vector2.one;
            rtArTxt.offsetMin = Vector2.zero;
            rtArTxt.offsetMax = Vector2.zero;
            TextMeshProUGUI tmpAr = arTxtGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpAr.font = font;
            tmpAr.text = "AR";
            tmpAr.fontSize = 28;
            tmpAr.fontStyle = FontStyles.Bold;
            tmpAr.alignment = TextAlignmentOptions.Center;
            tmpAr.color = new Color(0.96f, 0.93f, 0.85f);

            // 10. Studio Logo (TecGaming) - Single clean line matching reference
            GameObject logoGO = new GameObject("StudioLogo", typeof(RectTransform), typeof(TextMeshProUGUI));
            logoGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtLogo = logoGO.GetComponent<RectTransform>();
            rtLogo.anchorMin = new Vector2(0f, 0f);
            rtLogo.anchorMax = new Vector2(0f, 0f);
            rtLogo.pivot = new Vector2(0f, 0f);
            rtLogo.anchoredPosition = new Vector2(60f, 40f);
            rtLogo.sizeDelta = new Vector2(200f, 40f);
            TextMeshProUGUI tmpLogo = logoGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpLogo.font = font;
            tmpLogo.text = "TecGaming";
            tmpLogo.fontSize = 24;
            tmpLogo.fontStyle = FontStyles.Bold;
            tmpLogo.color = new Color(0.88f, 0.84f, 0.74f);

            // 11. Game Version - Anchored Bottom-Right
            GameObject verGO = new GameObject("VersionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            verGO.transform.SetParent(canvasGO.transform, false);
            RectTransform rtVer = verGO.GetComponent<RectTransform>();
            rtVer.anchorMin = new Vector2(1f, 0f);
            rtVer.anchorMax = new Vector2(1f, 0f);
            rtVer.pivot = new Vector2(1f, 0f);
            rtVer.anchoredPosition = new Vector2(-60f, 42f);
            rtVer.sizeDelta = new Vector2(120f, 30f);
            TextMeshProUGUI tmpVer = verGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpVer.font = font;
            tmpVer.text = "v0.1.0";
            tmpVer.fontSize = 18;
            tmpVer.fontStyle = FontStyles.Normal;
            tmpVer.alignment = TextAlignmentOptions.Right;
            tmpVer.color = new Color(0.77f, 0.73f, 0.65f);

            // 12. Wire Serialized Fields on JbelStartMenuController
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("loadingPanelCG").objectReferenceValue = cgLoad;
            so.FindProperty("startMenuPanelCG").objectReferenceValue = cgMenu;
            so.FindProperty("flashOverlayCG").objectReferenceValue = cgFlash;
            so.FindProperty("progressBarFill").objectReferenceValue = imgFill;
            so.FindProperty("progressPercentText").objectReferenceValue = tmpPct;
            so.FindProperty("tipText").objectReferenceValue = tmpTip;
            so.FindProperty("tipCanvasGroup").objectReferenceValue = cgTip;
            so.FindProperty("artworkRT").objectReferenceValue = rtArt;
            so.FindProperty("artworkCG").objectReferenceValue = cgArt;
            so.FindProperty("menuButtonsContainerRT").objectReferenceValue = rtBtnsContainer;
            so.FindProperty("continueButton").objectReferenceValue = btnContinue;
            so.FindProperty("modeSurvieButton").objectReferenceValue = btnModeSurvie;
            so.FindProperty("optionButton").objectReferenceValue = btnOption;
            so.FindProperty("creditsButton").objectReferenceValue = btnCredits;
            so.FindProperty("arButton").objectReferenceValue = btnAr;
            so.FindProperty("targetSceneName").stringValue = "SampleScene";
            so.FindProperty("minimumLoadingTime").floatValue = 2.5f;
            so.FindProperty("diveDuration").floatValue = 0.45f;
            so.FindProperty("maxDiveScale").floatValue = 18f;
            so.ApplyModifiedProperties();

            // Save Scene
            EditorSceneManager.SaveScene(newScene, LOADING_SCENE_PATH);
            Debug.Log("<color=green><b>[StartMenuSceneBuilder]</b> Startup & Menu Scene rebuilt and saved successfully to: " + LOADING_SCENE_PATH + "</color>");

            ConfigureBuildSettings();
        }

        private static Button CreateMenuButton(
            Transform parent,
            string name,
            Vector2 pos,
            Vector2 size,
            Sprite bgSprite,
            Sprite strokeSprite,
            Material strokeMat,
            Sprite iconSprite,
            Vector2 iconSize,
            Vector2 iconOffset,
            string labelText,
            TMP_FontAsset font,
            float fontSize,
            Vector2 textOffset)
        {
            GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);
            RectTransform rtBtn = btnGO.GetComponent<RectTransform>();
            rtBtn.anchorMin = new Vector2(0.5f, 0.5f);
            rtBtn.anchorMax = new Vector2(0.5f, 0.5f);
            rtBtn.pivot = new Vector2(0.5f, 0.5f);
            rtBtn.anchoredPosition = pos;
            rtBtn.sizeDelta = size;

            Image imgBg = btnGO.GetComponent<Image>();
            imgBg.sprite = bgSprite;
            imgBg.color = Color.white;

            Button btn = btnGO.GetComponent<Button>();

            // Solar Flare Stroke Child
            GameObject strokeGO = new GameObject("SolarFlareStroke", typeof(RectTransform), typeof(Image));
            strokeGO.transform.SetParent(btnGO.transform, false);
            RectTransform rtStroke = strokeGO.GetComponent<RectTransform>();
            rtStroke.anchorMin = Vector2.zero;
            rtStroke.anchorMax = Vector2.one;
            rtStroke.offsetMin = Vector2.zero;
            rtStroke.offsetMax = Vector2.zero;
            Image imgStroke = strokeGO.GetComponent<Image>();
            imgStroke.sprite = strokeSprite;
            if (strokeMat != null) imgStroke.material = strokeMat;
            imgStroke.raycastTarget = false;

            // Icon (optional)
            if (iconSprite != null)
            {
                GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGO.transform.SetParent(btnGO.transform, false);
                RectTransform rtIcon = iconGO.GetComponent<RectTransform>();
                rtIcon.anchorMin = new Vector2(0.5f, 0.5f);
                rtIcon.anchorMax = new Vector2(0.5f, 0.5f);
                rtIcon.pivot = new Vector2(0.5f, 0.5f);
                rtIcon.anchoredPosition = iconOffset;
                rtIcon.sizeDelta = iconSize;
                Image imgIcon = iconGO.GetComponent<Image>();
                imgIcon.sprite = iconSprite;
                imgIcon.preserveAspect = true;
                imgIcon.color = new Color(0.92f, 0.88f, 0.78f);
                imgIcon.raycastTarget = false;
            }

            // Text Label
            GameObject txtGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGO.transform.SetParent(btnGO.transform, false);
            RectTransform rtTxt = txtGO.GetComponent<RectTransform>();
            rtTxt.anchorMin = Vector2.zero;
            rtTxt.anchorMax = Vector2.one;
            rtTxt.offsetMin = new Vector2(textOffset.x, textOffset.y);
            rtTxt.offsetMax = new Vector2(textOffset.x, textOffset.y);
            TextMeshProUGUI tmp = txtGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = labelText;
            tmp.fontSize = fontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.91f, 0.87f, 0.77f);
            tmp.raycastTarget = false;

            return btn;
        }

        [MenuItem("Tools/Jbel Ighoud/Configure Build Settings")]
        public static void ConfigureBuildSettings()
        {
            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(LOADING_SCENE_PATH, true),
                new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true)
            };

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log("<color=green><b>[StartMenuSceneBuilder]</b> Build Settings: Scene 0 = LoadingScene, Scene 1 = SampleScene</color>");
        }
    }
}
