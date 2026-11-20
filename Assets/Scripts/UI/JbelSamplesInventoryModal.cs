using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JbelIghoud.Audio;
using JbelIghoud.Lithic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace JbelIghoud.UI
{
    /// <summary>
    /// Master Controller for the authentic Jbel Ighoud Samples Inventory Modal:
    /// - Matches reference art design with dual-panel layout (Left: Samples Grid, Right: Info Panel).
    /// - Top-left badge tab "SAMPLES" (Assets/UI/inv/sample.png).
    /// - 4x3 grid of 12 sample slots with dotted rounded frames (Assets/UI/inv/window object.png).
    /// - Items appear sequentially one by one as they are collected in the world.
    /// - Right info panel displays thumbnail on wavy tan card (Assets/UI/inv/chosed object's bg.png),
    ///   title, category subtitle, HARDNESS progress bar, RARITY progress bar, description, and INSPECT button.
    /// - INSPECT button opens the 3D rock turntable inspection studio.
    /// - Top-right circular close button with 'X'.
    /// - Full persistence & real-time sync with InventoryManager.
    /// </summary>
    public class JbelSamplesInventoryModal : MonoBehaviour
    {
        private static JbelSamplesInventoryModal _instance;
        public static JbelSamplesInventoryModal Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = UnityEngine.Object.FindAnyObjectByType<JbelSamplesInventoryModal>(FindObjectsInactive.Include);
                }
                return _instance;
            }
        }

        [System.Serializable]
        public class SampleSlotUI
        {
            public GameObject rootObj;
            public Button button;
            public Image frameImg;
            public Image iconImg;
            public GameObject selectedHighlight;
        }

        [Header("========================================")]
        [Header("--- MODAL CONTAINERS ---")]
        [Header("========================================")]
        [SerializeField] private CanvasGroup modalCG;
        [SerializeField] private RectTransform containerRT;
        [SerializeField] private Image dimBackdrop;
        [SerializeField] private Button closeButton;

        [Header("========================================")]
        [Header("--- 12 SAMPLE SLOTS ---")]
        [Header("========================================")]
        [SerializeField] private SampleSlotUI[] slots = new SampleSlotUI[12];

        [Header("========================================")]
        [Header("--- RIGHT INFO PANEL ELEMENTS ---")]
        [Header("========================================")]
        [SerializeField] private GameObject infoContentGroup;
        [SerializeField] private GameObject infoEmptyGroup;
        [SerializeField] private Image thumbnailIconImg;
        [SerializeField] private Image thumbnailCardBgImg;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI subtitleText;
        [SerializeField] private Image hardnessFillImg;
        [SerializeField] private Image rarityFillImg;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button inspectButton;

        [Header("========================================")]
        [Header("--- ROCK PREVIEW SPRITES ---")]
        [Header("========================================")]
        [SerializeField] private Sprite silexSprite;
        [SerializeField] private Sprite gresSprite;
        [SerializeField] private Sprite calcaireSprite;
        [SerializeField] private Sprite basaltSprite;
        [SerializeField] private Sprite fallbackRockSprite;

        [Header("========================================")]
        [Header("--- ANIMATION ---")]
        [Header("========================================")]
        [SerializeField] private float animDuration = 0.22f;

        private Coroutine animRoutine;
        private bool isOpen = false;
        public bool IsOpen => isOpen;

        private int selectedSlotIndex = -1;
        private GameObject rightPanelGO;
        private RockItem currentSelectedRock = null;
        private Action onCloseCallback;

        // ------------------------------------------------ TAKE button + tutorial highlight
        private Button takeButton;
        private Button breakButton;
        /// <summary>Raised when BREAK starts striking a stone with the hammerstone.</summary>
        public event Action<RockItem> OnBreakStarted;
        /// <summary>Raised when the bag opens.</summary>
        public event Action OnOpened;
        /// <summary>Raised when a stone is moved from the bag into the player's hands.</summary>
        public event Action<RockItem> OnRockTaken;
        /// <summary>Tutorial: stone slots pulse (take me) while nothing is selected, the TAKE button pulses once one is.</summary>
        public bool TutorialPulse { get; set; }
        private float pulseT;

        private void Awake()
        {
            _instance = this;
            AutoWire();
            WireListeners();
            LoadSprites();
        }

        private void Start()
        {
            AutoWire();
            WireListeners();
            LoadSprites();

            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshInventoryUI;
                InventoryManager.Instance.OnInventoryChanged += RefreshInventoryUI;
            }

            if (!isOpen)
            {
                HideImmediate();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnInventoryChanged -= RefreshInventoryUI;
            }
        }

        private void Update()
        {
            // Keyboard shortcut to toggle / close
            bool toggleRequested = false;
            bool closeRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    toggleRequested = true;
                }
                if (Keyboard.current.escapeKey.wasPressedThisFrame && isOpen)
                {
                    closeRequested = true;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
            {
                toggleRequested = true;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && isOpen)
            {
                closeRequested = true;
            }
#endif

            if (closeRequested)
            {
                CloseInventory();
            }
            else if (toggleRequested)
            {
                ToggleInventory();
            }
        }

        public void AutoWire()
        {
            if (modalCG == null) modalCG = GetComponent<CanvasGroup>();
            if (dimBackdrop == null)
            {
                var d = transform.Find("DimBackdrop");
                if (d != null) dimBackdrop = d.GetComponent<Image>();
            }
            if (containerRT == null)
            {
                var c = transform.Find("Container");
                containerRT = c != null ? c.GetComponent<RectTransform>() : GetComponent<RectTransform>();
            }

            Transform leftPanel = containerRT != null ? containerRT.Find("LeftPanel_Samples") : null;
            Transform rightPanel = containerRT != null ? containerRT.Find("RightPanel_Infos") : null;
            if (rightPanel != null) rightPanelGO = rightPanel.gameObject;
            leftPanelRT = leftPanel as RectTransform; rightPanelRT = rightPanel as RectTransform;
            if (containerRT != null) { var cbT = containerRT.Find("CloseButton"); if (cbT != null) closeRT = cbT as RectTransform; }

            // Auto-wire 12 slots under LeftPanel_Samples/SlotsGrid
            if (leftPanel != null)
            {
                Transform grid = leftPanel.Find("SlotsGrid");
                if (grid != null)
                {
                    if (slots == null || slots.Length != 12) slots = new SampleSlotUI[12];
                    for (int i = 0; i < 12; i++)
                    {
                        Transform slotT = grid.Find("Slot_" + i);
                        if (slotT != null)
                        {
                            if (slots[i] == null) slots[i] = new SampleSlotUI();
                            slots[i].rootObj = slotT.gameObject;
                            slots[i].button = slotT.GetComponent<Button>();
                            slots[i].frameImg = slotT.GetComponent<Image>();

                            var icon = slotT.Find("ItemIcon");
                            if (icon != null) slots[i].iconImg = icon.GetComponent<Image>();

                            var hl = slotT.Find("SelectedHighlight");
                            if (hl != null) slots[i].selectedHighlight = hl.gameObject;
                        }
                    }
                }
            }

            // Auto-wire right panel elements
            if (rightPanel != null)
            {
                if (closeButton == null)
                {
                    var cb = containerRT.Find("CloseButton"); if (cb == null) cb = rightPanel.Find("CloseButton");
                    if (cb != null) closeButton = cb.GetComponent<Button>();
                }

                var cardBg = rightPanel.Find("ItemHeader/ThumbnailCard");
                if (cardBg != null)
                {
                    thumbnailCardBgImg = cardBg.GetComponent<Image>();
                    var icon = cardBg.Find("ThumbnailIcon");
                    if (icon != null) thumbnailIconImg = icon.GetComponent<Image>();
                }

                if (titleText == null)
                {
                    var t = rightPanel.Find("ItemHeader/TitleText");
                    if (t != null) titleText = t.GetComponent<TextMeshProUGUI>();
                }

                if (subtitleText == null)
                {
                    var s = rightPanel.Find("ItemHeader/SubtitleText");
                    if (s != null) subtitleText = s.GetComponent<TextMeshProUGUI>();
                }

                if (hardnessFillImg == null)
                {
                    var h = rightPanel.Find("Stats/HardnessBar_Groove/Fill");
                    if (h != null) hardnessFillImg = h.GetComponent<Image>();
                }

                if (rarityFillImg == null)
                {
                    var r = rightPanel.Find("Stats/RarityBar_Groove/Fill");
                    if (r != null) rarityFillImg = r.GetComponent<Image>();
                }

                if (descriptionText == null)
                {
                    var d = rightPanel.Find("DescriptionText");
                    if (d != null) descriptionText = d.GetComponent<TextMeshProUGUI>();
                }

                if (inspectButton == null)
                {
                    var b = rightPanel.Find("InspectButton");
                    if (b != null) inspectButton = b.GetComponent<Button>();
                }

                var cg = rightPanel.Find("ContentGroup");
                if (cg != null) infoContentGroup = cg.gameObject;

                var eg = rightPanel.Find("EmptyGroup");
                if (eg != null) infoEmptyGroup = eg.gameObject;
            }
        }

        private void WireListeners()
        {
            EnsureTakeButton();
            if (takeButton != null)
            {
                takeButton.onClick.RemoveListener(OnTakeButtonClicked);
                takeButton.onClick.AddListener(OnTakeButtonClicked);
            }
            if (breakButton != null)
            {
                breakButton.onClick.RemoveListener(OnBreakButtonClicked);
                breakButton.onClick.AddListener(OnBreakButtonClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseInventory);
                closeButton.onClick.AddListener(CloseInventory);
            }

            if (inspectButton != null)
            {
                inspectButton.onClick.RemoveListener(OnInspectButtonClicked);
                inspectButton.onClick.AddListener(OnInspectButtonClicked);
            }

            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    int slotIndex = i;
                    if (slots[i] != null && slots[i].button != null)
                    {
                        slots[i].button.onClick.RemoveAllListeners();
                        slots[i].button.onClick.AddListener(() => OnSlotClicked(slotIndex));
                    }
                }
            }
        }

        public void LoadSprites()
        {
            if (silexSprite == null) silexSprite = Resources.Load<Sprite>("Rocks/model_preview_silex");
            if (gresSprite == null) gresSprite = Resources.Load<Sprite>("Rocks/model_preview_gres");
            if (calcaireSprite == null) calcaireSprite = Resources.Load<Sprite>("Rocks/model_preview_calcaire");
            if (basaltSprite == null) basaltSprite = Resources.Load<Sprite>("Rocks/model_preview_basalt");

#if UNITY_EDITOR
            if (silexSprite == null) silexSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Textures/model_preview_silex.png");
            if (gresSprite == null) gresSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Textures/model_preview_gres.png");
            if (calcaireSprite == null) calcaireSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Textures/model_preview_calcaire.png");
            if (basaltSprite == null) basaltSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Textures/model_preview_basalt.png");
#endif
            if (fallbackRockSprite == null) fallbackRockSprite = basaltSprite != null ? basaltSprite : silexSprite;
        }

        // =========================================================
        // --- INVENTORY DATA & SLOTS SYNCHRONIZATION ---
        // =========================================================

        public void RefreshInventoryUI()
        {
            var inv = InventoryManager.Instance;
            var items = inv != null ? inv.Items : null;
            int count = items != null ? items.Count : 0;

            // Update all 12 slots
            for (int i = 0; i < 12; i++)
            {
                if (slots == null || i >= slots.Length || slots[i] == null) continue;

                var slot = slots[i];
                bool hasItem = (items != null && i < count && items[i] != null);
                if (slot.rootObj != null) slot.rootObj.SetActive(hasItem);

                if (slot.iconImg != null)
                {
                    if (hasItem)
                    {
                        slot.iconImg.sprite = GetSpriteForRock(items[i]);
slot.iconImg.gameObject.SetActive(true);
                        slot.iconImg.enabled = slot.iconImg.sprite != null;
                        slot.iconImg.color = Color.white;
                    }
                    else
                    {
                        slot.iconImg.sprite = null;
                        slot.iconImg.enabled = false;
                    }
                }

                UpdateCountBadge(slot, hasItem ? items[i] : null);

                // Highlight slot if selected
                if (slot.selectedHighlight != null)
                {
                    slot.selectedHighlight.SetActive(false);
                }
            }

            // Clamp selected slot
            if (selectedSlotIndex >= count)
            {
                selectedSlotIndex = -1;
            }

            // Update Right Info Panel
            if (items != null && count > 0 && selectedSlotIndex >= 0 && selectedSlotIndex < count && items[selectedSlotIndex] != null)
            {
                currentSelectedRock = items[selectedSlotIndex];
                DisplayRockInfo(currentSelectedRock);
            }
            else
            {
                currentSelectedRock = null;
                DisplayEmptyInfo();
            }
        }

        private void OnSlotClicked(int slotIndex)
        {
            selectedSlotIndex = slotIndex;

            // Update slot highlights
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].selectedHighlight != null)
                    {
                        slots[i].selectedHighlight.SetActive(false);
                    }
                }
            }

            var inv = InventoryManager.Instance;
            if (inv != null && slotIndex < inv.ItemCount && inv.Items[slotIndex] != null)
            {
                currentSelectedRock = inv.Items[slotIndex];
                DisplayRockInfo(currentSelectedRock);
            }
            else
            {
                currentSelectedRock = null;
                DisplayEmptyInfo();
            }
        }

        private void DisplayRockInfo(RockItem rock)
        {
            if (rock == null)
            {
                DisplayEmptyInfo();
                return;
            }

            SetRightPanelVisible(true);
            if (infoContentGroup != null) infoContentGroup.SetActive(true);
            if (infoEmptyGroup != null) infoEmptyGroup.SetActive(false);

            // Thumbnail
            if (thumbnailIconImg != null)
            {
                thumbnailIconImg.sprite = GetSpriteForRock(rock);
                thumbnailIconImg.rectTransform.localScale = Vector3.one * ((rock.IsFragment || rock.isHammerstone) ? 1.05f : CardIconScale);
                thumbnailIconImg.enabled = thumbnailIconImg.sprite != null;
                thumbnailIconImg.color = Color.white;
            }

            // Title
            string rockName = rock.rockData != null ? rock.rockData.rockName : rock.name;
            rockName = CleanRockName(rockName);
            if (titleText != null)
            {
                titleText.text = rock.isHammerstone ? "QUARTZITE HAMMERSTONE" : (rock.IsFragment ? rockName + " FRAGMENTS" : rockName);
            }

            // Subtitle
            string sub = rock.isHammerstone ? "your percussor  ·  never breaks" : (rock.IsFragment ? $"x{rock.fragmentCount} pieces  ·  crafting material" : GetRockSubtitle(rock));
            if (subtitleText != null)
            {
                subtitleText.text = sub.ToUpper();
            }

            // Hardness Bar (0 to 1, max 20)
            float hardnessVal = rock.rockData != null ? rock.rockData.hardness : rock.strength;
            if (hardnessVal < 1f) hardnessVal = 10f;
            float hardnessRatio = Mathf.Clamp01(hardnessVal / 20f);
            if (hardnessFillImg != null)
            {
                hardnessFillImg.fillAmount = Mathf.Max(0.12f, hardnessRatio);
            }

            // Rarity / Toughness Bar (0 to 1)
            float rarityVal = rock.rockData != null ? rock.rockData.toughness : 0.6f;
            float rarityRatio = Mathf.Clamp01(rarityVal);
            if (rarityFillImg != null)
            {
                rarityFillImg.fillAmount = Mathf.Max(0.12f, rarityRatio);
            }

            // Description Text
            string desc = rock.rockData != null && !string.IsNullOrEmpty(rock.rockData.description)
                ? rock.rockData.description
                : GetDefaultDescription(rockName);

            if (descriptionText != null)
            {
                descriptionText.text = desc;
            }

            if (inspectButton != null)
            {
                inspectButton.interactable = true;
            }
        }

        private RectTransform leftPanelRT, rightPanelRT, closeRT;
        private bool rightShown = true;
        private Coroutine panelAnim;
        private const float CompactShift = 0.16f;   // fraction of container width

        private void ApplyCompactLayout()
        {
            float w = containerRT != null ? containerRT.rect.width : 1920f;
            if (leftPanelRT != null) leftPanelRT.anchoredPosition = new Vector2(CompactShift * w, 0f);
            if (closeRT != null) closeRT.anchoredPosition = new Vector2(-0.157f * w, 0f);
        }

        private void SetRightPanelVisible(bool v)
        {
            if (rightPanelGO == null) return;
            if (!v)
            {
                if (panelAnim != null) { StopCoroutine(panelAnim); panelAnim = null; }
                rightShown = false;
                if (rightPanelGO.activeSelf) rightPanelGO.SetActive(false);
                ApplyCompactLayout();
                return;
            }
            if (rightShown && rightPanelGO.activeSelf) return;
            rightShown = true;
            rightPanelGO.SetActive(true);
            if (!gameObject.activeInHierarchy) return;
            if (panelAnim != null) StopCoroutine(panelAnim);
            panelAnim = StartCoroutine(AnimateRightPanelIn());
        }

        private IEnumerator AnimateRightPanelIn()
        {
            float w = containerRT != null ? containerRT.rect.width : 1920f;
            var cg = rightPanelRT != null ? rightPanelRT.GetComponent<CanvasGroup>() : null;
            if (cg == null && rightPanelRT != null) cg = rightPanelRT.gameObject.AddComponent<CanvasGroup>();
            float dur = 0.32f, t = 0f;
            Vector2 leftFrom = leftPanelRT != null ? leftPanelRT.anchoredPosition : Vector2.zero;
            Vector2 closeFrom = closeRT != null ? closeRT.anchoredPosition : Vector2.zero;
            if (closeRT != null) { var cc = closeRT.GetComponent<CanvasGroup>(); }
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / dur);
                float e = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
                if (leftPanelRT != null) leftPanelRT.anchoredPosition = Vector2.Lerp(leftFrom, Vector2.zero, e);
                if (closeRT != null) closeRT.anchoredPosition = Vector2.Lerp(closeFrom, Vector2.zero, e);
                if (rightPanelRT != null)
                {
                    rightPanelRT.anchoredPosition = new Vector2((1f - e) * 0.05f * w, 0f);
                    rightPanelRT.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, e);
                }
                if (cg != null) cg.alpha = e;
                yield return null;
            }
            if (leftPanelRT != null) leftPanelRT.anchoredPosition = Vector2.zero;
            if (closeRT != null) closeRT.anchoredPosition = Vector2.zero;
            if (rightPanelRT != null) { rightPanelRT.anchoredPosition = Vector2.zero; rightPanelRT.localScale = Vector3.one; }
            if (cg != null) cg.alpha = 1f;
            panelAnim = null;
        }

        private void DisplayEmptyInfo()
        {
            SetRightPanelVisible(false);
            DisplayEmptyInfoInner();
        }

        private void DisplayEmptyInfoInner()
        {
            if (thumbnailIconImg != null)
            {
                thumbnailIconImg.enabled = false;
            }
            if (titleText != null)
            {
                titleText.text = "NO SAMPLE";
            }
            if (subtitleText != null)
            {
                subtitleText.text = "COLLECT SAMPLES IN THE WORLD";
            }
            if (hardnessFillImg != null)
            {
                hardnessFillImg.fillAmount = 0f;
            }
            if (rarityFillImg != null)
            {
                rarityFillImg.fillAmount = 0f;
            }
            if (descriptionText != null)
            {
                descriptionText.text = "Select an inventory slot containing an archaeological stone specimen to view its geological properties, hardness, rarity, and 3D structure.";
            }
            if (inspectButton != null)
            {
                inspectButton.interactable = false;
            }
        }

        private string CleanRockName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "BASALT";
            string s = raw;
            // Clean prefixes like "Rock 1 - ", "Rock_2_", etc.
            if (s.Contains("-"))
            {
                var parts = s.Split('-');
                s = parts[parts.Length - 1].Trim();
            }
            if (s.Contains("("))
            {
                // Take English or primary name e.g. "Silex (Flint)" -> "SILEX" or "FLINT"
                int p1 = s.IndexOf('(');
                s = s.Substring(0, p1).Trim();
            }
            return s.Trim();
        }

        private string GetRockSubtitle(RockItem rock)
        {
            if (rock == null) return "THE GAME PRIMARY UNDERGROUND";
            string n = rock.name.ToLower();
            if (rock.rockData != null) n = rock.rockData.rockName.ToLower();

            if (n.Contains("basalt")) return "THE GAME PRIMARY UNDERGROUND";
            if (n.Contains("silex") || n.Contains("flint")) return "KNAPPABLE LEVALLOIS CORE";
            if (n.Contains("grès") || n.Contains("gres") || n.Contains("sandstone")) return "HARD PERCUSSIVE HAMMERSTONE";
            if (n.Contains("calcaire") || n.Contains("limestone")) return "PIGMENT SOURCE & CHALKY MATRIX";

            return "ARCHAEOLOGICAL SPECIMEN";
        }

        private string GetDefaultDescription(string rockName)
        {
            string n = rockName.ToLower();
            if (n.Contains("basalt")) return "Dark volcanic rock. Used as forge flux and for heavy tools.";
            if (n.Contains("silex") || n.Contains("flint")) return "Extreme hardness with razor conchoidal cleavage. Produces sharp flakes and spear points.";
            if (n.Contains("grès") || n.Contains("sandstone")) return "High friction granular rock. Ideal as hammerstones, grinders, and abrasion tools.";
            if (n.Contains("calcaire") || n.Contains("limestone")) return "Soft chalky rock easily abraded to produce white archaeological marking pigment.";
            return "Prehistoric stone sample discovered during Jbel Ighoud archaeological exploration.";
        }

        private readonly System.Collections.Generic.Dictionary<JbelIghoud.Lithic.RockData, Sprite> fragmentIcons = new System.Collections.Generic.Dictionary<JbelIghoud.Lithic.RockData, Sprite>();

        public Sprite GetSpriteForRock(RockItem rock)
        {
            // fragment piles and the hammerstone have their own rendered icon (Resources/FragmentIcons/<data>)
            if (rock != null && (rock.IsFragment || rock.isHammerstone) && rock.rockData != null)
            {
                if (!fragmentIcons.TryGetValue(rock.rockData, out var fs))
                {
                    fs = Resources.Load<Sprite>("FragmentIcons/" + rock.rockData.name);
                    fragmentIcons[rock.rockData] = fs;
                }
                if (fs != null) return fs;
            }

            if (rock == null) return fallbackRockSprite;
            string n = (rock.rockData != null ? rock.rockData.rockName : rock.name).ToLower();

            if (n.Contains("basalt")) return basaltSprite != null ? basaltSprite : fallbackRockSprite;
            if (n.Contains("silex") || n.Contains("flint")) return silexSprite != null ? silexSprite : fallbackRockSprite;
            if (n.Contains("grès") || n.Contains("gres") || n.Contains("sandstone")) return gresSprite != null ? gresSprite : fallbackRockSprite;
            if (n.Contains("calcaire") || n.Contains("limestone")) return calcaireSprite != null ? calcaireSprite : fallbackRockSprite;

            return fallbackRockSprite;
        }

        // =========================================================
        // --- ACTION BUTTONS & INSPECTION ---
        // =========================================================

        /// <summary>Splits the INSPECT button area into INSPECT + TAKE (built at runtime from the INSPECT button).</summary>
        private void EnsureTakeButton()
        {
            if (takeButton != null || inspectButton == null) return;
            Transform parent = inspectButton.transform.parent;
            Transform existing = parent.Find("TakeButton");
            GameObject go = existing != null ? existing.gameObject : Instantiate(inspectButton.gameObject, parent);
            go.name = "TakeButton";
            takeButton = go.GetComponent<Button>();
            takeButton.onClick = new Button.ButtonClickedEvent();
            foreach (var t in go.GetComponentsInChildren<TMP_Text>(true)) t.text = "Equip";

            Transform existingB = parent.Find("BreakButton");
            GameObject bgo = existingB != null ? existingB.gameObject : Instantiate(inspectButton.gameObject, parent);
            bgo.name = "BreakButton";
            breakButton = bgo.GetComponent<Button>();
            breakButton.onClick = new Button.ButtonClickedEvent();
            foreach (var t in bgo.GetComponentsInChildren<TMP_Text>(true)) t.text = "Break";

            // INSPECT | TAKE | BREAK side by side
            var irt = inspectButton.GetComponent<RectTransform>();
            var trt = go.GetComponent<RectTransform>();
            var brt = bgo.GetComponent<RectTransform>();
            float y0 = irt.anchorMin.y, y1 = irt.anchorMax.y;
            irt.anchorMin = new Vector2(0.03f, y0); irt.anchorMax = new Vector2(0.33f, y1);
            trt.anchorMin = new Vector2(0.35f, y0); trt.anchorMax = new Vector2(0.65f, y1);
            brt.anchorMin = new Vector2(0.67f, y0); brt.anchorMax = new Vector2(0.97f, y1);
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            brt.offsetMin = brt.offsetMax = Vector2.zero;
            foreach (var t in parent.GetComponentsInChildren<TMP_Text>(true))
                if (t.transform.IsChildOf(irt) || t.transform.IsChildOf(trt) || t.transform.IsChildOf(brt)) t.enableAutoSizing = true;
        }

        /// <summary>EQUIP: put the selected item in the hotbar and in hand. UNEQUIP: take it off the hotbar.</summary>
        private void OnTakeButtonClicked()
        {
            var rock = currentSelectedRock;
            var bar = JbelHotbar.Instance;
            var inv = InventoryManager.Instance;
            if (rock == null || bar == null || inv == null) return;
            if (bar.IsPinned(rock)) bar.Unpin(rock);
            else
            {
                if (!bar.Pin(rock)) { inv.Notify("Your hotbar is full. Unequip something first.", new Color(1f, 0.5f, 0.4f)); return; }
                bar.EquipItem(rock);
                OnRockTaken?.Invoke(rock);
            }
            selectedSlotIndex = -1;
            currentSelectedRock = null;
            RefreshInventoryUI();
        }

        private void LateUpdate()
        {
            if (!isOpen) return;
            var player = TopDownPlayerController.Instance;
            var sel = currentSelectedRock;
            bool isHammer = sel != null && sel.isHammerstone;
            bool strikable = sel != null && !sel.IsFragment && !isHammer;
            if (takeButton != null)
            {
                takeButton.interactable = sel != null;
                bool pinned = sel != null && JbelHotbar.Instance != null && JbelHotbar.Instance.IsPinned(sel);
                string want = pinned ? "Unequip" : "Equip";
                foreach (var t in takeButton.GetComponentsInChildren<TMP_Text>(true)) if (t.text != want) t.text = want;
            }
            if (breakButton != null)
            {
                breakButton.gameObject.SetActive(!isHammer);
                breakButton.interactable = strikable && FindHammerstone() != null;
            }

            if (!TutorialPulse) { ResetPulse(); return; }
            pulseT += Time.unscaledDeltaTime;
            float beat = Mathf.Abs(Mathf.Sin(pulseT * 3.6f));
            float s = 1f + 0.12f * beat * beat;
            bool selected = currentSelectedRock != null;
            if (slots != null)
                foreach (var sl in slots)
                    if (sl != null && sl.rootObj != null) sl.rootObj.transform.localScale = selected ? Vector3.one : Vector3.one * s;
            if (breakButton != null) breakButton.transform.localScale = selected ? Vector3.one * (1f + 0.1f * beat * beat) : Vector3.one;
        }

        /// <summary>\"x5\" badge on fragment stacks, slightly smaller icon so whole stones and pieces read differently.</summary>
        /// <summary>Stone pictures were tiny inside their slots: draw them this much bigger.</summary>
        private const float SlotIconScale = 1.75f;
        private const float CardIconScale = 1.5f;

        private void UpdateCountBadge(SampleSlotUI slot, RockItem rock)
        {
            if (slot == null || slot.rootObj == null) return;
            Transform badgeT = slot.rootObj.transform.Find("CountBadge");
            bool frag = rock != null && rock.IsFragment;
            bool rendered = frag || (rock != null && rock.isHammerstone);
            if (slot.iconImg != null) slot.iconImg.rectTransform.localScale = Vector3.one * (rendered ? 1.1f : SlotIconScale); // fragment icons already fill their frame
            if (!frag) { if (badgeT != null) badgeT.gameObject.SetActive(false); return; }
            TextMeshProUGUI badge;
            if (badgeT == null)
            {
                var go = new GameObject("CountBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(slot.rootObj.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-10f, 8f);
                rt.sizeDelta = new Vector2(90f, 40f);
                badge = go.GetComponent<TextMeshProUGUI>();
                if (titleText != null) badge.font = titleText.font;
                badge.fontSize = 30f;
                badge.fontStyle = FontStyles.Bold;
                badge.alignment = TextAlignmentOptions.BottomRight;
                badge.color = new Color(0.98f, 0.86f, 0.55f);
                badge.raycastTarget = false;
                badge.outlineWidth = 0.18f;
                badge.outlineColor = new Color32(30, 18, 8, 255);
            }
            else badge = badgeT.GetComponent<TextMeshProUGUI>();
            badge.gameObject.SetActive(true);
            badge.text = "x" + rock.fragmentCount;
        }

        private void ResetPulse()
        {
            if (pulseT == 0f) return;
            pulseT = 0f;
            if (slots != null) foreach (var sl in slots) if (sl != null && sl.rootObj != null) sl.rootObj.transform.localScale = Vector3.one;
            if (takeButton != null) takeButton.transform.localScale = Vector3.one;
            if (breakButton != null) breakButton.transform.localScale = Vector3.one;
        }

        // ------------------------------------------------ BREAK (chosen stone vs the hammerstone)
        public static RockItem FindHammerstone()
        {
            var inv = InventoryManager.Instance;
            if (inv != null) foreach (var it in inv.Items) if (it != null && it.isHammerstone) return it;
            var pl = TopDownPlayerController.Instance;
            if (pl != null) foreach (var h in pl.HeldRocks) if (h != null && h.isHammerstone) return h;
            return null;
        }

        private void OnBreakButtonClicked()
        {
            var rock = currentSelectedRock;
            var hammer = FindHammerstone();
            var inv = InventoryManager.Instance;
            var player = TopDownPlayerController.Instance;
            var drag = RockDragManager.Instance;
            if (rock == null || hammer == null || inv == null || player == null || drag == null || rock == hammer || rock.IsFragment) return;

            // only the chosen stone + the hammerstone in hand
            foreach (var held in new List<RockItem>(player.HeldRocks)) if (held != null && held != hammer) inv.AddItem(held);
            if (player.MaxHeldRocks < 2) player.MaxHeldRocks = 2;
            if (!inv.EquipToHand(rock)) return;
            if (!hammer.isHeld && !inv.EquipToHand(hammer)) { inv.AddItem(rock); return; }

            selectedSlotIndex = -1;
            currentSelectedRock = null;
            CloseInventory();

            System.Action back = null;
            back = () =>
            {
                drag.OnSmashModeExited -= back;
                // whatever survived (and the hammerstone) goes back into the bag
                var pl = TopDownPlayerController.Instance;
                var bag = InventoryManager.Instance;
                if (pl == null || bag == null) return;
                foreach (var h in new List<RockItem>(pl.HeldRocks)) if (h != null) bag.AddItem(h);
            };
            drag.OnSmashModeExited += back;
            drag.StartFirstPersonSmash(rock, hammer); // chosen stone left, hammerstone right
            OnBreakStarted?.Invoke(rock);
        }

        private void OnInspectButtonClicked()
        {
            if (currentSelectedRock == null) return;

            var rockToInspect = currentSelectedRock;
            CloseInventory();

            var inspectionManager = RockInspectionManager.Instance;
            if (inspectionManager != null)
            {
                inspectionManager.OpenFullScreenInspection(rockToInspect, true);
            }
            else
            {
                Debug.LogWarning("[JbelSamplesInventory] RockInspectionManager not found in scene.");
            }
        }

        // =========================================================
        // --- MODAL ANIMATION & LIFECYCLE ---
        // =========================================================

        public void ToggleInventory()
        {
            if (isOpen) CloseInventory();
            else OpenInventory();
        }

        public void OpenInventory(Action onClose = null)
        {
            if (isOpen) return;
            isOpen = true;
            onCloseCallback = onClose;
            selectedSlotIndex = -1;

            gameObject.SetActive(true);
            if (modalCG != null)
            {
                modalCG.blocksRaycasts = true;
                modalCG.interactable = true;
            }

            AutoWire();
            WireListeners();
            LoadSprites();
            RefreshInventoryUI();

            // Lock player controls while viewing inventory
            var player = UnityEngine.Object.FindAnyObjectByType<TopDownPlayerController>();
            if (player != null) player.SetControlsLocked(true);

            var touch = UnityEngine.Object.FindAnyObjectByType<MobileFreeLookTouchController>();
            if (touch != null) touch.isInputDisabled = true;

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimateModal(true));
            OnOpened?.Invoke();

            Debug.Log("<color=gold>[JbelSamplesInventory] Inventory Modal Opened.</color>");
        }

        public void CloseInventory()
        {
            if (!isOpen) return;
            isOpen = false;

            // Restore player controls
            var player = UnityEngine.Object.FindAnyObjectByType<TopDownPlayerController>();
            if (player != null) player.SetControlsLocked(false);

            var touch = UnityEngine.Object.FindAnyObjectByType<MobileFreeLookTouchController>();
            if (touch != null) touch.isInputDisabled = false;

            if (animRoutine != null) StopCoroutine(animRoutine);
            animRoutine = StartCoroutine(AnimateModal(false));

            Debug.Log("<color=green>[JbelSamplesInventory] Inventory Modal Closed.</color>");
        }

        private IEnumerator AnimateModal(bool opening)
        {
            if (modalCG == null) yield break;

            if (opening)
            {
                modalCG.blocksRaycasts = true;
                modalCG.interactable = true;
                if (dimBackdrop != null) dimBackdrop.raycastTarget = true;
            }

            float t = 0f;
            float startAlpha = modalCG.alpha;
            float targetAlpha = opening ? 1f : 0f;

            Vector3 startScale = containerRT != null ? (opening ? Vector3.one * 0.94f : containerRT.localScale) : Vector3.one;
            Vector3 targetScale = opening ? Vector3.one : Vector3.one * 0.94f;

            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / animDuration);
                float smoothP = Mathf.SmoothStep(0f, 1f, p);

                modalCG.alpha = Mathf.Lerp(startAlpha, targetAlpha, smoothP);
                if (containerRT != null)
                {
                    containerRT.localScale = Vector3.Lerp(startScale, targetScale, smoothP);
                }
                yield return null;
            }

            modalCG.alpha = targetAlpha;

            if (!opening)
            {
                modalCG.blocksRaycasts = false;
                modalCG.interactable = false;
                if (dimBackdrop != null) dimBackdrop.raycastTarget = false;
                gameObject.SetActive(false);
                onCloseCallback?.Invoke();
                onCloseCallback = null;
            }

            animRoutine = null;
        }

        public void HideImmediate()
        {
            if (animRoutine != null) { StopCoroutine(animRoutine); animRoutine = null; }
            if (modalCG != null)
            {
                modalCG.alpha = 0f;
                modalCG.blocksRaycasts = false;
                modalCG.interactable = false;
            }
            if (dimBackdrop != null)
            {
                dimBackdrop.raycastTarget = false;
            }
            if (containerRT != null)
            {
                containerRT.localScale = Vector3.one * 0.94f;
            }
            gameObject.SetActive(false);
        }
    }
}
