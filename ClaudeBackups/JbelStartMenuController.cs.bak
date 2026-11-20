using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Master Controller for the Jbel Ighoud Game Startup Experience:
    /// - 100% respects designer placements in the scene (no hardcoded overrides).
    /// - Dynamic rotating tips during loading.
    /// - Hyper-speed dive transition into the cave handprint.
    /// - Seamless reveal of the Start Game Menu with animated Solar-Flare buttons.
    /// - Fully inspector-controllable interactive button click & hover animations:
    ///     * Continue: Hand wave animation before entering game.
    ///     * Mode Survie: Spears disappear, reappear separated, fast strike clash with intense impact screen shake!
    ///     * Option: Pointer enter / hover or click makes the gear turn.
    /// </summary>
    public class JbelStartMenuController : MonoBehaviour
    {
        private static JbelStartMenuController _instance;
        public static JbelStartMenuController Instance => _instance;

        public enum FlowState
        {
            Loading,
            FocusHandprint,
            Diving,
            StartMenu,
            TransitioningToGame
        }

        [Header("========================================")]
        [Header("--- FLOW & SCENE CONFIGURATION ---")]
        [Header("========================================")]
        [SerializeField] private FlowState currentState = FlowState.Loading;
        [Tooltip("Name of the scene loaded upon pressing Continue or Mode Survie.")]
        [SerializeField] private string targetSceneName = "SampleScene";
        [Tooltip("Minimum loading time in seconds.")]
        [SerializeField] private float minimumLoadingTime = 2.5f;
        [Tooltip("Fade to black duration when entering game.")]
        [SerializeField] private float fadeToBlackDuration = 0.45f;

        [Header("========================================")]
        [Header("--- UI PANELS & CANVAS GROUPS ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup loadingPanelCG;
        [SerializeField] private CanvasGroup startMenuPanelCG;
        [SerializeField] private CanvasGroup flashOverlayCG;

        [Header("========================================")]
        [Header("--- LOADING ELEMENTS ---")]
        [Header("========================================")]
        [SerializeField] private Image progressBarFill;
        [SerializeField] private TextMeshProUGUI progressPercentText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private CanvasGroup tipCanvasGroup;

        [Header("========================================")]
        [Header("--- HANDPRINT ARTWORK (LOADING & DIVE) ---")]
        [Header("========================================")]
        [SerializeField] private RectTransform artworkRT;
        [SerializeField] private CanvasGroup artworkCG;
        [Tooltip("Duration of hyper-speed dive zoom in seconds.")]
        [SerializeField] private float diveDuration = 0.45f;
        [Tooltip("Max scale multiplier reached at the peak of the dive.")]
        [SerializeField] private float maxDiveScale = 18f;

        [Header("========================================")]
        [Header("--- MENU BUTTONS ---")]
        [Header("========================================")]
        [SerializeField] private RectTransform menuButtonsContainerRT;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button modeSurvieButton;
        [SerializeField] private Button optionButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button arButton;

        [Header("========================================")]
        [Header("--- INTERACTIVE ICON REFERENCES ---")]
        [Header("========================================")]
        [SerializeField] private RectTransform continueHandIconRT;
        [SerializeField] private RectTransform spearLeftRT;
        [SerializeField] private RectTransform spearRightRT;
        [SerializeField] private RectTransform optionGearIconRT;

        [Header("========================================")]
        [Header("--- CONTINUE BUTTON (HAND WAVE) SETTINGS ---")]
        [Header("========================================")]
        [Tooltip("Total duration of the waving hand animation.")]
        [SerializeField] private float handWaveDuration = 0.65f;
        [Tooltip("Maximum tilt angle in degrees during the wave.")]
        [SerializeField] private float handWaveMaxAngle = 26f;
        [Tooltip("Number of wave back-and-forth oscillation cycles.")]
        [SerializeField] private float handWaveCycles = 3f;
        [Tooltip("Scale punch amount added to the hand during the wave (0.25 = +25% size).")]
        [SerializeField] private float handWaveScalePunch = 0.25f;

        [Header("========================================")]
        [Header("--- MODE SURVIE (SPEARS CLASH & SHAKE) SETTINGS ---")]
        [Header("========================================")]
        [Tooltip("How fast the crossed spears disappear when clicked before reappearing separated.")]
        [SerializeField] private float spearDisappearDuration = 0.08f;
        [Tooltip("Horizontal spawn offset when spears reappear separated (X axis in UI pixels).")]
        [SerializeField] private float spearSpawnDistanceX = 140f;
        [Tooltip("Vertical spawn offset when spears reappear separated (Y axis in UI pixels).")]
        [SerializeField] private float spearSpawnDistanceY = 45f;
        [Tooltip("Anticipation pause in seconds after appearing separated before striking.")]
        [SerializeField] private float spearAnticipationPause = 0.06f;
        [Tooltip("Duration of the fast strike where spears slam together into the center.")]
        [SerializeField] private float spearStrikeDuration = 0.11f;
        [Tooltip("Strength/intensity of the impact shake on camera and buttons.")]
        [SerializeField] private float spearImpactShakeIntensity = 20f;
        [Tooltip("Duration of the impact shake in seconds.")]
        [SerializeField] private float spearImpactShakeDuration = 0.30f;
        [Tooltip("Scale punch multiplier on the button upon spear impact.")]
        [SerializeField] private float spearButtonPunchScale = 1.14f;
        [Tooltip("Pause duration after the hit before fading to game.")]
        [SerializeField] private float spearPostHitPause = 0.22f;

        [Header("========================================")]
        [Header("--- OPTION BUTTON (GEAR SPIN) SETTINGS ---")]
        [Header("========================================")]
        [Tooltip("Duration of gear spin when mouse enters/hovers.")]
        [SerializeField] private float gearHoverSpinDuration = 0.55f;
        [Tooltip("Degrees the gear rotates on mouse hover.")]
        [SerializeField] private float gearHoverSpinDegrees = 360f;
        [Tooltip("Duration of gear spin when clicked.")]
        [SerializeField] private float gearClickSpinDuration = 0.85f;
        [Tooltip("Degrees the gear rotates on click.")]
        [SerializeField] private float gearClickSpinDegrees = 720f;

        [Header("========================================")]
        [Header("--- DYNAMIC TIPS ---")]
        [Header("========================================")]
        [SerializeField] private string[] tipsList = new string[] {
            "TIPS: HOLD RUN BUTTON TO SPRINT ACROSS THE TERRAIN.",
            "TIPS: SEARCH ROCKS AND CAVES TO DISCOVER ANCIENT FLINT TOOLS.",
            "TIPS: MONITOR YOUR FOOD, WATER, AND STAMINA TO SURVIVE IN THE WILD.",
            "TIPS: USE YOUR SATCHEL TO BALANCE RESOURCE WEIGHT AND STORAGE.",
            "TIPS: JBEL IGHOUD IS HOME TO THE OLDEST HOMO SAPIENS REMAINS IN HISTORY."
        };
        [SerializeField] private float tipCycleInterval = 2.5f;

        private Vector2 initialLoadingArtPos;
        private Vector2 initialLoadingArtSize;
        private AsyncOperation preloadSceneOp;
        private int currentTipIndex = 0;
        private bool isTransitioning = false;
        private Coroutine gearSpinCoroutine;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            // Cache initial loading art placement without modifying designer coordinates
            if (artworkRT != null)
            {
                initialLoadingArtPos = artworkRT.anchoredPosition;
                initialLoadingArtSize = artworkRT.sizeDelta;
            }

            // Wire button click listeners
            if (continueButton != null) continueButton.onClick.AddListener(OnContinueClicked);
            if (modeSurvieButton != null) modeSurvieButton.onClick.AddListener(OnModeSurvieClicked);
            if (optionButton != null)
            {
                optionButton.onClick.AddListener(OnOptionClicked);
                var hover = optionButton.GetComponent<MenuHoverTrigger>();
                if (hover == null) hover = optionButton.gameObject.AddComponent<MenuHoverTrigger>();
                hover.onHoverEnter = OnOptionHoverEnter;
            }
            if (creditsButton != null) creditsButton.onClick.AddListener(OnCreditsClicked);
            if (arButton != null) arButton.onClick.AddListener(OnARClicked);
        }

        private void Start()
        {
            StartCoroutine(StartupSequenceRoutine());
        }

        private IEnumerator StartupSequenceRoutine()
        {
            currentState = FlowState.Loading;
            isTransitioning = false;

            // Ensure initial visibility states without modifying any user-set background colors/opacities
            if (loadingPanelCG != null) { loadingPanelCG.alpha = 1f; loadingPanelCG.gameObject.SetActive(true); }
            if (startMenuPanelCG != null) { startMenuPanelCG.alpha = 0f; startMenuPanelCG.gameObject.SetActive(false); }
            if (flashOverlayCG != null) { flashOverlayCG.alpha = 0f; flashOverlayCG.gameObject.SetActive(false); }

            // Loading artwork in initial position
            if (artworkRT != null)
            {
                artworkRT.gameObject.SetActive(true);
                artworkRT.anchoredPosition = initialLoadingArtPos;
                artworkRT.sizeDelta = initialLoadingArtSize;
                artworkRT.localScale = Vector3.one;
            }
            if (artworkCG != null) artworkCG.alpha = 1f;

            // Start rotating tips
            Coroutine tipsCoroutine = StartCoroutine(CycleTipsRoutine());

            // Preload gameplay scene asynchronously in background
            try
            {
                preloadSceneOp = SceneManager.LoadSceneAsync(targetSceneName);
                if (preloadSceneOp != null)
                {
                    preloadSceneOp.allowSceneActivation = false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[JbelStartup] Async preload note: {ex.Message}");
            }

            // Animate progress bar from 0% to 100%
            float elapsed = 0f;
            float displayedProgress = 0f;

            while (displayedProgress < 1f)
            {
                elapsed += Time.deltaTime;
                float timeProgress = Mathf.Clamp01(elapsed / minimumLoadingTime);
                float realProgress = (preloadSceneOp != null) ? Mathf.Clamp01(preloadSceneOp.progress / 0.9f) : 1f;
                float target = Mathf.Min(timeProgress, realProgress);

                displayedProgress = Mathf.MoveTowards(displayedProgress, target, Time.deltaTime * 1.6f);

                if (progressBarFill != null) progressBarFill.fillAmount = displayedProgress;
                if (progressPercentText != null) progressPercentText.text = $"{Mathf.RoundToInt(displayedProgress * 100f)}%";

                if (elapsed >= minimumLoadingTime && (preloadSceneOp == null || preloadSceneOp.progress >= 0.9f))
                {
                    displayedProgress = 1f;
                    if (progressBarFill != null) progressBarFill.fillAmount = 1f;
                    if (progressPercentText != null) progressPercentText.text = "100%";
                    break;
                }

                yield return null;
            }

            yield return new WaitForSeconds(0.2f);
            if (tipsCoroutine != null) StopCoroutine(tipsCoroutine);

            // ==========================================
            // PHASE 1: FADE OUT LOADING BAR & TIPS
            // ==========================================
            currentState = FlowState.FocusHandprint;
            float fadeTime = 0.35f;
            float t = 0f;
            while (t < fadeTime)
            {
                t += Time.deltaTime;
                if (loadingPanelCG != null) loadingPanelCG.alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
                yield return null;
            }
            if (loadingPanelCG != null)
            {
                loadingPanelCG.alpha = 0f;
                loadingPanelCG.gameObject.SetActive(false);
            }

            // Brief moment where ONLY the handprint is visible
            yield return new WaitForSeconds(0.25f);

            // ==========================================
            // PHASE 2: HYPER-SPEED DIVE ZOOM
            // ==========================================
            currentState = FlowState.Diving;
            if (flashOverlayCG != null) flashOverlayCG.gameObject.SetActive(true);

            float diveTimer = 0f;
            while (diveTimer < diveDuration)
            {
                diveTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(diveTimer / diveDuration);

                // Exponential fast dive curve (EaseInExpo)
                float diveFactor = Mathf.Pow(progress, 3.5f);
                float currentScale = Mathf.Lerp(1f, maxDiveScale, diveFactor);

                if (artworkRT != null)
                {
                    artworkRT.localScale = new Vector3(currentScale, currentScale, 1f);
                }

                // Solar flash flare-up as we reach the core of the hand
                if (progress > 0.40f && flashOverlayCG != null)
                {
                    float flashT = (progress - 0.40f) / 0.60f;
                    flashOverlayCG.alpha = Mathf.Sin(flashT * Mathf.PI);
                }

                yield return null;
            }

            // Peak of the dive: hide loading artwork as the Start Game Menu takes over
            if (artworkCG != null) artworkCG.alpha = 0f;
            if (artworkRT != null)
            {
                artworkRT.localScale = Vector3.one;
                artworkRT.gameObject.SetActive(false);
            }

            // ==========================================
            // PHASE 3: REVEAL START GAME MENU
            // ==========================================
            currentState = FlowState.StartMenu;
            if (startMenuPanelCG != null)
            {
                startMenuPanelCG.gameObject.SetActive(true);
                startMenuPanelCG.alpha = 0f;
            }

            float revealTime = 0.5f;
            t = 0f;
            while (t < revealTime)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / revealTime);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);

                if (flashOverlayCG != null)
                {
                    flashOverlayCG.alpha = Mathf.Lerp(flashOverlayCG.alpha, 0f, p);
                }

                if (startMenuPanelCG != null)
                {
                    startMenuPanelCG.alpha = smoothP;
                }

                yield return null;
            }

            if (flashOverlayCG != null)
            {
                flashOverlayCG.alpha = 0f;
                flashOverlayCG.gameObject.SetActive(false);
            }
            if (startMenuPanelCG != null) startMenuPanelCG.alpha = 1f;

            Debug.Log("<color=gold><b>[JbelStartup]</b> Transition complete! Start Game Menu is active.</color>");
        }

        private IEnumerator CycleTipsRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(tipCycleInterval);
            while (currentState == FlowState.Loading)
            {
                yield return wait;

                float t = 0f;
                while (t < 0.25f && tipCanvasGroup != null)
                {
                    t += Time.deltaTime;
                    tipCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.25f);
                    yield return null;
                }

                currentTipIndex = (currentTipIndex + 1) % tipsList.Length;
                if (tipText != null) tipText.text = tipsList[currentTipIndex];

                t = 0f;
                while (t < 0.25f && tipCanvasGroup != null)
                {
                    t += Time.deltaTime;
                    tipCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.25f);
                    yield return null;
                }
            }
        }

        // ==========================================
        // BUTTON CLICKS & INTERACTIVE ANIMATIONS
        // ==========================================

        public void OnContinueClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Debug.Log("[JbelStartup] Continue clicked -> Triggering Hand Wave animation!");
            StartCoroutine(AnimateHandWaveAndTransition());
        }

        private IEnumerator AnimateHandWaveAndTransition()
        {
            if (continueHandIconRT != null)
            {
                Vector3 initialScale = continueHandIconRT.localScale;
                Quaternion initialRot = continueHandIconRT.localRotation;

                float t = 0f;
                while (t < handWaveDuration)
                {
                    t += Time.deltaTime;
                    float p = t / handWaveDuration;

                    // Organic friendly hand wave: oscillations decaying with easing
                    float waveAngle = Mathf.Sin(p * Mathf.PI * 2f * handWaveCycles) * Mathf.Lerp(handWaveMaxAngle, 0f, p);
                    float popScale = 1f + Mathf.Sin(p * Mathf.PI) * handWaveScalePunch;

                    continueHandIconRT.localRotation = Quaternion.Euler(0f, 0f, waveAngle);
                    continueHandIconRT.localScale = initialScale * popScale;
                    yield return null;
                }

                continueHandIconRT.localRotation = initialRot;
                continueHandIconRT.localScale = initialScale;
            }

            yield return TransitionToGameRoutine();
        }

        public void OnModeSurvieClicked()
        {
            if (isTransitioning) return;
            isTransitioning = true;
            Debug.Log("[JbelStartup] Mode Survie clicked -> Triggering Spears Strike & Clash action!");
            StartCoroutine(AnimateSpearsClashAndTransition());
        }

        private IEnumerator AnimateSpearsClashAndTransition()
        {
            if (spearLeftRT != null && spearRightRT != null)
            {
                Vector2 leftCenterPos = spearLeftRT.anchoredPosition;
                Vector2 rightCenterPos = spearRightRT.anchoredPosition;

                CanvasGroup leftCG = spearLeftRT.GetComponent<CanvasGroup>();
                if (leftCG == null) leftCG = spearLeftRT.gameObject.AddComponent<CanvasGroup>();
                CanvasGroup rightCG = spearRightRT.GetComponent<CanvasGroup>();
                if (rightCG == null) rightCG = spearRightRT.gameObject.AddComponent<CanvasGroup>();

                // ==========================================
                // STEP 1: MAKE THEM DISAPPEAR
                // ==========================================
                float t = 0f;
                while (t < spearDisappearDuration)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / spearDisappearDuration);
                    leftCG.alpha = 1f - p;
                    rightCG.alpha = 1f - p;
                    spearLeftRT.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, p);
                    spearRightRT.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, p);
                    yield return null;
                }
                leftCG.alpha = 0f;
                rightCG.alpha = 0f;

                // ==========================================
                // STEP 2: POSITION THEM SEPARATED
                // ==========================================
                Vector2 leftSpawnPos = leftCenterPos + new Vector2(-spearSpawnDistanceX, spearSpawnDistanceY);
                Vector2 rightSpawnPos = rightCenterPos + new Vector2(spearSpawnDistanceX, spearSpawnDistanceY);

                spearLeftRT.anchoredPosition = leftSpawnPos;
                spearRightRT.anchoredPosition = rightSpawnPos;

                // ==========================================
                // STEP 3: APPEAR SEPARATED
                // ==========================================
                leftCG.alpha = 1f;
                rightCG.alpha = 1f;
                spearLeftRT.localScale = Vector3.one * 1.15f;
                spearRightRT.localScale = Vector3.one * 1.15f;

                if (spearAnticipationPause > 0f)
                {
                    yield return new WaitForSeconds(spearAnticipationPause);
                }

                // ==========================================
                // STEP 4: FAST STRIKE! HIT EACH OTHER!
                // ==========================================
                t = 0f;
                while (t < spearStrikeDuration)
                {
                    t += Time.deltaTime;
                    float p = t / spearStrikeDuration;
                    // High acceleration ease
                    float easeP = p * p * p;

                    spearLeftRT.anchoredPosition = Vector2.Lerp(leftSpawnPos, leftCenterPos, easeP);
                    spearRightRT.anchoredPosition = Vector2.Lerp(rightSpawnPos, rightCenterPos, easeP);
                    spearLeftRT.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one, p);
                    spearRightRT.localScale = Vector3.Lerp(Vector3.one * 1.15f, Vector3.one, p);
                    yield return null;
                }

                spearLeftRT.anchoredPosition = leftCenterPos;
                spearRightRT.anchoredPosition = rightCenterPos;
                spearLeftRT.localScale = Vector3.one;
                spearRightRT.localScale = Vector3.one;

                // ==========================================
                // STEP 5: IMPACT PUNCH & INTENSE SHAKE
                // ==========================================
                if (modeSurvieButton != null)
                {
                    StartCoroutine(PunchButtonScale(modeSurvieButton.transform, spearButtonPunchScale, 0.14f));
                }

                // Screen and camera shake!
                yield return StartCoroutine(DoImpactShake(spearImpactShakeIntensity, spearImpactShakeDuration));

                // Savor the hit impact
                if (spearPostHitPause > 0f)
                {
                    yield return new WaitForSeconds(spearPostHitPause);
                }
            }

            yield return TransitionToGameRoutine();
        }

        private IEnumerator DoImpactShake(float intensity, float duration)
        {
            Camera cam = Camera.main;
            Vector3 camOrigPos = (cam != null) ? cam.transform.localPosition : Vector3.zero;
            Vector2 containerOrigPos = (menuButtonsContainerRT != null) ? menuButtonsContainerRT.anchoredPosition : Vector2.zero;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                // Decaying vibration curve
                float decay = 1f - Mathf.Pow(progress, 1.4f);
                float currentStrength = intensity * decay;

                Vector2 randomOffset = Random.insideUnitCircle * currentStrength;

                // Shake UI buttons container
                if (menuButtonsContainerRT != null)
                {
                    menuButtonsContainerRT.anchoredPosition = containerOrigPos + randomOffset;
                }

                // Shake Camera
                if (cam != null)
                {
                    Vector3 camOffset = new Vector3(randomOffset.x, randomOffset.y, 0f) * 0.012f;
                    cam.transform.localPosition = camOrigPos + camOffset;
                }

                yield return null;
            }

            if (menuButtonsContainerRT != null) menuButtonsContainerRT.anchoredPosition = containerOrigPos;
            if (cam != null) cam.transform.localPosition = camOrigPos;
        }

        private IEnumerator PunchButtonScale(Transform target, float punchScale, float duration)
        {
            if (target == null) yield break;
            Vector3 orig = target.localScale;
            float half = duration * 0.4f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(orig, orig * punchScale, t / half);
                yield return null;
            }
            t = 0f;
            float secondHalf = duration - half;
            while (t < secondHalf)
            {
                t += Time.deltaTime;
                target.localScale = Vector3.Lerp(orig * punchScale, orig, t / secondHalf);
                yield return null;
            }
            target.localScale = orig;
        }

        public void OnOptionHoverEnter()
        {
            TriggerGearSpin(gearHoverSpinDuration, gearHoverSpinDegrees);
        }

        public void OnOptionClicked()
        {
            Debug.Log("[JbelStartup] Option clicked -> Rapid gear ratchet spin!");
            TriggerGearSpin(gearClickSpinDuration, gearClickSpinDegrees);
        }

        public void TriggerGearSpin(float duration, float totalDegrees)
        {
            if (optionGearIconRT == null) return;
            if (gearSpinCoroutine != null) StopCoroutine(gearSpinCoroutine);
            gearSpinCoroutine = StartCoroutine(SpinGearRoutine(duration, totalDegrees));
        }

        private IEnumerator SpinGearRoutine(float duration, float totalDegrees)
        {
            float t = 0f;
            float startAngle = optionGearIconRT.localEulerAngles.z;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);
                optionGearIconRT.localRotation = Quaternion.Euler(0f, 0f, startAngle - smoothP * totalDegrees);
                yield return null;
            }
        }

        public void OnCreditsClicked()
        {
            Debug.Log("[JbelStartup] Credits clicked!");
        }

        public void OnARClicked()
        {
            Debug.Log("[JbelStartup] AR language/badge clicked!");
        }

        private IEnumerator TransitionToGameRoutine()
        {
            currentState = FlowState.TransitioningToGame;

            if (flashOverlayCG != null)
            {
                flashOverlayCG.gameObject.SetActive(true);
                Image img = flashOverlayCG.GetComponent<Image>();
                if (img != null) img.color = Color.black;

                float t = 0f;
                while (t < fadeToBlackDuration)
                {
                    t += Time.deltaTime;
                    flashOverlayCG.alpha = Mathf.Lerp(0f, 1f, t / fadeToBlackDuration);
                    yield return null;
                }
            }

            if (preloadSceneOp != null)
            {
                preloadSceneOp.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }
        }

        // --- Editor Preview Helpers ---
        public void ApplyLoadingPreviewState()
        {
            if (loadingPanelCG != null) { loadingPanelCG.alpha = 1f; loadingPanelCG.gameObject.SetActive(true); }
            if (startMenuPanelCG != null) { startMenuPanelCG.alpha = 0f; startMenuPanelCG.gameObject.SetActive(false); }
            if (artworkRT != null)
            {
                artworkRT.gameObject.SetActive(true);
                artworkRT.localScale = Vector3.one;
            }
            if (artworkCG != null) artworkCG.alpha = 1f;
        }

        public void ApplyMenuPreviewState()
        {
            if (loadingPanelCG != null) { loadingPanelCG.alpha = 0f; loadingPanelCG.gameObject.SetActive(false); }
            if (startMenuPanelCG != null) { startMenuPanelCG.alpha = 1f; startMenuPanelCG.gameObject.SetActive(true); }
            if (artworkRT != null)
            {
                artworkRT.gameObject.SetActive(false);
            }
            if (artworkCG != null) artworkCG.alpha = 0f;
        }
    }
}
