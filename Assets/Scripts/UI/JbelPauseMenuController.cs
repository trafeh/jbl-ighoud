using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JbelIghoud.UI
{
    /// <summary>
    /// Controller for the authentic Jbel Ighoud Pause Menu:
    /// - Dims the frozen game screen with a dark backdrop.
    /// - Smoothly pops in the Pause Modal Card with authentic pause/bg.png and glowing pill buttons.
    /// - Handles Resume, Map, Setting, Main Menu, and Quit actions.
    /// - Tracks session playtime and displays active quest objective.
    /// - Supports mouse clicks and keyboard (Escape / P) shortcut.
    /// </summary>
    public class JbelPauseMenuController : MonoBehaviour
    {
        private static JbelPauseMenuController _instance;
        public static JbelPauseMenuController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Object.FindAnyObjectByType<JbelPauseMenuController>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        [Header("========================================")]
        [Header("--- MODAL PANELS & CANVAS GROUPS ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup pauseModalCG;
        [SerializeField] private Image dimBackdropImage;
        [SerializeField] private RectTransform pauseCardRT;

        [Header("========================================")]
        [Header("--- TEXT ELEMENTS ---")]
        [Header("========================================")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI questObjectiveText;
        [SerializeField] private TextMeshProUGUI playtimeText;

        [Header("========================================")]
        [Header("--- BUTTONS ---")]
        [Header("========================================")]
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button mapButton;
        [SerializeField] private Button settingButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button quitButton;

        [Header("========================================")]
        [Header("--- CONFIGURABLE PARAMETERS ---")]
        [Header("========================================")]
        [Tooltip("Alpha opacity of the dark dimming backdrop overlay.")]
        [Range(0.2f, 0.95f)]
        [SerializeField] private float dimBackdropAlpha = 0.68f;

        [Tooltip("Duration of the modal fade & card pop animation in seconds.")]
        [SerializeField] private float modalAnimationDuration = 0.22f;

        [Tooltip("Name of the main menu scene to return to.")]
        [SerializeField] private string mainMenuSceneName = "LoadingScene";

        [Tooltip("Default or starting playtime in minutes (e.g. 139 = 2h 19m).")]
        [SerializeField] private float initialPlaytimeMinutes = 139f;

        private bool isPaused = false;
        public bool IsPaused => isPaused;

        private float totalSessionSeconds = 0f;
        private Coroutine modalAnimCoroutine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            totalSessionSeconds = initialPlaytimeMinutes * 60f;

            // Auto-heal serialized references if missing
            if (pauseModalCG == null) pauseModalCG = GetComponent<CanvasGroup>();
            if (dimBackdropImage == null)
            {
                var d = transform.Find("DimBackdrop");
                if (d != null) dimBackdropImage = d.GetComponent<Image>();
            }
            if (pauseCardRT == null)
            {
                var c = transform.Find("PauseCard");
                if (c != null) pauseCardRT = c.GetComponent<RectTransform>();
            }
            if (resumeButton == null && pauseCardRT != null)
            {
                var b = pauseCardRT.Find("ResumeButton");
                if (b != null) resumeButton = b.GetComponent<Button>();
            }
            if (mapButton == null && pauseCardRT != null)
            {
                var b = pauseCardRT.Find("SplitButtonsRow/MapButton");
                if (b != null) mapButton = b.GetComponent<Button>();
            }
            if (settingButton == null && pauseCardRT != null)
            {
                var b = pauseCardRT.Find("SplitButtonsRow/SettingButton");
                if (b != null) settingButton = b.GetComponent<Button>();
            }
            if (mainMenuButton == null && pauseCardRT != null)
            {
                var b = pauseCardRT.Find("MainMenuButton");
                if (b != null) mainMenuButton = b.GetComponent<Button>();
            }
            if (quitButton == null && pauseCardRT != null)
            {
                var b = pauseCardRT.Find("QuitButton");
                if (b != null) quitButton = b.GetComponent<Button>();
            }
            if (titleText == null && pauseCardRT != null)
            {
                var t = pauseCardRT.Find("TitleText");
                if (t != null) titleText = t.GetComponent<TextMeshProUGUI>();
            }
            if (questObjectiveText == null && pauseCardRT != null)
            {
                var t = pauseCardRT.Find("QuestText");
                if (t != null) questObjectiveText = t.GetComponent<TextMeshProUGUI>();
            }
            if (playtimeText == null && pauseCardRT != null)
            {
                var t = pauseCardRT.Find("PlaytimeText");
                if (t != null) playtimeText = t.GetComponent<TextMeshProUGUI>();
            }

            // Wire button click listeners
            if (resumeButton != null) { resumeButton.onClick.RemoveListener(ResumeGame); resumeButton.onClick.AddListener(ResumeGame); }
            if (mapButton != null) { mapButton.onClick.RemoveListener(OnMapClicked); mapButton.onClick.AddListener(OnMapClicked); }
            if (settingButton != null) { settingButton.onClick.RemoveListener(OnSettingClicked); settingButton.onClick.AddListener(OnSettingClicked); }
            if (mainMenuButton != null) { mainMenuButton.onClick.RemoveListener(ReturnToMainMenu); mainMenuButton.onClick.AddListener(ReturnToMainMenu); }
            if (quitButton != null) { quitButton.onClick.RemoveListener(QuitGame); quitButton.onClick.AddListener(QuitGame); }
        }

        private void Start()
        {
            // Keep GameObject active so Awake and Update run, but set alpha to 0 and disable raycasts
            if (pauseModalCG != null)
            {
                pauseModalCG.alpha = 0f;
                pauseModalCG.blocksRaycasts = false;
                pauseModalCG.interactable = false;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = 0f;
                dimBackdropImage.color = c;
                dimBackdropImage.raycastTarget = false;
            }
            if (pauseCardRT != null)
            {
                pauseCardRT.localScale = Vector3.one * 0.92f;
            }
        }

        private void Update()
        {
            // Track real unscaled playtime
            totalSessionSeconds += Time.unscaledDeltaTime;

            // Keep solar flare strokes shimmering even during freeze/timeScale=0
            Shader.SetGlobalFloat("_UnscaledTime", Time.unscaledTime);

            // Safe dual-input keyboard shortcut to toggle pause (Escape or P)
            bool pauseToggleRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                pauseToggleRequested = Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (!pauseToggleRequested)
            {
                try
                {
                    pauseToggleRequested = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
                }
                catch { }
            }
#endif

            if (pauseToggleRequested)
            {
                if (isPaused)
                {
                    ResumeGame();
                }
                else
                {
                    PauseGame();
                }
            }
        }

        public void PauseGame()
        {
            if (isPaused) return;
            isPaused = true;
            Time.timeScale = 0f;

            // Explicitly lock camera and player controls
            var touchCtrl = Object.FindAnyObjectByType<MobileFreeLookTouchController>();
            if (touchCtrl != null) touchCtrl.isInputDisabled = true;

            var player = Object.FindAnyObjectByType<TopDownPlayerController>();
            if (player != null) player.SetControlsLocked(true);

            // Update quest objective text
            if (questObjectiveText != null)
            {
                questObjectiveText.text = "Find 100 pieces of gold";
            }

            // Update formatted playtime
            UpdatePlaytimeDisplay();

            // Animate modal open
            if (modalAnimCoroutine != null) StopCoroutine(modalAnimCoroutine);
            modalAnimCoroutine = StartCoroutine(AnimateModalRoutine(true));

            Debug.Log("<color=gold>[JbelPause] Game Paused.</color>");
        }

        public void ResumeGame()
        {
            if (!isPaused) return;
            isPaused = false;
            Time.timeScale = 1f;

            // Re-enable camera and player controls
            var touchCtrl = Object.FindAnyObjectByType<MobileFreeLookTouchController>();
            if (touchCtrl != null) touchCtrl.isInputDisabled = false;

            var player = Object.FindAnyObjectByType<TopDownPlayerController>();
            if (player != null) player.SetControlsLocked(false);

            // Animate modal close
            if (modalAnimCoroutine != null) StopCoroutine(modalAnimCoroutine);
            modalAnimCoroutine = StartCoroutine(AnimateModalRoutine(false));

            Debug.Log("<color=green>[JbelPause] Game Resumed.</color>");
        }

        private IEnumerator AnimateModalRoutine(bool opening)
        {
            if (pauseModalCG == null) yield break;

            if (opening)
            {
                pauseModalCG.blocksRaycasts = true;
                pauseModalCG.interactable = true;
                if (dimBackdropImage != null) dimBackdropImage.raycastTarget = true;
            }

            float t = 0f;
            float startAlpha = pauseModalCG.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startCardScale = (pauseCardRT != null) ? pauseCardRT.localScale : Vector3.one;
            Vector3 targetCardScale = opening ? Vector3.one : Vector3.one * 0.92f;

            while (t < modalAnimationDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / modalAnimationDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);

                pauseModalCG.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothP);

                if (dimBackdropImage != null)
                {
                    Color c = dimBackdropImage.color;
                    c.a = Mathf.Lerp(0f, dimBackdropAlpha, opening ? smoothP : (1f - smoothP));
                    dimBackdropImage.color = c;
                }

                if (pauseCardRT != null)
                {
                    pauseCardRT.localScale = Vector3.Lerp(startCardScale, targetCardScale, smoothP);
                }

                yield return null;
            }

            pauseModalCG.alpha = targetAlpha;

            if (!opening)
            {
                pauseModalCG.blocksRaycasts = false;
                pauseModalCG.interactable = false;
                if (dimBackdropImage != null) dimBackdropImage.raycastTarget = false;
            }
        }

        private void UpdatePlaytimeDisplay()
        {
            if (playtimeText == null) return;
            int totalMinutes = Mathf.FloorToInt(totalSessionSeconds / 60f);
            int hours = totalMinutes / 60;
            int mins = totalMinutes % 60;
            playtimeText.text = $"Playtime {hours}h {mins:D2}m";
        }

        private void OnMapClicked()
        {
            Debug.Log("[JbelPause] Map clicked from Pause Menu.");
            ResumeGame();
        }

        private void OnSettingClicked()
        {
            Debug.Log("[JbelPause] Setting clicked from Pause Menu.");
        }

        public void ReturnToMainMenu()
        {
            Debug.Log("[JbelPause] Returning to Main Menu...");
            Time.timeScale = 1f;
            SceneManager.LoadScene(mainMenuSceneName);
        }

        public void QuitGame()
        {
            Debug.Log("[JbelPause] Quitting game...");
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --- Editor Preview Helpers ---
        public void ApplyPreviewOpenState()
        {
            if (pauseModalCG != null)
            {
                pauseModalCG.alpha = 1f;
                pauseModalCG.blocksRaycasts = true;
                pauseModalCG.interactable = true;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = dimBackdropAlpha;
                dimBackdropImage.color = c;
            }
            if (pauseCardRT != null)
            {
                pauseCardRT.localScale = Vector3.one;
            }
            UpdatePlaytimeDisplay();
        }

        public void ApplyPreviewClosedState()
        {
            if (pauseModalCG != null)
            {
                pauseModalCG.alpha = 0f;
                pauseModalCG.blocksRaycasts = false;
                pauseModalCG.interactable = false;
            }
            if (dimBackdropImage != null)
            {
                Color c = dimBackdropImage.color;
                c.a = 0f;
                dimBackdropImage.color = c;
            }
            if (pauseCardRT != null)
            {
                pauseCardRT.localScale = Vector3.one * 0.92f;
            }
        }
    }
}
