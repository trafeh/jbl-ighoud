using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JbelIghoud.DayNight
{
    /// <summary>
    /// In-game Clock and Day/Night HUD Widget.
    /// Displays time, day number, phase icon, and provides quick controls for testing and gameplay.
    /// </summary>
    [ExecuteAlways]
    public class DayNightClockHUD : MonoBehaviour
    {
        [Header("--- UI References ---")]
        public Text timeText;
        public Text phaseText;
        public Text dayText;
        public GameObject controlsPanel;
        public Slider timeSlider;
        public Button toggleControlsButton;

        [Header("--- Styling & Settings ---")]
        public bool show12HourFormat = true;
        public bool startWithControlsCollapsed = true;

        private DayNightCycleManager manager;
        private bool isUpdatingFromCode = false;

        private void OnEnable()
        {
            manager = DayNightCycleManager.Instance;
            if (manager == null)
            {
                manager = FindAnyObjectByType<DayNightCycleManager>();
            }
            UpdateDisplay();
        }

        private void Start()
        {
            manager = DayNightCycleManager.Instance;
            if (manager == null)
            {
                manager = FindAnyObjectByType<DayNightCycleManager>();
            }

            if (controlsPanel != null && startWithControlsCollapsed)
            {
                controlsPanel.SetActive(false);
            }

            if (timeSlider != null)
            {
                timeSlider.minValue = 0f;
                timeSlider.maxValue = 24f;
                timeSlider.onValueChanged.AddListener(OnSliderTimeChanged);
            }

            if (toggleControlsButton != null)
            {
                toggleControlsButton.onClick.AddListener(ToggleControls);
            }

            UpdateDisplay();
        }

        private void Update()
        {
            if (manager == null)
            {
                manager = DayNightCycleManager.Instance;
                if (manager == null) return;
            }

            UpdateDisplay();

            if (Application.isPlaying)
            {
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
                {
                    ToggleControls();
                }
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.T))
                {
                    ToggleControls();
                }
#endif
            }
        }

        private void UpdateDisplay()
        {
            if (manager == null) return;

            if (timeText != null)
            {
                timeText.text = manager.GetFormattedTime(!show12HourFormat);
            }

            if (phaseText != null)
            {
                string icon = GetPhaseEmoji(manager.CurrentTime);
                phaseText.text = $"{icon} {manager.CurrentPhase}";
            }

            if (dayText != null)
            {
                dayText.text = manager.GetFormattedDay();
            }

#if ENABLE_INPUT_SYSTEM
            bool isInteracting = Application.isPlaying && Mouse.current != null && Mouse.current.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            bool isInteracting = Application.isPlaying && Input.GetMouseButton(0);
#else
            bool isInteracting = false;
#endif
            if (timeSlider != null && !isUpdatingFromCode && !isInteracting)
            {
                isUpdatingFromCode = true;
                timeSlider.value = manager.CurrentTime;
                isUpdatingFromCode = false;
            }
        }

        private string GetPhaseEmoji(float hour)
        {
            if (hour >= 5f && hour < 8f) return "🌅"; // Sunrise / Morning
            if (hour >= 8f && hour < 16f) return "☀️"; // Day
            if (hour >= 16f && hour < 19f) return "🌇"; // Sunset
            if (hour >= 19f && hour < 21f) return "🌆"; // Dusk
            return "🌙"; // Night
        }

        public void ToggleControls()
        {
            if (controlsPanel != null)
            {
                controlsPanel.SetActive(!controlsPanel.activeSelf);
            }
        }

        public void SetSpeed1x() => SetSpeed(1f);
        public void SetSpeed5x() => SetSpeed(5f);
        public void SetSpeed20x() => SetSpeed(20f);
        public void SetSpeed60x() => SetSpeed(60f);

        public void SetSpeed(float mult)
        {
            if (manager != null)
            {
                manager.isPaused = false;
                manager.SetTimeMultiplier(mult);
            }
        }

        public void TogglePause()
        {
            if (manager != null)
            {
                manager.TogglePause();
            }
        }

        private void OnSliderTimeChanged(float value)
        {
            if (isUpdatingFromCode || manager == null) return;
            manager.SetTime(value);
        }

        /// <summary>
        /// Programmatically creates the Clock HUD hierarchy if not present in the scene.
        /// </summary>
        public static DayNightClockHUD CreateDefaultHUD(Canvas targetCanvas)
        {
            if (targetCanvas == null)
            {
                targetCanvas = FindAnyObjectByType<Canvas>();
                if (targetCanvas == null)
                {
                    GameObject canvasGO = new GameObject("HUD_Canvas");
                    targetCanvas = canvasGO.AddComponent<Canvas>();
                    targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvasGO.AddComponent<CanvasScaler>();
                    canvasGO.AddComponent<GraphicRaycaster>();
                }
            }

            // Root container
            GameObject hudRoot = new GameObject("DayNightClockWidget");
            hudRoot.transform.SetParent(targetCanvas.transform, false);

            RectTransform rootRect = hudRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(1f, 1f);
            rootRect.anchoredPosition = new Vector2(-25f, -25f);
            rootRect.sizeDelta = new Vector2(220f, 68f);

            // Background pill
            Image bg = hudRoot.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.08f, 0.12f, 0.88f);

            Outline outline = hudRoot.AddComponent<Outline>();
            outline.effectColor = new Color(0.2f, 0.6f, 0.9f, 0.45f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            DayNightClockHUD hud = hudRoot.AddComponent<DayNightClockHUD>();

            // Time Text
            GameObject timeGO = new GameObject("TimeText");
            timeGO.transform.SetParent(hudRoot.transform, false);
            RectTransform timeRect = timeGO.AddComponent<RectTransform>();
            timeRect.anchorMin = new Vector2(0f, 0.45f);
            timeRect.anchorMax = new Vector2(1f, 1f);
            timeRect.offsetMin = new Vector2(10f, 0f);
            timeRect.offsetMax = new Vector2(-10f, -4f);

            Text tText = timeGO.AddComponent<Text>();
            tText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tText.fontSize = 22;
            tText.fontStyle = FontStyle.Bold;
            tText.color = new Color(1.0f, 0.88f, 0.40f);
            tText.alignment = TextAnchor.MiddleCenter;
            tText.horizontalOverflow = HorizontalWrapMode.Overflow;
            tText.verticalOverflow = VerticalWrapMode.Overflow;
            hud.timeText = tText;

            // Phase & Day Text
            GameObject phaseGO = new GameObject("PhaseText");
            phaseGO.transform.SetParent(hudRoot.transform, false);
            RectTransform phaseRect = phaseGO.AddComponent<RectTransform>();
            phaseRect.anchorMin = new Vector2(0f, 0f);
            phaseRect.anchorMax = new Vector2(1f, 0.45f);
            phaseRect.offsetMin = new Vector2(10f, 4f);
            phaseRect.offsetMax = new Vector2(-10f, 0f);

            Text pText = phaseGO.AddComponent<Text>();
            pText.font = tText.font;
            pText.fontSize = 13;
            pText.color = new Color(0.75f, 0.90f, 1.0f);
            pText.alignment = TextAnchor.MiddleCenter;
            pText.horizontalOverflow = HorizontalWrapMode.Overflow;
            pText.verticalOverflow = VerticalWrapMode.Overflow;
            hud.phaseText = pText;

            // Mini Button to toggle controls
            GameObject btnGO = new GameObject("ToggleBtn");
            btnGO.transform.SetParent(hudRoot.transform, false);
            RectTransform btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0f, 0f);
            btnRect.anchorMax = new Vector2(1f, 1f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            Button btn = btnGO.AddComponent<Button>();
            Image btnImg = btnGO.AddComponent<Image>();
            btnImg.color = Color.clear;
            hud.toggleControlsButton = btn;

            // Controls Panel (Dropdown below)
            GameObject panelGO = new GameObject("ControlsDropdown");
            panelGO.transform.SetParent(hudRoot.transform, false);
            RectTransform pRect = panelGO.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0f, 0f);
            pRect.anchorMax = new Vector2(1f, 0f);
            pRect.pivot = new Vector2(0.5f, 1f);
            pRect.anchoredPosition = new Vector2(0f, -6f);
            pRect.sizeDelta = new Vector2(0f, 85f);

            Image panelBg = panelGO.AddComponent<Image>();
            panelBg.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
            hud.controlsPanel = panelGO;

            // Time Slider in Controls Panel
            GameObject sliderGO = new GameObject("TimeSlider");
            sliderGO.transform.SetParent(panelGO.transform, false);
            RectTransform sRect = sliderGO.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.1f, 0.6f);
            sRect.anchorMax = new Vector2(0.9f, 0.85f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;

            Slider slider = sliderGO.AddComponent<Slider>();
            GameObject sBg = new GameObject("Background");
            sBg.transform.SetParent(sliderGO.transform, false);
            RectTransform sBgRect = sBg.AddComponent<RectTransform>();
            sBgRect.anchorMin = Vector2.zero;
            sBgRect.anchorMax = Vector2.one;
            sBgRect.sizeDelta = Vector2.zero;
            Image sBgImg = sBg.AddComponent<Image>();
            sBgImg.color = new Color(0.2f, 0.22f, 0.26f);

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGO.transform, false);
            RectTransform fillRect = fillArea.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fRect = fill.AddComponent<RectTransform>();
            fRect.sizeDelta = Vector2.zero;
            Image fImg = fill.AddComponent<Image>();
            fImg.color = new Color(1f, 0.75f, 0.25f);
            slider.fillRect = fRect;

            hud.timeSlider = slider;

            // Speed Buttons row
            CreateSpeedButton(panelGO.transform, "1x", new Vector2(0.08f, 0.15f), new Vector2(0.26f, 0.45f), () => hud.SetSpeed1x());
            CreateSpeedButton(panelGO.transform, "5x", new Vector2(0.30f, 0.15f), new Vector2(0.48f, 0.45f), () => hud.SetSpeed5x());
            CreateSpeedButton(panelGO.transform, "20x", new Vector2(0.52f, 0.15f), new Vector2(0.70f, 0.45f), () => hud.SetSpeed20x());
            CreateSpeedButton(panelGO.transform, "⏸", new Vector2(0.74f, 0.15f), new Vector2(0.92f, 0.45f), () => hud.TogglePause());

            panelGO.SetActive(false);

            return hud;
        }

        private static void CreateSpeedButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax, UnityEngine.Events.UnityAction action)
        {
            GameObject bGO = new GameObject($"Btn_{label}");
            bGO.transform.SetParent(parent, false);
            RectTransform bRect = bGO.AddComponent<RectTransform>();
            bRect.anchorMin = anchorMin;
            bRect.anchorMax = anchorMax;
            bRect.offsetMin = Vector2.zero;
            bRect.offsetMax = Vector2.zero;

            Image img = bGO.AddComponent<Image>();
            img.color = new Color(0.25f, 0.30f, 0.38f);

            Button btn = bGO.AddComponent<Button>();
            btn.onClick.AddListener(action);

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(bGO.transform, false);
            RectTransform tRect = textGO.AddComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            Text txt = textGO.AddComponent<Text>();
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (txt.font == null) txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 11;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;
        }
    }
}
