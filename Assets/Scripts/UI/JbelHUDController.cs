using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JbelIghoud.Survival;

namespace JbelIghoud.UI
{
    /// <summary>
    /// Master HUD Controller for the authentic Jbel Ighoud visual interface.
    /// Manages Survivor Profile, Dynamic Quest Banner, Top Resource Bar (Stone, Coin, Hunger),
    /// Pause state, and Bottom-Right Action Buttons (COLLECT, BAG, MAP).
    /// </summary>
    public class JbelHUDController : MonoBehaviour
    {
        private static JbelHUDController _instance;
        public static JbelHUDController Instance => _instance;

        [Header("--- Top-Left: Survivor & Quest ---")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private JbelPauseMenuController pauseMenuController;
        [SerializeField] private TextMeshProUGUI survivorNameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private Image xpBarFill;
        [SerializeField] private Image avatarImage;
        [SerializeField] private RectTransform questBanner;
        [SerializeField] private TextMeshProUGUI questText;

        [Header("--- Top-Right: Resource Bar ---")]
        [SerializeField] private Image stoneCountImage;
        [SerializeField] private Image goldCountImage;
        [SerializeField] private Image foodPercentImage;
        [SerializeField] private TextMeshProUGUI stoneCountText;
        [SerializeField] private TextMeshProUGUI goldCountText;
        [SerializeField] private TextMeshProUGUI foodPercentText;
        [SerializeField] private Button plusButton;

        [Header("--- Bottom-Right: Action Buttons ---")]
        [SerializeField] private Button collectButton;
        [SerializeField] private Button bagButton;
        [SerializeField] private Button mapButton;
        [SerializeField] private CanvasGroup collectCanvasGroup;

        [Header("--- Bottom-Left: Joystick ---")]
        [SerializeField] private MobileJoystick mobileJoystick;

        [Header("--- Configurable Defaults ---")]
        [SerializeField] private string defaultSurvivorName = "SURVIVOR NAME";
        [SerializeField] private string defaultLevelName = "NIVEAU 1";
        [Range(0f, 1f)]
        [SerializeField] private float defaultXPProgress = 0.72f;
        [SerializeField] private string defaultQuestText = "FIND 100 PIECES OF GOLD";
        [SerializeField] private int goldCount = 245;
        [SerializeField] private int fallbackStoneCount = 34;
        [SerializeField] private int fallbackFoodPercent = 88;

        private PlayerSurvivalManager survivalManager;
        private InventoryManager inventoryManager;
        private TopDownPlayerController playerController;
        private bool isPaused = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            survivalManager = PlayerSurvivalManager.Instance;
            if (survivalManager == null) survivalManager = FindAnyObjectByType<PlayerSurvivalManager>();

            inventoryManager = InventoryManager.Instance;
            if (inventoryManager == null) inventoryManager = FindAnyObjectByType<InventoryManager>();

            playerController = FindAnyObjectByType<TopDownPlayerController>();

            // Setup listeners
            var allChildButtons = GetComponentsInChildren<Button>(true);
            foreach (var b in allChildButtons)
            {
                if (b.name.Contains("Pause", System.StringComparison.OrdinalIgnoreCase))
                {
                    b.onClick.RemoveListener(OnPauseClicked);
                    b.onClick.AddListener(OnPauseClicked);
                }
            }
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(OnPauseClicked);
                pauseButton.onClick.AddListener(OnPauseClicked);
            }
            if (collectButton != null) collectButton.onClick.AddListener(OnCollectClicked);
            if (bagButton != null) bagButton.onClick.AddListener(OnBagClicked);
            if (mapButton != null) mapButton.onClick.AddListener(OnMapClicked);
            if (plusButton != null) plusButton.onClick.AddListener(OnPlusClicked);

            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryChanged += UpdateInventoryDisplay;
            }

