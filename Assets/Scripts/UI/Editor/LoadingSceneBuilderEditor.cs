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
    public static class LoadingSceneBuilderEditor
    {
        private const string LOADING_SCENE_PATH = "Assets/Scenes/LoadingScene.unity";
        private const string SAMPLE_SCENE_PATH = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Tools/Jbel Ighoud/Build Dedicated Loading Scene")]
        public static void BuildDedicatedLoadingScene()
        {
            Debug.Log("[LoadingSceneBuilder] Building dedicated LoadingScene matching reference design at: " + LOADING_SCENE_PATH);

            // 1. Create a brand new clean scene
            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Main Camera
            GameObject camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGO.tag = "MainCamera";
            Camera cam = camGO.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.08f, 0.07f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGO.transform.position = new Vector3(0, 0, -10);

            // 3. EventSystem with InputSystemUIInputModule
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

            // Load assets
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/IM_FELL_English_SC SDF.asset");
            if (font == null) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Sprite sBg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading bg.png");
            Sprite sArt = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_artwork.png");
            Sprite sLogo = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/logo_tecgaming.png");
            Sprite sTrack = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_bar_track.png");
            Sprite sFill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/loading/loading_bar_fill.png");

            // 5. LoadingScreen_Panel root
            GameObject screenGO = new GameObject("LoadingScreen_Panel", typeof(RectTransform), typeof(CanvasGroup));
            screenGO.transform.SetParent(canvasGO.transform, false);

            RectTransform rtScreen = screenGO.GetComponent<RectTransform>();
            rtScreen.anchorMin = Vector2.zero;
            rtScreen.anchorMax = Vector2.one;
            rtScreen.offsetMin = Vector2.zero;
            rtScreen.offsetMax = Vector2.zero;

            CanvasGroup cg = screenGO.GetComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;

            LoadingScreenController ctrl = screenGO.AddComponent<LoadingScreenController>();

            // 6. Fullscreen Background Artwork (Sunset cave landscape)
            GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(rtScreen, false);
            RectTransform rtBg = bgGO.GetComponent<RectTransform>();
            rtBg.anchorMin = Vector2.zero;
            rtBg.anchorMax = Vector2.one;
            rtBg.offsetMin = Vector2.zero;
            rtBg.offsetMax = Vector2.zero;
            Image imgBg = bgGO.GetComponent<Image>();
            imgBg.sprite = sBg;
            imgBg.color = Color.white;

            // Subtle dark tint overlay for readability (matching ref vignette)
            GameObject tintGO = new GameObject("VignetteTint", typeof(RectTransform), typeof(Image));
            tintGO.transform.SetParent(rtScreen, false);
            RectTransform rtTint = tintGO.GetComponent<RectTransform>();
            rtTint.anchorMin = Vector2.zero;
            rtTint.anchorMax = Vector2.one;
            rtTint.offsetMin = Vector2.zero;
            rtTint.offsetMax = Vector2.zero;
            Image imgTint = tintGO.GetComponent<Image>();
            imgTint.color = new Color(0f, 0f, 0f, 0.18f);

            // 7. Center Artwork (Jbel Ighoud Cave Handprint)
            GameObject artGO = new GameObject("CenterArtwork", typeof(RectTransform), typeof(Image));
            artGO.transform.SetParent(rtScreen, false);
            RectTransform rtArt = artGO.GetComponent<RectTransform>();
            rtArt.anchorMin = new Vector2(0.5f, 0.5f);
            rtArt.anchorMax = new Vector2(0.5f, 0.5f);
            rtArt.pivot = new Vector2(0.5f, 0.5f);
            rtArt.anchoredPosition = new Vector2(0f, 100f);
            rtArt.sizeDelta = new Vector2(580f, 435f);
            Image imgArt = artGO.GetComponent<Image>();
            imgArt.sprite = sArt;
            imgArt.preserveAspect = true;

            // 8. Progress Bar Track & Fill (Directly beneath artwork)
            GameObject trackGO = new GameObject("ProgressBar_Track", typeof(RectTransform), typeof(Image));
            trackGO.transform.SetParent(rtScreen, false);
            RectTransform rtTrack = trackGO.GetComponent<RectTransform>();
            rtTrack.anchorMin = new Vector2(0.5f, 0.5f);
            rtTrack.anchorMax = new Vector2(0.5f, 0.5f);
            rtTrack.pivot = new Vector2(0.5f, 0.5f);
            rtTrack.anchoredPosition = new Vector2(0f, -170f);
            rtTrack.sizeDelta = new Vector2(740f, 36f);
            Image imgTrack = trackGO.GetComponent<Image>();
            imgTrack.sprite = sTrack;
            imgTrack.preserveAspect = true;

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

            // 9. Rotating Tips (Directly beneath progress bar)
            GameObject tipGO = new GameObject("DynamicTip", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(CanvasGroup));
            tipGO.transform.SetParent(rtScreen, false);
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
            tmpTip.textWrappingMode = TextWrappingModes.Normal;
            tmpTip.color = new Color(0.925f, 0.886f, 0.808f); // #ECE2CE
            CanvasGroup tipCg = tipGO.GetComponent<CanvasGroup>();

            // 10. Studio Logo (TecGame ING) - Anchored Bottom-Left
            GameObject logoGO = new GameObject("StudioLogo", typeof(RectTransform), typeof(Image));
            logoGO.transform.SetParent(rtScreen, false);
            RectTransform rtLogo = logoGO.GetComponent<RectTransform>();
            rtLogo.anchorMin = new Vector2(0f, 0f);
            rtLogo.anchorMax = new Vector2(0f, 0f);
            rtLogo.pivot = new Vector2(0f, 0f);
            rtLogo.anchoredPosition = new Vector2(55f, 35f);
            rtLogo.sizeDelta = new Vector2(130f, 66f);
            Image imgLogo = logoGO.GetComponent<Image>();
            imgLogo.sprite = sLogo;
            imgLogo.preserveAspect = true;

            // 11. Game Version - Anchored Bottom-Right
            GameObject verGO = new GameObject("VersionText", typeof(RectTransform), typeof(TextMeshProUGUI));
            verGO.transform.SetParent(rtScreen, false);
            RectTransform rtVer = verGO.GetComponent<RectTransform>();
            rtVer.anchorMin = new Vector2(1f, 0f);
            rtVer.anchorMax = new Vector2(1f, 0f);
            rtVer.pivot = new Vector2(1f, 0f);
            rtVer.anchoredPosition = new Vector2(-55f, 40f);
            rtVer.sizeDelta = new Vector2(120f, 30f);
            TextMeshProUGUI tmpVer = verGO.GetComponent<TextMeshProUGUI>();
            if (font != null) tmpVer.font = font;
            tmpVer.text = "v0.1.0";
            tmpVer.fontSize = 17;
            tmpVer.fontStyle = FontStyles.Normal;
            tmpVer.alignment = TextAlignmentOptions.Right;
            tmpVer.color = new Color(0.77f, 0.73f, 0.65f); // #C4BAA6

            // 12. Wire Serialized Fields on LoadingScreenController
            SerializedObject so = new SerializedObject(ctrl);
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("backgroundImage").objectReferenceValue = imgBg;
            so.FindProperty("artworkImage").objectReferenceValue = imgArt;
            so.FindProperty("progressBarFill").objectReferenceValue = imgFill;
            so.FindProperty("progressPercentText").objectReferenceValue = tmpPct;
            so.FindProperty("tipText").objectReferenceValue = tmpTip;
            so.FindProperty("tipCanvasGroup").objectReferenceValue = tipCg;
            so.FindProperty("logoImage").objectReferenceValue = imgLogo;
            so.FindProperty("versionText").objectReferenceValue = tmpVer;
            so.FindProperty("targetSceneName").stringValue = "SampleScene";
            so.FindProperty("minimumDisplayTime").floatValue = 3.0f;
            so.FindProperty("fadeOutDuration").floatValue = 0.6f;
            so.FindProperty("autoLoadOnStart").boolValue = true;
            so.FindProperty("showTips").boolValue = true;
            so.FindProperty("tipCycleInterval").floatValue = 2.5f;
            so.ApplyModifiedProperties();

            // 13. Save scene to Assets/Scenes/LoadingScene.unity
            EditorSceneManager.SaveScene(newScene, LOADING_SCENE_PATH);
            Debug.Log("<color=green><b>[LoadingSceneBuilder]</b> Saved dedicated loading scene matching reference to: " + LOADING_SCENE_PATH + "</color>");

            // 14. Configure Build Settings: Scene 0 = LoadingScene, Scene 1 = SampleScene
            ConfigureBuildSettings();
        }

        [MenuItem("Tools/Jbel Ighoud/Configure Build Settings")]
        public static void ConfigureBuildSettings()
        {
            var buildScenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(LOADING_SCENE_PATH, true),
                new EditorBuildSettingsScene(SAMPLE_SCENE_PATH, true)
            };

            EditorBuildSettings.scenes = buildScenes.ToArray();
            Debug.Log("<color=green><b>[LoadingSceneBuilder]</b> Build Settings updated successfully! Scene 0: LoadingScene, Scene 1: SampleScene</color>");
        }
    }
}
