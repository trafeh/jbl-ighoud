using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JbelIghoud.Survival
{
    /// <summary>
    /// High-definition, crisp Survival HUD view powered by TextMeshPro SDF typography
    /// and dedicated sprite assets (Pixel Hearts, Bubble, Meat, Zzz).
    /// Built in the style of PlayerHealthBar (2D Health & Damage System) with beveled frames,
    /// smooth lerped fill transitions, warning pulses, and atmospheric overlays.
    /// </summary>
    public class SurvivalHUDView : MonoBehaviour
    {
        private static SurvivalHUDView _instance;
        public static SurvivalHUDView Instance => _instance;

        [Header("--- Survival Manager Reference ---")]
        [SerializeField] private PlayerSurvivalManager survivalManager;

        [Header("--- Main Widget Hierarchy ---")]
        [SerializeField] private RectTransform widgetContainer;
        [SerializeField] private CanvasGroup widgetCanvasGroup;

        [Header("--- Stat Bar UI: Health ---")]
        [SerializeField] private Image healthFillImage;
        [SerializeField] private TextMeshProUGUI healthValueText;
        [SerializeField] private Image healthIconImage;

        [Header("--- Stat Bar UI: Respiration (Breath) ---")]
        [SerializeField] private RectTransform respirationRow;
        [SerializeField] private Image respirationFillImage;
        [SerializeField] private TextMeshProUGUI respirationValueText;
        [SerializeField] private Image respirationIconImage;

        [Header("--- Stat Bar UI: Hunger ---")]
        [SerializeField] private Image hungerFillImage;
        [SerializeField] private TextMeshProUGUI hungerValueText;
        [SerializeField] private Image hungerIconImage;

        [Header("--- Stat Bar UI: Sleep ---")]
        [SerializeField] private Image sleepFillImage;
        [SerializeField] private TextMeshProUGUI sleepValueText;
        [SerializeField] private Image sleepIconImage;

        [Header("--- Action Buttons ---")]
        [SerializeField] private Button diveButton;
        [SerializeField] private TextMeshProUGUI diveButtonText;
        [SerializeField] private Button eatButton;
        [SerializeField] private Button restButton;

        [Header("--- Animation & Responsiveness ---")]
        [Tooltip("How quickly the UI stat bars smooth-slide to match current stat values (higher = faster / snappier, lower = smoother).")]
        [Range(1f, 40f)]
        [SerializeField] private float barAnimationSpeed = 10f;

        [Header("--- Screen Atmosphere Overlays ---")]
        [SerializeField] private Image damageFlashOverlay;
        [SerializeField] private Image underwaterOverlay;
        [SerializeField] private Image drowsyOverlay;
        [SerializeField] private CanvasGroup sleepBlackoutCanvasGroup;
        [SerializeField] private TextMeshProUGUI sleepBannerText;

        // Smooth fill interpolation
        private float displayedHealth = 1f;
        private float displayedRespiration = 1f;
        private float displayedHunger = 1f;
        private float displayedSleep = 1f;

        // Damage flash state
        private float damageFlashAlpha = 0f;

        // Style Colors matching PlayerHealthBar & Jbel Ighoud palette
        private readonly Color healthColor = new Color(0.85f, 0.12f, 0.15f, 1f);     // Vibrant Ruby Red
        private readonly Color respirationColor = new Color(0.00f, 0.85f, 1.00f, 1f); // Ocean Cyan
        private readonly Color hungerColor = new Color(1.00f, 0.58f, 0.08f, 1f);      // Warm Amber
        private readonly Color sleepColor = new Color(0.55f, 0.35f, 1.00f, 1f);       // Deep Indigo

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }
            _instance = this;

            if (survivalManager == null)
            {
                survivalManager = PlayerSurvivalManager.Instance;
                if (survivalManager == null)
                {
                    survivalManager = FindAnyObjectByType<PlayerSurvivalManager>();
                }
            }

            EnsureHUDStructure();
        }

        private void Start()
        {
            if (survivalManager == null)
            {
                survivalManager = PlayerSurvivalManager.Instance;
                if (survivalManager == null)
                {
                    survivalManager = FindAnyObjectByType<PlayerSurvivalManager>();
                }
            }

            if (survivalManager != null)
            {
                survivalManager.OnDamageTaken += HandleDamageTaken;
                survivalManager.OnSleepStateChanged += HandleSleepStateChanged;
            }

            // Hook up mobile action buttons (DiveButton handled by TopDownPlayerController exclusively)
            if (eatButton != null)
            {
                eatButton.onClick.RemoveListener(OnEatButtonClicked);
                eatButton.onClick.AddListener(OnEatButtonClicked);
            }
            if (restButton != null)
            {
                restButton.onClick.RemoveListener(OnRestButtonClicked);
                restButton.onClick.AddListener(OnRestButtonClicked);
            }

            // Register dive button with player controller
            var pc = TopDownPlayerController.Instance;
            if (pc != null && diveButton != null)
            {
                pc.SetDiveButton(diveButton);
            }
        }

        private void OnDestroy()
        {
            if (survivalManager != null)
            {
                survivalManager.OnDamageTaken -= HandleDamageTaken;
                survivalManager.OnSleepStateChanged -= HandleSleepStateChanged;
            }
        }

        private void Update()
        {
            if (survivalManager == null)
            {
                survivalManager = PlayerSurvivalManager.Instance;
                if (survivalManager == null) return;
            }

            UpdateSmoothFillValues();
            UpdateVisualTexts();
            UpdateWarningPulses();
            UpdateOverlays();
            UpdateActionButtons();
        }

        private void UpdateSmoothFillValues()
        {
            float lerpSpeed = barAnimationSpeed * Time.deltaTime;

            displayedHealth = Mathf.Lerp(displayedHealth, survivalManager.Health.Normalized, lerpSpeed);
            displayedRespiration = Mathf.Lerp(displayedRespiration, survivalManager.Respiration.Normalized, lerpSpeed);
            displayedHunger = Mathf.Lerp(displayedHunger, survivalManager.Hunger.Normalized, lerpSpeed);
            displayedSleep = Mathf.Lerp(displayedSleep, survivalManager.Sleep.Normalized, lerpSpeed);

            if (healthFillImage != null) healthFillImage.fillAmount = displayedHealth;
            if (respirationFillImage != null) respirationFillImage.fillAmount = displayedRespiration;
            if (hungerFillImage != null) hungerFillImage.fillAmount = displayedHunger;
            if (sleepFillImage != null) sleepFillImage.fillAmount = displayedSleep;
        }

        private void UpdateVisualTexts()
        {
            if (healthValueText != null)
            {
                healthValueText.text = $"{Mathf.CeilToInt(survivalManager.Health.CurrentValue)} / {Mathf.CeilToInt(survivalManager.Health.MaxValue)}";
            }
            if (respirationValueText != null)
            {
                respirationValueText.text = $"{Mathf.CeilToInt(survivalManager.Respiration.CurrentValue)}%";
            }
            if (hungerValueText != null)
            {
                hungerValueText.text = $"{Mathf.CeilToInt(survivalManager.Hunger.CurrentValue)}%";
            }
            if (sleepValueText != null)
            {
                sleepValueText.text = $"{Mathf.CeilToInt(survivalManager.Sleep.CurrentValue)}%";
            }
        }

        private void UpdateWarningPulses()
        {
            // Health critical pulse
            if (healthFillImage != null)
            {
                if (survivalManager.Health.IsCritical)
                {
                    healthFillImage.color = Color.Lerp(healthColor, Color.white, Mathf.Sin(Time.time * 10f) * 0.5f + 0.5f);
                }
                else
                {
                    healthFillImage.color = healthColor;
                }
            }

            // Respiration pulse while underwater
            var pc = TopDownPlayerController.Instance;
            bool isUnderwater = pc != null && pc.IsUnderwater;

            if (respirationFillImage != null)
            {
                if (survivalManager.Respiration.IsCritical)
                {
                    respirationFillImage.color = Color.Lerp(respirationColor, Color.red, Mathf.Sin(Time.time * 12f) * 0.5f + 0.5f);
                }
                else if (isUnderwater)
                {
                    respirationFillImage.color = Color.Lerp(respirationColor, Color.white, Mathf.Sin(Time.time * 4f) * 0.25f + 0.25f);
                }
                else
                {
                    respirationFillImage.color = respirationColor;
                }
            }

            // Low Hunger warning
            if (hungerFillImage != null)
            {
                hungerFillImage.color = survivalManager.Hunger.IsCritical
                    ? Color.Lerp(hungerColor, Color.red, Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f)
                    : hungerColor;
            }

            // Low Sleep warning
            if (sleepFillImage != null)
            {
                sleepFillImage.color = survivalManager.Sleep.IsCritical
                    ? Color.Lerp(sleepColor, Color.red, Mathf.Sin(Time.time * 8f) * 0.5f + 0.5f)
                    : sleepColor;
            }
        }

        private void UpdateOverlays()
        {
            // Damage red flash fade
            if (damageFlashOverlay != null)
            {
                if (damageFlashAlpha > 0f)
                {
                    damageFlashAlpha = Mathf.MoveTowards(damageFlashAlpha, 0f, 2.0f * Time.deltaTime);
                    Color c = damageFlashOverlay.color;
                    c.a = damageFlashAlpha;
                    damageFlashOverlay.color = c;
                }
            }

            // Underwater atmospheric overlay when diving
            var pc = TopDownPlayerController.Instance;
            if (underwaterOverlay != null && pc != null)
            {
                float targetAquaAlpha = pc.IsUnderwater ? (pc.IsDiving ? 0.38f : 0.22f) : 0f;
                Color uc = underwaterOverlay.color;
                uc.a = Mathf.MoveTowards(uc.a, targetAquaAlpha, 3f * Time.deltaTime);
                underwaterOverlay.color = uc;
            }

            // Drowsy fatigue overlay
            if (drowsyOverlay != null && survivalManager != null)
            {
                float targetDrowsyAlpha = survivalManager.Sleep.IsLow ? (survivalManager.Sleep.IsCritical ? 0.45f : 0.25f) : 0f;
                Color dc = drowsyOverlay.color;
                dc.a = Mathf.MoveTowards(dc.a, targetDrowsyAlpha, 1.5f * Time.deltaTime);
                drowsyOverlay.color = dc;
            }
        }

        private void UpdateActionButtons()
        {
            var pc = TopDownPlayerController.Instance;
            if (pc == null) return;

            bool inWater = pc.IsInWater || pc.IsSwimming;
            if (diveButton != null)
            {
                bool shouldShowDive = inWater && !survivalManager.IsDead && !survivalManager.IsSleeping;
                if (diveButton.gameObject.activeSelf != shouldShowDive)
                {
                    diveButton.gameObject.SetActive(shouldShowDive);
                }

                if (diveButtonText != null)
                {
                    diveButtonText.text = pc.IsDiving ? "SURFACE" : "DIVE";
                }
            }
        }

        private void HandleDamageTaken(float amount, DamageCause cause)
        {
            damageFlashAlpha = Mathf.Clamp(damageFlashAlpha + 0.45f, 0f, 0.75f);
        }

        private void HandleSleepStateChanged(bool sleeping)
        {
            StartCoroutine(AnimateSleepBlackout(sleeping));
        }

        private IEnumerator AnimateSleepBlackout(bool sleeping)
        {
            if (sleepBlackoutCanvasGroup == null) yield break;

            float targetAlpha = sleeping ? 1f : 0f;
            float duration = 1.0f;
            float startAlpha = sleepBlackoutCanvasGroup.alpha;
            float elapsed = 0f;

            if (sleeping && sleepBannerText != null)
            {
                var dnm = JbelIghoud.DayNight.DayNightCycleManager.Instance;
                int day = dnm != null ? dnm.currentDay : 1;
                sleepBannerText.text = $"<b>Resting through the night...</b>\n<size=18><color=#F6C878>☀️ Waking up to Day {day + 1}</color></size>";
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                sleepBlackoutCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                yield return null;
            }
            sleepBlackoutCanvasGroup.alpha = targetAlpha;
        }

        private void OnEatButtonClicked()
        {
            if (survivalManager != null)
            {
                survivalManager.Eat(25f);
            }
        }

        private void OnRestButtonClicked()
        {
            if (survivalManager != null)
            {
                survivalManager.SleepUntilMorning(7.0f);
            }
        }

        /// <summary>
        /// Automatically constructs the full survival UI hierarchy with crisp SDF typography,
        /// high-res sprites, and PlayerHealthBar styled beveled frames.
        /// </summary>
        public void EnsureHUDStructure()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindAnyObjectByType<Canvas>();
            }
            if (canvas == null) return;

            // If authentic Jbel HUD exists, skip legacy survival widget panel creation
            if (canvas.transform.Find("JbelHUD_Panel") != null)
            {
                return;
            }

            // Load Sprites and TMP Font Asset
            Sprite heartSp = Resources.Load<Sprite>("heart pixel art 64x64");