            InitializeDefaults();
            UpdateAllDisplays();
        }

        private void OnDestroy()
        {
            if (inventoryManager != null)
            {
                inventoryManager.OnInventoryChanged -= UpdateInventoryDisplay;
            }
        }

        private void InitializeDefaults()
        {
            if (survivorNameText != null) survivorNameText.text = defaultSurvivorName;
            if (levelText != null) levelText.text = defaultLevelName;
            if (xpBarFill != null) xpBarFill.fillAmount = defaultXPProgress;
            if (questText != null) questText.text = defaultQuestText;
            if (goldCountText != null) goldCountText.text = goldCount.ToString();
        }

        private void Update()
        {
            UpdateSurvivalDisplay();
            UpdateCollectButtonState();
        }

        private void UpdateSurvivalDisplay()
        {
            // Sync Hunger to Food percentage
            if (survivalManager != null && survivalManager.Hunger != null)
            {
                int hungerPct = Mathf.RoundToInt(survivalManager.Hunger.Normalized * 100f);
                if (hungerPct != fallbackFoodPercent)
                {
                    if (foodPercentImage != null) foodPercentImage.gameObject.SetActive(false);
                    if (foodPercentText != null)
                    {
                        foodPercentText.gameObject.SetActive(true);
                        foodPercentText.text = $"{hungerPct}%";
                    }
                }
            }
        }

        private void UpdateInventoryDisplay()
        {
            if (inventoryManager != null)
            {
                int count = inventoryManager.ItemCount;
                if (count > 0 && count != fallbackStoneCount)
                {
                    if (stoneCountImage != null) stoneCountImage.gameObject.SetActive(false);
                    if (stoneCountText != null)
                    {
                        stoneCountText.gameObject.SetActive(true);
                        stoneCountText.text = count.ToString();
                    }
                }
            }
        }

        private void UpdateCollectButtonState()
        {
            if (collectButton == null) return;

            // Highlight / pulse collect button when interactable is near
            GameObject pickBtnObj = GameObject.Find("PickButton");
            bool canCollect = pickBtnObj != null && pickBtnObj.activeInHierarchy;

            if (collectCanvasGroup != null)
            {
                collectCanvasGroup.alpha = canCollect ? 1.0f : 0.75f;
            }
        }

        public void UpdateAllDisplays()
        {
            UpdateSurvivalDisplay();
            UpdateInventoryDisplay();
        }

        public void SetSurvivorName(string name)
        {
            defaultSurvivorName = name;
            if (survivorNameText != null) survivorNameText.text = name;
        }

        public void SetLevel(string level, float progress01)
        {
            defaultLevelName = level;
            defaultXPProgress = Mathf.Clamp01(progress01);
            if (levelText != null) levelText.text = level;
            if (xpBarFill != null) xpBarFill.fillAmount = defaultXPProgress;
        }

        public void SetQuest(string quest)
        {
            defaultQuestText = quest;
            if (questText != null) questText.text = quest;
        }

        public void SetGold(int amount)
        {
            goldCount = amount;
            if (goldCountText != null) goldCountText.text = amount.ToString();
        }

        public void SetStone(int amount)
        {
            fallbackStoneCount = amount;
            if (stoneCountText != null) stoneCountText.text = amount.ToString();
        }

        private void OnCollectClicked()
        {
            // Forward click to existing PickButton if available
            GameObject pickBtnObj = GameObject.Find("PickButton");
            if (pickBtnObj != null)
            {
                Button btn = pickBtnObj.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.Invoke();
                    return;
                }
            }

            Debug.Log("[JbelHUD] Collect action triggered.");
        }

        private void OnBagClicked()
        {
            // Open Inventory Satchel
            InventoryUI invUI = InventoryUI.Instance;
            if (invUI != null)
            {
                invUI.ToggleInventory();
                return;
            }

            GameObject hudBtn = GameObject.Find("InventoryHUDButton");
            if (hudBtn != null)
            {
                Button btn = hudBtn.GetComponent<Button>();
                if (btn != null)
                {
                    btn.onClick.Invoke();
                }
            }
        }

        private void OnMapClicked()
        {
            Debug.Log("[JbelHUD] Map button clicked.");
        }

        private void OnPlusClicked()
        {
            Debug.Log("[JbelHUD] Plus button clicked (Crafting / Resource Menu).");
            OnBagClicked();
        }

        public void OnPauseClicked()
        {
            var pause = pauseMenuController != null ? pauseMenuController : JbelPauseMenuController.Instance;
            if (pause == null)
            {
                pause = Object.FindAnyObjectByType<JbelPauseMenuController>(FindObjectsInactive.Include);
                if (pause != null) pauseMenuController = pause;
            }

            if (pause != null)
            {
                if (pause.IsPaused)
                {
                    pause.ResumeGame();
                }
                else
                {
                    pause.PauseGame();
                }
            }
            else
            {
                isPaused = !isPaused;
                Time.timeScale = isPaused ? 0f : 1f;
                Debug.Log($"[JbelHUD] Game Paused: {isPaused}");
            }
        }
    }
}
