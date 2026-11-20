using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using JbelIghoud.Audio;
using JbelIghoud.UI;

namespace JbelIghoud.EditorScripts
{
    public static class JbelAudioSystemBuilderEditor
    {
        private const string SOUND_BG_PATH = "Assets/UI/settings/settings/sound bg.png";
        private const string MODAL_BG_PATH = "Assets/UI/settings/settings/bg.png";
        private const string LEFT_GROOVE_PATH = "Assets/UI/settings/settings/left.png";
        private const string SELECTED_FILL_PATH = "Assets/UI/settings/settings/selected.png";
        private const string RANGE_BTN_PATH = "Assets/UI/settings/settings/range btn.png";
        private const string RADIO_BTN_PATH = "Assets/UI/settings/settings/subtitle radio btn.png";
        private const string SUBTITLE_BG_PATH = "Assets/UI/hud/clean_sprites/card_quest_bg.png";
        private const string FONT_PATH = "Assets/Fonts/IM_FELL_English_SC SDF.asset";
        private const string MUSIC_CLIP_PATH = "Assets/Audio/Tranquil Exploration.wav";
        private const string VOICE_CLIP_PATH = "Assets/Audio/talk/ElevenLabs_2026-11-09T18_03_39_Spuds Oxley - Grandpa _pvc_s50_sim75_v4.wav";
        private const string SETTINGS_CARD_BG_PATH = "Assets/UI/settings/bg.png";
        private const string SETTINGS_ARROW_PATH = "Assets/UI/settings/arrow.png";

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

        [MenuItem("Tools/Jbel Ighoud/Build Complete Audio & Sound Settings System")]
        public static void BuildCompleteSystem()
        {
            Undo.SetCurrentGroupName("Build Jbel Audio & Sound Settings System");
            int undoGroup = Undo.GetCurrentGroup();

            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT_PATH);
            var musicClip = AssetDatabase.LoadAssetAtPath<AudioClip>(MUSIC_CLIP_PATH);
            var voiceClip = AssetDatabase.LoadAssetAtPath<AudioClip>(VOICE_CLIP_PATH);

