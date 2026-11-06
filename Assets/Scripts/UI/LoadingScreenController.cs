using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Dedicated Asynchronous Loading Scene Controller for Jbel Ighoud.
    /// Matched precisely to the visual reference:
    /// - Full cinematic prehistoric background (Assets/UI/loading bg.png)
    /// - Handprint cave art centered
    /// - Progress bar directly beneath the artwork
    /// - Dynamic rotating gameplay & lore tips beneath the bar
    /// - Studio logo in bottom-left corner
    /// - Version display in bottom-right corner
    /// </summary>
    public class LoadingScreenController : MonoBehaviour
    {
        private static LoadingScreenController _instance;
        public static LoadingScreenController Instance => _instance;

        [Header("--- UI References ---")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image artworkImage;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private TextMeshProUGUI progressPercentText;
        [SerializeField] private TextMeshProUGUI tipText;
        [SerializeField] private CanvasGroup tipCanvasGroup;
        [SerializeField] private Image logoImage;
        [SerializeField] private TextMeshProUGUI versionText;

        [Header("--- Scene Loading Configuration ---")]
        [Tooltip("The target scene to load asynchronously.")]
        [SerializeField] private string targetSceneName = "SampleScene";

        [Tooltip("Minimum duration in seconds to display loading screen so player can admire the artwork and read tips.")]
        [SerializeField] private float minimumDisplayTime = 3.0f;

        [Tooltip("Fade out speed in seconds when loading finishes.")]
        [SerializeField] private float fadeOutDuration = 0.6f;

        [Tooltip("Whether to automatically begin async loading on Start.")]
        [SerializeField] private bool autoLoadOnStart = true;

        [Header("--- Dynamic Tips System ---")]
        [Tooltip("Enable or disable tips display.")]
        [SerializeField] private bool showTips = true;

        [Tooltip("Interval in seconds between rotating tips.")]
        [SerializeField] private float tipCycleInterval = 2.5f;

        [SerializeField] private string[] tipsList = new string[] {
            "TIPS: HOLD RUN BUTTON TO SPRINT ACROSS THE TERRAIN.",
            "TIPS: SEARCH ROCKS AND CAVES TO DISCOVER ANCIENT FLINT TOOLS.",
            "TIPS: MONITOR YOUR FOOD, WATER, AND STAMINA TO SURVIVE IN THE WILD.",
            "TIPS: USE YOUR SATCHEL TO BALANCE RESOURCE WEIGHT AND STORAGE.",
            "TIPS: JBEL IGHOUD IS HOME TO THE OLDEST HOMO SAPIENS REMAINS IN HISTORY."
        };

        private bool isLoadingComplete = false;
        private float displayedProgress = 0f;
        private int currentTipIndex = 0;
        private Coroutine tipCycleCoroutine;

        public bool IsLoadingComplete => isLoadingComplete;
        public float Progress => displayedProgress;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            if (tipText != null)
            {
                tipText.gameObject.SetActive(showTips);
                if (showTips && tipsList != null && tipsList.Length > 0)
                {
                    currentTipIndex = Random.Range(0, tipsList.Length);
                    tipText.text = tipsList[currentTipIndex];
                }
            }
        }

        private void Start()
        {
            if (showTips && tipText != null && tipsList != null && tipsList.Length > 1)
            {
                tipCycleCoroutine = StartCoroutine(CycleTipsRoutine());
            }

            if (autoLoadOnStart && !string.IsNullOrEmpty(targetSceneName))
            {
                StartCoroutine(LoadSceneAsyncRoutine());
            }
        }

        private IEnumerator CycleTipsRoutine()
        {
            WaitForSeconds wait = new WaitForSeconds(tipCycleInterval);
            while (!isLoadingComplete)
            {
                yield return wait;

                if (tipCanvasGroup != null)
                {
                    // Fade out current tip
                    float t = 0f;
                    while (t < 0.25f)
                    {
                        t += Time.deltaTime;
                        tipCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / 0.25f);
                        yield return null;
                    }
                }

                // Switch to next tip
                currentTipIndex = (currentTipIndex + 1) % tipsList.Length;
                if (tipText != null)
                {
                    tipText.text = tipsList[currentTipIndex];
                }

                if (tipCanvasGroup != null)
                {
                    // Fade in new tip
                    float t = 0f;
                    while (t < 0.25f)
                    {
                        t += Time.deltaTime;
                        tipCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / 0.25f);
                        yield return null;
                    }
                }
            }
        }

        private IEnumerator LoadSceneAsyncRoutine()
        {
            displayedProgress = 0f;
            if (progressBarFill != null) progressBarFill.fillAmount = 0f;
            if (progressPercentText != null) progressPercentText.text = "0%";

            float timer = 0f;

            // Start loading target scene in background
            AsyncOperation asyncOp = null;
            try
            {
                asyncOp = SceneManager.LoadSceneAsync(targetSceneName);
                if (asyncOp != null)
                {
                    asyncOp.allowSceneActivation = false;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[LoadingScreen] Could not load scene '{targetSceneName}' asynchronously: {ex.Message}");
            }

            // Smooth progress interpolation
            while (displayedProgress < 1f)
            {
                timer += Time.deltaTime;

                float targetRealProgress = 1f;
                if (asyncOp != null)
                {
                    // In Unity, 0.9 = finished loading scene assets into memory
                    targetRealProgress = Mathf.Clamp01(asyncOp.progress / 0.9f);
                }

                float timeProgress = Mathf.Clamp01(timer / minimumDisplayTime);
                float target = Mathf.Min(targetRealProgress, timeProgress);

                displayedProgress = Mathf.MoveTowards(displayedProgress, target, Time.deltaTime * 1.5f);

                if (progressBarFill != null)
                {
                    progressBarFill.fillAmount = displayedProgress;
                }

                if (progressPercentText != null)
                {
                    int pct = Mathf.RoundToInt(displayedProgress * 100f);
                    progressPercentText.text = $"{pct}%";
                }

                if (timer >= minimumDisplayTime && (asyncOp == null || asyncOp.progress >= 0.9f))
                {
                    displayedProgress = 1f;
                    if (progressBarFill != null) progressBarFill.fillAmount = 1f;
                    if (progressPercentText != null) progressPercentText.text = "100%";
                    break;
                }

                yield return null;
            }

            // Hold 100% briefly
            if (progressBarFill != null) progressBarFill.fillAmount = 1f;
            if (progressPercentText != null) progressPercentText.text = "100%";
            yield return new WaitForSeconds(0.2f);

            // Fade out
            if (canvasGroup != null && fadeOutDuration > 0f)
            {
                float fadeTimer = 0f;
                while (fadeTimer < fadeOutDuration)
                {
                    fadeTimer += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, fadeTimer / fadeOutDuration);
                    yield return null;
                }
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            isLoadingComplete = true;

            // Activate game scene
            if (asyncOp != null)
            {
                asyncOp.allowSceneActivation = true;
            }
            else
            {
                SceneManager.LoadScene(targetSceneName);
            }

            Debug.Log($"[LoadingScreen] Transition to '{targetSceneName}' complete!");
        }

        public void SetTipsVisible(bool visible)
        {
            showTips = visible;
            if (tipText != null)
            {
                tipText.gameObject.SetActive(visible);
            }
        }
    }
}