#if UNITY_EDITOR
            if (heartSp == null) heartSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Hearts/heart pixel art/heart pixel art 64x64.png");
            if (heartSp == null) heartSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Hearts/heart pixel art/heart pixel art 254x254.png");
            Sprite bubbleSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/breath bubble.png");
            Sprite meatSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/meat.jpg");
            Sprite zzzSp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/zzz_594550.png");
            TMP_FontAsset fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
#else
            Sprite bubbleSp = null;
            Sprite meatSp = null;
            Sprite zzzSp = null;
            TMP_FontAsset fontAsset = null;
#endif

            // Clean up old panel if rebuilding
            Transform oldPanel = canvas.transform.Find("SurvivalHUD_Panel");
            if (oldPanel != null)
            {
                DestroyImmediate(oldPanel.gameObject);
            }

            // 1. Root Container (Top-Left of Screen)
            GameObject rootGo = new GameObject("SurvivalHUD_Panel", typeof(RectTransform), typeof(CanvasGroup));
            rootGo.transform.SetParent(canvas.transform, false);
            widgetContainer = rootGo.GetComponent<RectTransform>();
            widgetCanvasGroup = rootGo.GetComponent<CanvasGroup>();

            widgetContainer.anchorMin = new Vector2(0f, 1f);
            widgetContainer.anchorMax = new Vector2(0f, 1f);
            widgetContainer.pivot = new Vector2(0f, 1f);
            widgetContainer.anchoredPosition = new Vector2(28f, -28f);
            widgetContainer.sizeDelta = new Vector2(300f, 215f);

            // Sleek obsidian card background
            Image bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.88f);
            bg.raycastTarget = false;

            // Golden amber outline matching Jbel Ighoud UI theme
            Outline outline = rootGo.AddComponent<Outline>();
            outline.effectColor = new Color(0.85f, 0.65f, 0.25f, 0.55f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Header Title with crisp SDF Font
            CreateTMPTextObject(rootGo.transform, "Title", "<color=#F6C878><b>EXPEDITION VITALS</b></color>", 13, new Vector2(14f, -10f), new Vector2(240f, 20f), TextAlignmentOptions.Left, fontAsset);

            // 2. Build 4 Stat Bars in the exact style of PlayerHealthBar.prefab
            float startY = -36f;
            float rowSpacing = 34f;

            healthFillImage = CreatePlayerHealthBarStyleRow(rootGo.transform, "HealthBar", heartSp, healthColor, new Vector2(12f, startY), fontAsset, out healthValueText, out healthIconImage);
            respirationFillImage = CreatePlayerHealthBarStyleRow(rootGo.transform, "RespirationBar", bubbleSp, respirationColor, new Vector2(12f, startY - rowSpacing), fontAsset, out respirationValueText, out respirationIconImage);
            hungerFillImage = CreatePlayerHealthBarStyleRow(rootGo.transform, "HungerBar", meatSp, hungerColor, new Vector2(12f, startY - rowSpacing * 2f), fontAsset, out hungerValueText, out hungerIconImage);
            sleepFillImage = CreatePlayerHealthBarStyleRow(rootGo.transform, "SleepBar", zzzSp, sleepColor, new Vector2(12f, startY - rowSpacing * 3f), fontAsset, out sleepValueText, out sleepIconImage);

            respirationRow = rootGo.transform.Find("RespirationBar") as RectTransform;

            // Mini Action Row for Eating & Sleeping
            CreateQuickActionsRow(rootGo.transform, new Vector2(12f, startY - rowSpacing * 4f - 2f), fontAsset);

            // 3. Create Dive Button near Jump Button if missing
            Transform oldDive = canvas.transform.Find("DiveButton");
            if (oldDive != null)
            {
                DestroyImmediate(oldDive.gameObject);
            }
            CreateDiveButton(canvas.transform, fontAsset);

            // 4. Create Atmospheric Full-Screen Overlays
            CreateAtmosphericOverlays(canvas.transform, fontAsset);
        }

        private Image CreatePlayerHealthBarStyleRow(Transform parent, string name, Sprite iconSprite, Color fillCol, Vector2 pos, TMP_FontAsset fontAsset, out TextMeshProUGUI valText, out Image iconImg)
        {
            GameObject rowGo = new GameObject(name, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRT = rowGo.GetComponent<RectTransform>();
            rowRT.anchorMin = new Vector2(0f, 1f);
            rowRT.anchorMax = new Vector2(0f, 1f);
            rowRT.pivot = new Vector2(0f, 1f);
            rowRT.anchoredPosition = pos;
            rowRT.sizeDelta = new Vector2(276f, 28f);

            // Icon Slot (Left-aligned, crisp aspect ratio)
            GameObject iconSlotGo = new GameObject("IconSlot", typeof(RectTransform), typeof(Image));
            iconSlotGo.transform.SetParent(rowGo.transform, false);
            RectTransform iconRT = iconSlotGo.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0f, 0.5f);
            iconRT.anchorMax = new Vector2(0f, 0.5f);
            iconRT.pivot = new Vector2(0f, 0.5f);
            iconRT.anchoredPosition = new Vector2(0f, 0f);
            iconRT.sizeDelta = new Vector2(28f, 28f);

            iconImg = iconSlotGo.GetComponent<Image>();
            iconImg.color = Color.white;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            if (iconSprite != null)
            {
                iconImg.sprite = iconSprite;
            }

            // Outer Black Beveled Frame (exactly like PlayerHealthBar.prefab Background)
            GameObject frameGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(rowGo.transform, false);
            RectTransform frameRT = frameGo.GetComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0f, 0f);
            frameRT.anchorMax = new Vector2(1f, 1f);
            frameRT.offsetMin = new Vector2(34f, 1f);
            frameRT.offsetMax = new Vector2(0f, -1f);

            Image frameImg = frameGo.GetComponent<Image>();
            frameImg.color = new Color(0.0f, 0.0f, 0.0f, 0.95f); // Deep solid black bevel
            frameImg.raycastTarget = false;

            // Recessed FillArea Track (like PlayerHealthBar.prefab FillArea)
            GameObject trackGo = new GameObject("FillArea", typeof(RectTransform), typeof(Image));
            trackGo.transform.SetParent(frameGo.transform, false);
            RectTransform trackRT = trackGo.GetComponent<RectTransform>();
            trackRT.anchorMin = new Vector2(0f, 0f);
            trackRT.anchorMax = new Vector2(1f, 1f);
            trackRT.offsetMin = new Vector2(3f, 3f);
            trackRT.offsetMax = new Vector2(-3f, -3f);

            Image trackImg = trackGo.GetComponent<Image>();
            trackImg.color = new Color(0.18f, 0.18f, 0.20f, 1f); // Dark gray recessed trench
            trackImg.raycastTarget = false;

            // Fill Image (like PlayerHealthBar.prefab Fill)
            GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(trackGo.transform, false);
            RectTransform fillRT = fillGo.GetComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0f, 0f);
            fillRT.anchorMax = new Vector2(1f, 1f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            Image fillImg = fillGo.GetComponent<Image>();
            fillImg.color = fillCol;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1.0f;
            fillImg.raycastTarget = false;

            // Crisp TextMeshPro Value Overlay
            valText = CreateTMPTextObject(trackGo.transform, "Value", "100%", 13, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center, fontAsset);
            RectTransform valRT = valText.rectTransform;
            valRT.anchorMin = Vector2.zero;
            valRT.anchorMax = Vector2.one;
            valRT.offsetMin = Vector2.zero;
            valRT.offsetMax = Vector2.zero;
            valText.fontStyle = FontStyles.Bold;
            valText.color = Color.white;

            return fillImg;
        }

        private void CreateQuickActionsRow(Transform parent, Vector2 pos, TMP_FontAsset fontAsset)
        {
            GameObject actionsRow = new GameObject("ActionsRow", typeof(RectTransform));
            actionsRow.transform.SetParent(parent, false);
            RectTransform rt = actionsRow.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(276f, 26f);

            // Eat Button
            eatButton = CreateTMPButton(actionsRow.transform, "EatButton", "<b>EAT 🍖</b>", new Vector2(34f, 0f), new Vector2(116f, 26f), new Color(0.85f, 0.45f, 0.1f, 0.95f), fontAsset);

            // Sleep / Rest Button
            restButton = CreateTMPButton(actionsRow.transform, "RestButton", "<b>REST 💤</b>", new Vector2(156f, 0f), new Vector2(120f, 26f), new Color(0.35f, 0.25f, 0.75f, 0.95f), fontAsset);
        }

        private Button CreateTMPButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color btnColor, TMP_FontAsset fontAsset)
        {
            GameObject btnGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = btnGo.GetComponent<Image>();
            img.color = btnColor;

            Outline outline = btnGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.0f, 0.0f, 0.0f, 0.75f);
            outline.effectDistance = new Vector2(1f, -1f);

            Button btn = btnGo.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.highlightedColor = btnColor * 1.2f;
            cb.pressedColor = btnColor * 0.8f;
            btn.colors = cb;

            TextMeshProUGUI txt = CreateTMPTextObject(btnGo.transform, "Text", label, 12, Vector2.zero, size, TextAlignmentOptions.Center, fontAsset);
            RectTransform txtRT = txt.rectTransform;
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            txt.color = Color.white;

            return btn;
        }

        private void CreateDiveButton(Transform canvasTransform, TMP_FontAsset fontAsset)
        {
            GameObject diveGo = new GameObject("DiveButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            diveGo.transform.SetParent(canvasTransform, false);
            RectTransform rt = diveGo.GetComponent<RectTransform>();

            // Placed near bottom-right Jump Button (1920x1080 canvas coordinates)
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-220f, 90f);
            rt.sizeDelta = new Vector2(100f, 48f);

            Image img = diveGo.GetComponent<Image>();
            img.color = new Color(0.0f, 0.65f, 0.88f, 0.95f);

            Outline outline = diveGo.GetComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.95f, 1.0f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            diveButton = diveGo.GetComponent<Button>();
            diveButtonText = CreateTMPTextObject(diveGo.transform, "Label", "<b>DIVE</b>", 16, Vector2.zero, rt.sizeDelta, TextAlignmentOptions.Center, fontAsset);
            RectTransform txtRT = diveButtonText.rectTransform;
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = Vector2.zero;
            txtRT.offsetMax = Vector2.zero;
            diveButtonText.color = Color.white;

            diveGo.SetActive(false); // Only active while swimming or in water
        }

        private void CreateAtmosphericOverlays(Transform canvasTransform, TMP_FontAsset fontAsset)
        {
            // 1. Damage Flash Overlay (Red Screen Border Flash)
            Transform oldDmg = canvasTransform.Find("Overlay_DamageFlash");
            if (oldDmg != null) DestroyImmediate(oldDmg.gameObject);

            GameObject flashGo = new GameObject("Overlay_DamageFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(canvasTransform, false);
            flashGo.transform.SetAsFirstSibling();
            RectTransform rt = flashGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            damageFlashOverlay = flashGo.GetComponent<Image>();
            damageFlashOverlay.color = new Color(0.85f, 0.05f, 0.05f, 0.0f);
            damageFlashOverlay.raycastTarget = false;

            // 2. Underwater Diving Atmosphere Overlay
            Transform oldUnder = canvasTransform.Find("Overlay_Underwater");
            if (oldUnder != null) DestroyImmediate(oldUnder.gameObject);

            GameObject aquaGo = new GameObject("Overlay_Underwater", typeof(RectTransform), typeof(Image));
            aquaGo.transform.SetParent(canvasTransform, false);
            aquaGo.transform.SetSiblingIndex(1);
            RectTransform art = aquaGo.GetComponent<RectTransform>();
            art.anchorMin = Vector2.zero;
            art.anchorMax = Vector2.one;
            art.offsetMin = Vector2.zero;
            art.offsetMax = Vector2.zero;

            underwaterOverlay = aquaGo.GetComponent<Image>();
            underwaterOverlay.color = new Color(0.0f, 0.40f, 0.65f, 0.0f);
            underwaterOverlay.raycastTarget = false;

            // 3. Drowsy Vignette Overlay
            Transform oldDrw = canvasTransform.Find("Overlay_Drowsy");
            if (oldDrw != null) DestroyImmediate(oldDrw.gameObject);

            GameObject drowsyGo = new GameObject("Overlay_Drowsy", typeof(RectTransform), typeof(Image));
            drowsyGo.transform.SetParent(canvasTransform, false);
            drowsyGo.transform.SetSiblingIndex(2);
            RectTransform drt = drowsyGo.GetComponent<RectTransform>();
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;

            drowsyOverlay = drowsyGo.GetComponent<Image>();
            drowsyOverlay.color = new Color(0.03f, 0.03f, 0.06f, 0.0f);
            drowsyOverlay.raycastTarget = false;

            // 4. Sleep Blackout Curtain
            Transform oldSlp = canvasTransform.Find("Overlay_SleepBlackout");
            if (oldSlp != null) DestroyImmediate(oldSlp.gameObject);

            GameObject sleepGo = new GameObject("Overlay_SleepBlackout", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            sleepGo.transform.SetParent(canvasTransform, false);
            RectTransform srt = sleepGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = Vector2.zero;
            srt.offsetMax = Vector2.zero;

            Image sbg = sleepGo.GetComponent<Image>();
            sbg.color = new Color(0.02f, 0.02f, 0.03f, 1.0f);
            sbg.raycastTarget = false;

            sleepBlackoutCanvasGroup = sleepGo.GetComponent<CanvasGroup>();
            sleepBlackoutCanvasGroup.alpha = 0f;
            sleepBlackoutCanvasGroup.blocksRaycasts = false;

            sleepBannerText = CreateTMPTextObject(sleepGo.transform, "SleepBanner", "<b>Resting through the night...</b>", 24, Vector2.zero, new Vector2(500f, 120f), TextAlignmentOptions.Center, fontAsset);
            RectTransform txtRT = sleepBannerText.rectTransform;
            txtRT.anchorMin = new Vector2(0.5f, 0.5f);
            txtRT.anchorMax = new Vector2(0.5f, 0.5f);
            txtRT.pivot = new Vector2(0.5f, 0.5f);
            txtRT.anchoredPosition = Vector2.zero;
        }

        private TextMeshProUGUI CreateTMPTextObject(Transform parent, string name, string content, float fontSize, Vector2 pos, Vector2 size, TextAlignmentOptions alignment, TMP_FontAsset fontAsset)
        {
            GameObject txtGo = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(parent, false);
            RectTransform rt = txtGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            TextMeshProUGUI tmp = txtGo.GetComponent<TextMeshProUGUI>();
            if (fontAsset != null) tmp.font = fontAsset;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;

            return tmp;
        }
    }
}