            var soundBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SOUND_BG_PATH);
            var modalBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MODAL_BG_PATH);
            var grooveSprite = AssetDatabase.LoadAssetAtPath<Sprite>(LEFT_GROOVE_PATH);
            var fillSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SELECTED_FILL_PATH);
            var knobSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RANGE_BTN_PATH);
            var radioBtnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(RADIO_BTN_PATH);
            var subtitleBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SUBTITLE_BG_PATH);
            var cardBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SETTINGS_CARD_BG_PATH);
            var arrowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SETTINGS_ARROW_PATH);

            // 1. Build or configure GameAudioManager in scene
            BuildGameAudioManager(musicClip);

            // 2. Build Subtitle HUD on MobileHUD_Canvas
            BuildSubtitleHUD(font, subtitleBgSprite);

            // 3. Ensure SettingsScreenModal exists under MobileHUD_Canvas
            BuildSettingsScreenModal(font, cardBgSprite, arrowSprite);

            // 4. Build SoundSettingsModal under SettingsScreenModal
            BuildSoundSettingsModal(font, modalBgSprite, soundBgSprite, grooveSprite, fillSprite, knobSprite, radioBtnSprite);

            // 5. Build GraphicsSettingsModal under SettingsScreenModal if assets present
            var generalBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/graphics/general bg.png");
            var graphicsBadgeSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/graphics/graphics.png");
            var optionsBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/graphics/options bg.png");
            var radioDotSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/graphics/radio_dot.png");
            if (generalBgSprite != null && graphicsBadgeSprite != null)
            {
                JbelGraphicsSystemBuilderEditor.BuildGraphicsSettingsModal(font, generalBgSprite, graphicsBadgeSprite, optionsBgSprite, radioBtnSprite, radioDotSprite);
                // Subtitle toggle uses authentic CheckX matching reference
            }

            // Wire JbelSettingsMenuController
            if (JbelSettingsMenuController.Instance != null)
            {
                JbelSettingsMenuController.Instance.AutoWireReferences();
            }

            // 6. Build Person X (Elder NPC Capsule)
            BuildPersonXNPC(voiceClip, font);

            Undo.CollapseUndoOperations(undoGroup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("<color=green><b>[Jbel Audio Builder]</b> Successfully built and integrated Audio, Settings Screen, Sound Settings UI, Subtitles, and Person X!</color>");
        }

        private static void BuildGameAudioManager(AudioClip musicClip)
        {
            var existing = Object.FindAnyObjectByType<GameAudioManager>(FindObjectsInactive.Include);
            GameObject go;
            if (existing != null)
            {
                go = existing.gameObject;
            }
            else
            {
                go = new GameObject("[GameAudioManager]");
                Undo.RegisterCreatedObjectUndo(go, "Create GameAudioManager");
            }

            var manager = GetOrAddComponent<GameAudioManager>(go);

            var so = new SerializedObject(manager);
            so.FindProperty("themeMusicClip").objectReferenceValue = musicClip;
            so.FindProperty("musicPauseInterval").floatValue = 60f;
            so.FindProperty("musicFadeDuration").floatValue = 2.5f;

            // Ensure AudioSources
            var sources = go.GetComponents<AudioSource>();
            AudioSource mSrc = null, vSrc = null, sSrc = null;
            if (sources.Length >= 3)
            {
                mSrc = sources[0];
                vSrc = sources[1];
                sSrc = sources[2];
            }
            else
            {
                while (sources.Length < 3)
                {
                    go.AddComponent<AudioSource>();
                    sources = go.GetComponents<AudioSource>();
                }
                mSrc = sources[0];
                vSrc = sources[1];
                sSrc = sources[2];
            }

            mSrc.playOnAwake = false;
            mSrc.loop = false;
            mSrc.spatialBlend = 0f;

            vSrc.playOnAwake = false;
            vSrc.loop = false;
            vSrc.spatialBlend = 0f;

            sSrc.playOnAwake = false;
            sSrc.loop = false;
            sSrc.spatialBlend = 0f;

            so.FindProperty("musicSource").objectReferenceValue = mSrc;
            so.FindProperty("voiceSource").objectReferenceValue = vSrc;
            so.FindProperty("sfxSource").objectReferenceValue = sSrc;
            so.ApplyModifiedProperties();
        }

        private static void BuildSubtitleHUD(TMP_FontAsset font, Sprite backdropSprite)
        {
            var canvas = GameObject.Find("MobileHUD_Canvas");
            if (canvas == null) return;

            GameObject panelGO = GetOrCreateUIChild(canvas.transform, "SubtitleHUD_Panel");
            panelGO.transform.SetSiblingIndex(canvas.transform.childCount - 1);

            var rt = panelGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 45f);
            rt.sizeDelta = new Vector2(980f, 130f);

            var img = GetOrAddComponent<Image>(panelGO);
            img.sprite = backdropSprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            img.raycastTarget = false;

            var cg = GetOrAddComponent<CanvasGroup>(panelGO);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            // Speaker Text (anchored to top)
            GameObject sGO = GetOrCreateUIChild(panelGO.transform, "SpeakerText");
            var sRT = sGO.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0.5f, 1f);
            sRT.anchorMax = new Vector2(0.5f, 1f);
            sRT.pivot = new Vector2(0.5f, 1f);
            sRT.anchoredPosition = new Vector2(0f, -12f);
            sRT.sizeDelta = new Vector2(860f, 26f);

            var sTMP = GetOrAddComponent<TextMeshProUGUI>(sGO);
            sTMP.font = font;
            sTMP.fontSize = 18f;
            sTMP.fontStyle = FontStyles.Bold;
            sTMP.color = new Color(0.92f, 0.78f, 0.52f, 1f); // Warm gold
            sTMP.alignment = TextAlignmentOptions.Center;
            sTMP.text = "ELDER";
            sTMP.raycastTarget = false;

            // Dialogue Content Text (inside the dark ribbon below Speaker)
            GameObject dGO = GetOrCreateUIChild(panelGO.transform, "DialogueText");
            var dRT = dGO.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 0f);
            dRT.anchorMax = new Vector2(1f, 1f);
            dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.offsetMin = new Vector2(60f, 12f);
            dRT.offsetMax = new Vector2(-60f, -38f);

            var dTMP = GetOrAddComponent<TextMeshProUGUI>(dGO);
            dTMP.font = font;
            dTMP.fontSize = 20f;
            dTMP.color = new Color(0.97f, 0.94f, 0.88f, 1f); // Crisp warm off-white
            dTMP.alignment = TextAlignmentOptions.Center;
            dTMP.textWrappingMode = TextWrappingModes.Normal;
            dTMP.text = "The world is a strange place... stranger than most folks care to admit.";
            dTMP.raycastTarget = false;

            var ctrl = GetOrAddComponent<SubtitleHUDController>(panelGO);
            ctrl.AutoWire();
        }

        private static GameObject BuildSettingsScreenModal(TMP_FontAsset font, Sprite cardBgSprite, Sprite arrowSprite)
        {
            var canvas = GameObject.Find("MobileHUD_Canvas");
            if (canvas == null) return null;

            GameObject settingsGO = GetOrCreateUIChild(canvas.transform, "SettingsScreenModal");
            var sRT = settingsGO.GetComponent<RectTransform>();
            sRT.anchorMin = Vector2.zero;
            sRT.anchorMax = Vector2.one;
            sRT.sizeDelta = Vector2.zero;
            sRT.anchoredPosition = Vector2.zero;

            var cg = GetOrAddComponent<CanvasGroup>(settingsGO);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            var ctrl = GetOrAddComponent<JbelSettingsMenuController>(settingsGO);

            // DimBackdrop
            GameObject dimGO = GetOrCreateUIChild(settingsGO.transform, "DimBackdrop");
            var dimRT = dimGO.GetComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.sizeDelta = Vector2.zero;
            dimRT.anchoredPosition = Vector2.zero;
            var dimImg = GetOrAddComponent<Image>(dimGO);
            dimImg.color = new Color(0f, 0f, 0f, 0.75f);
            dimImg.raycastTarget = false;

            // SettingsContainer
            GameObject containerGO = GetOrCreateUIChild(settingsGO.transform, "SettingsContainer");
            var cRT = containerGO.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0.5f, 0.5f);
            cRT.anchorMax = new Vector2(0.5f, 0.5f);
            cRT.pivot = new Vector2(0.5f, 0.5f);
            cRT.anchoredPosition = Vector2.zero;
            cRT.sizeDelta = new Vector2(1060f, 580f);

            // BackButton
            GameObject backGO = GetOrCreateUIChild(containerGO.transform, "BackButton");
            var bRT = backGO.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0f, 1f);
            bRT.anchorMax = new Vector2(0f, 1f);
            bRT.pivot = new Vector2(0.5f, 0.5f);
            bRT.anchoredPosition = new Vector2(40f, -35f);
            bRT.sizeDelta = new Vector2(60f, 60f);

            var backImg = GetOrAddComponent<Image>(backGO);
            backImg.sprite = arrowSprite;
            backImg.type = Image.Type.Simple;
            backImg.color = Color.white;
            var backBtn = GetOrAddComponent<Button>(backGO);

            // HeaderTitle
            GameObject titleGO = GetOrCreateUIChild(containerGO.transform, "HeaderTitle");
            var tRT = titleGO.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0.5f, 1f);
            tRT.anchorMax = new Vector2(0.5f, 1f);
            tRT.pivot = new Vector2(0.5f, 0.5f);
            tRT.anchoredPosition = new Vector2(0f, -35f);
            tRT.sizeDelta = new Vector2(300f, 50f);

            var tTMP = GetOrAddComponent<TextMeshProUGUI>(titleGO);
            tTMP.font = font;
            tTMP.fontSize = 28f;
            tTMP.fontStyle = FontStyles.Bold;
            tTMP.alignment = TextAlignmentOptions.Center;
            tTMP.color = new Color(0.92f, 0.78f, 0.52f, 1f);
            tTMP.text = "SETTINGS";

            // CardsGrid
            GameObject gridGO = GetOrCreateUIChild(containerGO.transform, "CardsGrid");
            var gRT = gridGO.GetComponent<RectTransform>();
            gRT.anchorMin = new Vector2(0.5f, 0.5f);
            gRT.anchorMax = new Vector2(0.5f, 0.5f);
            gRT.pivot = new Vector2(0.5f, 0.5f);
            gRT.anchoredPosition = new Vector2(0f, -25f);
            gRT.sizeDelta = new Vector2(980f, 400f);

            string[] cardKeys = { "Card_Sound", "Card_Graphics", "Card_Control", "Card_Language", "Card_Accessibility", "Card_ResetDefaults" };
            string[] titles = { "SOUND", "GRAPHICS", "CONTROLS", "LANGUAGE", "ACCESSIBILITY", "RESET DEFAULTS" };
            string[] subtitles = { "Audio Levels & Voices", "Visual Quality & Display", "Touch & Key Bindings", "Subtitles & Interface", "Color, UI Scale, Assists", "Restore original values" };

            float[] colX = { -325f, 0f, 325f };
            float[] rowY = { 85f, -95f };

            for (int i = 0; i < 6; i++)
            {
                int col = i % 3;
                int row = i / 3;

                GameObject cardGO = GetOrCreateUIChild(gridGO.transform, cardKeys[i]);
                var cardRT = cardGO.GetComponent<RectTransform>();
                cardRT.anchorMin = new Vector2(0.5f, 0.5f);
                cardRT.anchorMax = new Vector2(0.5f, 0.5f);
                cardRT.pivot = new Vector2(0.5f, 0.5f);
                cardRT.anchoredPosition = new Vector2(colX[col], rowY[row]);
                cardRT.sizeDelta = new Vector2(300f, 150f);

                var cardImg = GetOrAddComponent<Image>(cardGO);
                cardImg.sprite = cardBgSprite;
                cardImg.type = Image.Type.Sliced;
                cardImg.color = Color.white;

                var btn = GetOrAddComponent<Button>(cardGO);
                ColorBlock cb = btn.colors;
                cb.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
                cb.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
                btn.colors = cb;

                // TitleText
                GameObject ttGO = GetOrCreateUIChild(cardGO.transform, "TitleText");
                var ttRT = ttGO.GetComponent<RectTransform>();
                ttRT.anchorMin = new Vector2(0f, 0f);
                ttRT.anchorMax = new Vector2(1f, 0f);
                ttRT.pivot = new Vector2(0f, 0f);
                ttRT.anchoredPosition = new Vector2(22f, 42f);
                ttRT.sizeDelta = new Vector2(-44f, 30f);

                var ttTMP = GetOrAddComponent<TextMeshProUGUI>(ttGO);
                ttTMP.font = font;
                ttTMP.fontSize = 20f;
                ttTMP.fontStyle = FontStyles.Bold;
                ttTMP.alignment = TextAlignmentOptions.Left;
                ttTMP.color = new Color(0.95f, 0.89f, 0.81f, 1f);
                ttTMP.text = titles[i];

                // SubtitleText
                GameObject stGO = GetOrCreateUIChild(cardGO.transform, "SubtitleText");
                var stRT = stGO.GetComponent<RectTransform>();
                stRT.anchorMin = new Vector2(0f, 0f);
                stRT.anchorMax = new Vector2(1f, 0f);
                stRT.pivot = new Vector2(0f, 0f);
                stRT.anchoredPosition = new Vector2(22f, 18f);
                stRT.sizeDelta = new Vector2(-44f, 24f);

                var stTMP = GetOrAddComponent<TextMeshProUGUI>(stGO);
                stTMP.font = font;
                stTMP.fontSize = 12.5f;
                stTMP.alignment = TextAlignmentOptions.Left;
                stTMP.color = new Color(0.72f, 0.68f, 0.62f, 1f);
                stTMP.text = subtitles[i];
            }

            // Toast Notification
            GameObject toastGO = GetOrCreateUIChild(containerGO.transform, "ToastNotification");
            var toastRT = toastGO.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.5f, 0f);
            toastRT.anchorMax = new Vector2(0.5f, 0f);
            toastRT.pivot = new Vector2(0.5f, 0.5f);
            toastRT.anchoredPosition = new Vector2(0f, 40f);
            toastRT.sizeDelta = new Vector2(400f, 42f);

            var toastCG = GetOrAddComponent<CanvasGroup>(toastGO);
            toastCG.alpha = 0f;
            toastCG.blocksRaycasts = false;
            toastCG.interactable = false;

            var toastImg = GetOrAddComponent<Image>(toastGO);
            toastImg.color = new Color(0.12f, 0.1f, 0.08f, 0.92f);

            GameObject toastTxtGO = GetOrCreateUIChild(toastGO.transform, "ToastText");
            var txtRT = toastTxtGO.GetComponent<RectTransform>();
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.sizeDelta = Vector2.zero;
            txtRT.anchoredPosition = Vector2.zero;

            var txtTMP = GetOrAddComponent<TextMeshProUGUI>(toastTxtGO);
            txtTMP.font = font;
            txtTMP.fontSize = 16f;
            txtTMP.alignment = TextAlignmentOptions.Center;
            txtTMP.color = new Color(0.95f, 0.85f, 0.6f, 1f);

            ctrl.AutoWireReferences();
            return settingsGO;
        }

        private static void BuildSoundSettingsModal(
            TMP_FontAsset font,
            Sprite modalBg,
            Sprite soundBadgeBg,
            Sprite grooveBg,
            Sprite fillBg,
            Sprite knobBg,
            Sprite radioBtnBg)
        {
            var settingsModal = GameObject.Find("MobileHUD_Canvas/SettingsScreenModal");
            if (settingsModal == null) return;

            GameObject modalGO = GetOrCreateUIChild(settingsModal.transform, "SoundSettingsModal");

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

            // 1. Header Pill "SOUND"
            GameObject pillGO = GetOrCreateUIChild(cardGO.transform, "HeaderPill_Sound");
            var pillRT = pillGO.GetComponent<RectTransform>();
            pillRT.anchorMin = new Vector2(0f, 1f);
            pillRT.anchorMax = new Vector2(0f, 1f);
            pillRT.pivot = new Vector2(0.5f, 0.5f);
            pillRT.anchoredPosition = new Vector2(75f, 0f);
            pillRT.sizeDelta = new Vector2(145f, 56f);

            var pillImg = GetOrAddComponent<Image>(pillGO);
            pillImg.sprite = soundBadgeBg;
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
            ptTMP.fontSize = 18f;
            ptTMP.fontStyle = FontStyles.Bold;
            ptTMP.color = new Color(0.96f, 0.92f, 0.84f, 1f);
            ptTMP.alignment = TextAlignmentOptions.Center;
            ptTMP.text = "SOUND";

            // 2. Close Button 'X'
            GameObject closeGO = GetOrCreateUIChild(cardGO.transform, "CloseButton");
            var closeRT = closeGO.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(1f, 1f);
            closeRT.anchorMax = new Vector2(1f, 1f);
            closeRT.pivot = new Vector2(0.5f, 0.5f);
            closeRT.anchoredPosition = new Vector2(-22f, 0f);
            closeRT.sizeDelta = new Vector2(50f, 50f);

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

            // 3. Row SFX
            Slider sfxSlider = CreateSliderRow(cardGO.transform, "Row_SFX", "SOUND EFFECT", 72f, font, grooveBg, fillBg, knobBg);

            // 4. Row Music
            Slider musicSlider = CreateSliderRow(cardGO.transform, "Row_Music", "MUSIC", 2f, font, grooveBg, fillBg, knobBg);

            // 5. Row Voice & Subtitles
            GameObject voiceRowGO = GetOrCreateUIChild(cardGO.transform, "Row_Voice");
            var vrRT = voiceRowGO.GetComponent<RectTransform>();
            vrRT.anchorMin = new Vector2(0f, 0.5f);
            vrRT.anchorMax = new Vector2(1f, 0.5f);
            vrRT.pivot = new Vector2(0.5f, 0.5f);
            vrRT.anchoredPosition = new Vector2(0f, -68f);
            vrRT.sizeDelta = new Vector2(0f, 50f);

            // Voice Label
            GameObject vlGO = GetOrCreateUIChild(voiceRowGO.transform, "Label");
            var vlRT = vlGO.GetComponent<RectTransform>();
            vlRT.anchorMin = new Vector2(0f, 0.5f);
            vlRT.anchorMax = new Vector2(0f, 0.5f);
            vlRT.pivot = new Vector2(0f, 0.5f);
            vlRT.anchoredPosition = new Vector2(48f, 0f);
            vlRT.sizeDelta = new Vector2(180f, 36f);

            var vlTMP = GetOrAddComponent<TextMeshProUGUI>(vlGO);
            vlTMP.font = font;
            vlTMP.fontSize = 21f;
            vlTMP.fontStyle = FontStyles.Normal;
            vlTMP.color = new Color(0.95f, 0.89f, 0.81f, 1f);
            vlTMP.alignment = TextAlignmentOptions.Left;
            vlTMP.text = "VOICE";

            // Voice Slider
            Slider voiceSlider = CreateSliderComponent(voiceRowGO.transform, "Slider_Voice", new Vector2(245f, 0f), new Vector2(275f, 14f), grooveBg, fillBg, knobBg);

            // Subtitles Group
            GameObject subGroupGO = GetOrCreateUIChild(voiceRowGO.transform, "SubtitlesGroup");
            var sgRT = subGroupGO.GetComponent<RectTransform>();
            sgRT.anchorMin = new Vector2(1f, 0.5f);
            sgRT.anchorMax = new Vector2(1f, 0.5f);
            sgRT.pivot = new Vector2(1f, 0.5f);
            sgRT.anchoredPosition = new Vector2(-42f, 0f);
            sgRT.sizeDelta = new Vector2(250f, 48f);

            // Subtitles Label
            GameObject subLabelGO = GetOrCreateUIChild(subGroupGO.transform, "SubtitlesLabel");
            var slRT = subLabelGO.GetComponent<RectTransform>();
            slRT.anchorMin = new Vector2(0f, 0.5f);
            slRT.anchorMax = new Vector2(0f, 0.5f);
            slRT.pivot = new Vector2(0f, 0.5f);
            slRT.anchoredPosition = new Vector2(10f, 0f);
            slRT.sizeDelta = new Vector2(170f, 36f);

            var slTMP = GetOrAddComponent<TextMeshProUGUI>(subLabelGO);
            slTMP.font = font;
            slTMP.fontSize = 24f;
            slTMP.fontStyle = FontStyles.Normal;
            slTMP.color = new Color(0.95f, 0.89f, 0.81f, 1f);
            slTMP.alignment = TextAlignmentOptions.Right;
            slTMP.text = "SUBTITLES";

            // Subtitles Radio Button
            GameObject radioGO = GetOrCreateUIChild(subGroupGO.transform, "SubtitleRadioBtn");
            var rRT = radioGO.GetComponent<RectTransform>();
            rRT.anchorMin = new Vector2(1f, 0.5f);
            rRT.anchorMax = new Vector2(1f, 0.5f);
            rRT.pivot = new Vector2(1f, 0.5f);
            rRT.anchoredPosition = new Vector2(-10f, 0f);
            rRT.sizeDelta = new Vector2(44f, 44f);

            var rImg = GetOrAddComponent<Image>(radioGO);
            rImg.sprite = radioBtnBg;
            rImg.type = Image.Type.Simple;
            rImg.color = Color.white;

            var rBtn = GetOrAddComponent<Button>(radioGO);

            // Check X
            GameObject checkXGO = GetOrCreateUIChild(radioGO.transform, "CheckX");
            var cxRT = checkXGO.GetComponent<RectTransform>();
            cxRT.anchorMin = Vector2.zero;
            cxRT.anchorMax = Vector2.one;
            cxRT.sizeDelta = Vector2.zero;
            cxRT.anchoredPosition = Vector2.zero;

            var cxTMP = GetOrAddComponent<TextMeshProUGUI>(checkXGO);
            cxTMP.font = font;
            cxTMP.fontSize = 26f;
            cxTMP.fontStyle = FontStyles.Bold;
            cxTMP.color = new Color(0.98f, 0.96f, 0.90f, 1f);
            cxTMP.alignment = TextAlignmentOptions.Center;
            cxTMP.text = "X";

            // Attach & Wire Modal Controller
            var modalCtrl = GetOrAddComponent<JbelSoundSettingsModal>(modalGO);
            modalCtrl.AutoWire();

            // Wire to JbelSettingsMenuController
            var settingsCtrl = settingsModal.GetComponent<JbelSettingsMenuController>();
            if (settingsCtrl != null)
            {
                var sCtrlSO = new SerializedObject(settingsCtrl);
                sCtrlSO.FindProperty("soundSettingsModal").objectReferenceValue = modalCtrl;
                sCtrlSO.ApplyModifiedProperties();
            }

            modalGO.SetActive(false);
        }

        private static Slider CreateSliderRow(
            Transform parent,
            string rowName,
            string labelText,
            float yPos,
            TMP_FontAsset font,
            Sprite grooveBg,
            Sprite fillBg,
            Sprite knobBg)
        {
            GameObject rowGO = GetOrCreateUIChild(parent, rowName);
            var rRT = rowGO.GetComponent<RectTransform>();
            rRT.anchorMin = new Vector2(0f, 0.5f);
            rRT.anchorMax = new Vector2(1f, 0.5f);
            rRT.pivot = new Vector2(0.5f, 0.5f);
            rRT.anchoredPosition = new Vector2(0f, yPos);
            rRT.sizeDelta = new Vector2(0f, 50f);

            // Label
            GameObject lGO = GetOrCreateUIChild(rowGO.transform, "Label");
            var lRT = lGO.GetComponent<RectTransform>();
            lRT.anchorMin = new Vector2(0f, 0.5f);
            lRT.anchorMax = new Vector2(0f, 0.5f);
            lRT.pivot = new Vector2(0f, 0.5f);
            lRT.anchoredPosition = new Vector2(48f, 0f);
            lRT.sizeDelta = new Vector2(180f, 36f);

            var lTMP = GetOrAddComponent<TextMeshProUGUI>(lGO);
            lTMP.font = font;
            lTMP.fontSize = 21f;
            lTMP.fontStyle = FontStyles.Normal;
            lTMP.color = new Color(0.95f, 0.89f, 0.81f, 1f);
            lTMP.alignment = TextAlignmentOptions.Left;
            lTMP.text = labelText;

            // Slider
            string sliderName = "Slider_" + rowName.Replace("Row_", "");
            return CreateSliderComponent(rowGO.transform, sliderName, new Vector2(245f, 0f), new Vector2(275f, 14f), grooveBg, fillBg, knobBg);
        }

        private static Slider CreateSliderComponent(
            Transform parent,
            string sliderName,
            Vector2 anchoredPos,
            Vector2 sizeDelta,
            Sprite grooveBg,
            Sprite fillBg,
            Sprite knobBg)
        {
            GameObject sGO = GetOrCreateUIChild(parent, sliderName);
            var sRT = sGO.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0.5f);
            sRT.anchorMax = new Vector2(0f, 0.5f);
            sRT.pivot = new Vector2(0f, 0.5f);
            sRT.anchoredPosition = anchoredPos;
            sRT.sizeDelta = sizeDelta;

            var slider = GetOrAddComponent<Slider>(sGO);
            slider.minValue = 0f;
            slider.maxValue = 1f;

            // Background Groove
            GameObject bgGO = GetOrCreateUIChild(sGO.transform, "Background");
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            bgRT.anchoredPosition = Vector2.zero;

            var bgImg = GetOrAddComponent<Image>(bgGO);
            bgImg.sprite = grooveBg;
            bgImg.type = Image.Type.Sliced;
            bgImg.color = Color.white;

            // Fill Area
            GameObject fillAreaGO = GetOrCreateUIChild(sGO.transform, "Fill Area");
            var faRT = fillAreaGO.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0f, 0.15f);
            faRT.anchorMax = new Vector2(1f, 0.85f);
            faRT.sizeDelta = new Vector2(-16f, 0f);
            faRT.anchoredPosition = Vector2.zero;

            GameObject fillGO = GetOrCreateUIChild(fillAreaGO.transform, "Fill");
            var fRT = fillGO.GetComponent<RectTransform>();
            fRT.anchorMin = Vector2.zero;
            fRT.anchorMax = Vector2.one;
            fRT.sizeDelta = Vector2.zero;
            fRT.anchoredPosition = Vector2.zero;

            var fillImg = GetOrAddComponent<Image>(fillGO);
            fillImg.sprite = fillBg;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0.9f, 0.75f, 0.5f, 0.6f);

            // Handle Slide Area
            GameObject handleAreaGO = GetOrCreateUIChild(sGO.transform, "Handle Slide Area");
            var haRT = handleAreaGO.GetComponent<RectTransform>();
            haRT.anchorMin = Vector2.zero;
            haRT.anchorMax = Vector2.one;
            haRT.sizeDelta = new Vector2(-16f, 0f);
            haRT.anchoredPosition = Vector2.zero;

            GameObject handleGO = GetOrCreateUIChild(handleAreaGO.transform, "Handle");
            var hRT = handleGO.GetComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(26f, 26f);
            hRT.anchoredPosition = Vector2.zero;

            var handleImg = GetOrAddComponent<Image>(handleGO);
            handleImg.sprite = knobBg;
            handleImg.type = Image.Type.Simple;
            handleImg.color = Color.white;

            slider.fillRect = fRT;
            slider.handleRect = hRT;
            slider.targetGraphic = handleImg;

            return slider;
        }

        private static void BuildPersonXNPC(AudioClip voiceClip, TMP_FontAsset font)
        {
            var existing = GameObject.Find("Person_X_OldMan");
            GameObject npcGO;
            if (existing != null)
            {
                npcGO = existing;
            }
            else
            {
                npcGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                npcGO.name = "Person_X_OldMan";
                Undo.RegisterCreatedObjectUndo(npcGO, "Create Person_X_OldMan");
            }

            // Position 5m ahead of player facing player
            npcGO.transform.position = new Vector3(114.2f, 1.7f, -37.0f);
            npcGO.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            // Style capsule material with prehistoric warm elder tone
            var renderer = npcGO.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat == null) mat = new Material(Shader.Find("Standard"));
                mat.name = "M_PersonX_Elder";
                mat.color = new Color(0.48f, 0.32f, 0.18f, 1f); // Rich warm leather/bronze
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
                renderer.sharedMaterial = mat;
            }

            // World Prompt UI
            Transform promptT = npcGO.transform.Find("WorldPrompt");
            GameObject promptGO;
            if (promptT != null)
            {
                promptGO = promptT.gameObject;
            }
            else
            {
                promptGO = new GameObject("WorldPrompt");
                promptGO.transform.SetParent(npcGO.transform, false);
            }

            promptGO.transform.localPosition = new Vector3(0f, 1.45f, 0f);

            var tmp = GetOrAddComponent<TextMeshPro>(promptGO);
            tmp.font = font;
            tmp.fontSize = 4.2f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = "<color=#D4AF37><b>Elder</b></color>\n<size=75%><color=#FFFFFF>[E] Talk</color></size>";
            tmp.color = Color.white;

            var interactable = GetOrAddComponent<PersonXInteractable>(npcGO);
            var so = new SerializedObject(interactable);
            so.FindProperty("voiceClip").objectReferenceValue = voiceClip;
            so.FindProperty("characterName").stringValue = "ELDER";
            so.FindProperty("interactionRadius").floatValue = 4.0f;
            so.FindProperty("worldPromptContainer").objectReferenceValue = promptGO;
            so.FindProperty("promptText").objectReferenceValue = tmp;

            var ybot = GameObject.Find("Y Bot");
            if (ybot != null) so.FindProperty("playerTransform").objectReferenceValue = ybot.transform;

            so.ApplyModifiedProperties();
        }
    }
}
