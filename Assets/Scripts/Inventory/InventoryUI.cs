using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JbelIghoud.Lithic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Masterpiece AAA Archaeological Expedition Satchel & Weight Inventory UI for Jbel Ighoud.
/// Combines Death Stranding encumbrance bar, Witcher 3 split-dossier architecture,
/// authentic in-game 3D model renders, back arrow navigation, and in-hand return button.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    private static InventoryUI _instance;
    public static InventoryUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<InventoryUI>();
                if (_instance == null)
                {
                    Canvas c = FindAnyObjectByType<Canvas>();
                    GameObject go = c != null ? c.gameObject : new GameObject("InventoryUI");
                    _instance = go.AddComponent<InventoryUI>();
                }
            }
            if (_instance != null) _instance.EnsureInitialized();
            return _instance;
        }
    }

    [Header("In-Game 3D Model Render Sprites")]
    [SerializeField] private Sprite inventoryIconSprite;
    [SerializeField] private Sprite silexModelSprite;
    [SerializeField] private Sprite gresModelSprite;
    [SerializeField] private Sprite calcaireModelSprite;
    [SerializeField] private Sprite backArrowSprite;
    [SerializeField] private Sprite cardFrameSprite;
    [SerializeField] private Sprite pedestalGlowSprite;

    private bool isInitialized = false;
    private bool isOpen = false;
    public bool IsOpen => isOpen;

    private TopDownPlayerController player;

    [Header("HUD Inventory Button (Editable & Removable in Unity)")]
    [Tooltip("The main HUD button that opens the satchel. If unassigned, automatically finds 'InventoryHUDButton'. Its position & RectTransform are 100% preserved.")]
    [SerializeField] private GameObject hudButtonObj;
    [SerializeField] private Button hudInvButton;
    [SerializeField] private TextMeshProUGUI hudWeightBadgeText;
    [SerializeField] private Image hudInvIconImg;

    [Header("Return to Satchel Action Button (Editable & Removable)")]
    [Tooltip("Floating button shown when player holds a rock. If unassigned, automatically finds 'ReturnToInventoryButton'.")]
    [SerializeField] private GameObject returnToInvButtonObj;
    [SerializeField] private Button returnToInvButton;
    [SerializeField] private TextMeshProUGUI returnToInvText;

    [Header("Expedition Modal Window (Editable & Removable)")]
    [Tooltip("The main fullscreen modal root. If unassigned, automatically finds 'ExpeditionInventoryModal'.")]
    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button headerBackButton;
    [SerializeField] private Button headerCloseButton;
    [SerializeField] private Image weightBarFill;
    [SerializeField] private TextMeshProUGUI weightText;
    [SerializeField] private TextMeshProUGUI weightStatusBadgeText;
    [SerializeField] private Image weightStatusBadgeBg;
    [SerializeField] private TextMeshProUGUI itemCountText;

    [Header("Modal Panes & Controls")]
    [SerializeField] private Transform itemListContent;
    [SerializeField] private GameObject emptyStateObj;
    [SerializeField] private GameObject dossierEmptyNotice;
    [SerializeField] private GameObject dossierContentGroup;
    [SerializeField] private Image dossierModelImage;
    [SerializeField] private Image dossierPedestalImage;
    [SerializeField] private TextMeshProUGUI dossierTitleText;
    [SerializeField] private TextMeshProUGUI dossierWeightStatText;
    [SerializeField] private TextMeshProUGUI dossierHardnessStatText;
    [SerializeField] private TextMeshProUGUI dossierDurabilityStatText;
    [SerializeField] private TextMeshProUGUI dossierToughnessStatText;
    [SerializeField] private TextMeshProUGUI dossierDescText;
    [SerializeField] private Button dossierInspect3DButton;
    [SerializeField] private Button dossierDropButton;
    [SerializeField] private Button dossierEquipButton;
    [SerializeField] private Button dossierBookButton;

    [Header("Toast Notification Banner (Editable & Removable)")]
    [SerializeField] private GameObject toastBannerObj;
    [SerializeField] private TextMeshProUGUI toastText;
    [SerializeField] private Image toastBg;
    private float toastTimer = 0f;

    // Runtime Selection
    private RockItem selectedRock = null;
    private List<GameObject> activeItemCards = new List<GameObject>();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;

        player = FindAnyObjectByType<TopDownPlayerController>();
        LoadSprites();
        BuildHUDButton();
        BuildReturnToInvButton();
        BuildInventoryModal();
        BuildToastBanner();

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += OnInventoryDataChanged;
            InventoryManager.Instance.OnInventoryNotification += ShowToastNotification;
        }

        CloseInventory();
        UpdateHUDButtonVisuals();
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryDataChanged;
            InventoryManager.Instance.OnInventoryNotification -= ShowToastNotification;
        }
    }

    public void LoadSprites()
    {
        if (inventoryIconSprite == null) inventoryIconSprite = LoadSprite("Assets/UI/Textures/ui_inventory_icon.png");
        if (silexModelSprite == null) silexModelSprite = LoadSprite("Assets/UI/Textures/model_preview_silex.png");
        if (gresModelSprite == null) gresModelSprite = LoadSprite("Assets/UI/Textures/model_preview_gres.png");
        if (calcaireModelSprite == null) calcaireModelSprite = LoadSprite("Assets/UI/Textures/model_preview_calcaire.png");
        if (backArrowSprite == null) backArrowSprite = LoadSprite("Assets/UI/Textures/ui_back_arrow_icon.png");
        if (cardFrameSprite == null) cardFrameSprite = LoadSprite("Assets/UI/Textures/ui_ornate_card_frame.png");
        if (pedestalGlowSprite == null) pedestalGlowSprite = LoadSprite("Assets/UI/Textures/ui_pedestal_glow_ring.png");
    }

    private static Sprite LoadSprite(string path)
    {
#if UNITY_EDITOR
        var s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
#endif
        if (System.IO.File.Exists(path))
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (tex.LoadImage(bytes))
            {
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }
        return null;
    }

    public Sprite GetRockModelSprite(RockItem rock)
    {
        if (rock == null) return inventoryIconSprite;
        string rName = rock.rockData != null ? rock.rockData.rockName.ToLower() : rock.name.ToLower();
        if (rName.Contains("silex") || rName.Contains("flint"))
        {
            if (silexModelSprite == null) silexModelSprite = LoadSprite("Assets/UI/Textures/model_preview_silex.png");
            return silexModelSprite != null ? silexModelSprite : inventoryIconSprite;
        }
        if (rName.Contains("grès") || rName.Contains("sandstone") || rName.Contains("gres"))
        {
            if (gresModelSprite == null) gresModelSprite = LoadSprite("Assets/UI/Textures/model_preview_gres.png");
            return gresModelSprite != null ? gresModelSprite : inventoryIconSprite;
        }
        if (calcaireModelSprite == null) calcaireModelSprite = LoadSprite("Assets/UI/Textures/model_preview_calcaire.png");
        return calcaireModelSprite != null ? calcaireModelSprite : inventoryIconSprite;
    }

    // =========================================================================
    // 1. HUD BUTTON (Above the Joystick on the Left)
    // =========================================================================
    private void BuildHUDButton()
    {
        Canvas hudCanvas = GetComponentInParent<Canvas>();
        if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();
        if (hudCanvas == null) return;

        // 1. If not assigned, search canvas for existing button (prioritize new Jbel HUD BagButton)
        if (hudButtonObj == null)
        {
            Transform bagTr = hudCanvas.transform.Find("JbelHUD_Panel/ActionButtonsCluster/BagButton");
            if (bagTr != null)
            {
                hudButtonObj = bagTr.gameObject;
            }
            else
            {
                Transform existing = hudCanvas.transform.Find("InventoryHUDButton");
                if (existing != null) hudButtonObj = existing.gameObject;
            }
        }

        // 2. NEVER destroy user objects & NEVER overwrite user RectTransform (anchoredPosition, anchors, size)
        if (hudButtonObj != null)
        {
            if (hudInvIconImg == null) hudInvIconImg = hudButtonObj.GetComponent<Image>();
            if (hudInvIconImg != null && hudInvIconImg.sprite == null && inventoryIconSprite != null)
            {
                hudInvIconImg.sprite = inventoryIconSprite;
            }

            if (hudInvButton == null) hudInvButton = hudButtonObj.GetComponent<Button>();
            if (hudInvButton == null) hudInvButton = hudButtonObj.AddComponent<Button>();
            hudInvButton.onClick.RemoveListener(ToggleInventory);
            hudInvButton.onClick.AddListener(ToggleInventory);

            if (hudWeightBadgeText == null)
            {
                Transform badgeTr = hudButtonObj.transform.Find("WeightBadge");
                if (badgeTr != null)
                {
                    hudWeightBadgeText = badgeTr.GetComponentInChildren<TextMeshProUGUI>();
                }
                else
                {
                    hudWeightBadgeText = hudButtonObj.GetComponentInChildren<TextMeshProUGUI>();
                }
            }

            return; // Fully preserved with user's exact position (e.g. 173.3, 521, 0)
        }

        // 3. Fallback only if no button exists anywhere
        hudButtonObj = new GameObject("InventoryHUDButton");
        hudButtonObj.transform.SetParent(hudCanvas.transform, false);

        RectTransform rt = hudButtonObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(173.3f, 521f); // Default to user's desired position
        rt.sizeDelta = new Vector2(96f, 96f);

        hudInvIconImg = hudButtonObj.AddComponent<Image>();
        hudInvIconImg.sprite = inventoryIconSprite;
        hudInvIconImg.color = Color.white;

        hudInvButton = hudButtonObj.AddComponent<Button>();
        hudInvButton.onClick.AddListener(ToggleInventory);

        Outline outline = hudButtonObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.92f, 0.78f, 0.40f, 0.85f);
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        // Weight badge below icon
        GameObject badgeObj = new GameObject("WeightBadge");
        badgeObj.transform.SetParent(hudButtonObj.transform, false);
        RectTransform badgeRt = badgeObj.AddComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0.5f, 0f);
        badgeRt.anchorMax = new Vector2(0.5f, 0f);
        badgeRt.pivot = new Vector2(0.5f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(0f, -8f);
        badgeRt.sizeDelta = new Vector2(104f, 26f);

        Image badgeBg = badgeObj.AddComponent<Image>();
        badgeBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);

        Outline bOutline = badgeObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.35f, 0.45f, 0.60f, 0.8f);
        bOutline.effectDistance = new Vector2(1f, -1f);

        GameObject badgeTextObj = new GameObject("BadgeText");
        badgeTextObj.transform.SetParent(badgeObj.transform, false);
        RectTransform btRt = badgeTextObj.AddComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.sizeDelta = Vector2.zero;

        hudWeightBadgeText = badgeTextObj.AddComponent<TextMeshProUGUI>();
        hudWeightBadgeText.fontSize = 11;
        hudWeightBadgeText.fontStyle = FontStyles.Bold;
        hudWeightBadgeText.alignment = TextAlignmentOptions.Center;
        hudWeightBadgeText.color = new Color(0.96f, 0.82f, 0.50f, 1.0f);
        hudWeightBadgeText.text = "0.0 / 200 KG";
    }

    // =========================================================================
    // 2. LARGE "RETURN TO INVENTORY" BUTTON (WHEN ROCK IS IN HAND)
    // =========================================================================
    private void BuildReturnToInvButton()
    {
        Canvas hudCanvas = GetComponentInParent<Canvas>();
        if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();
        if (hudCanvas == null) return;

        if (returnToInvButtonObj == null)
        {
            Transform existing = hudCanvas.transform.Find("ReturnToInventoryButton");
            if (existing != null) returnToInvButtonObj = existing.gameObject;
        }

        if (returnToInvButtonObj != null)
        {
            if (returnToInvButton == null) returnToInvButton = returnToInvButtonObj.GetComponent<Button>();
            if (returnToInvButton == null) returnToInvButton = returnToInvButtonObj.AddComponent<Button>();
            returnToInvButton.onClick.RemoveListener(OnReturnHeldRockToInventoryClicked);
            returnToInvButton.onClick.AddListener(OnReturnHeldRockToInventoryClicked);

            if (returnToInvText == null) returnToInvText = returnToInvButtonObj.GetComponentInChildren<TextMeshProUGUI>();
            returnToInvButtonObj.SetActive(false);
            return;
        }

        returnToInvButtonObj = new GameObject("ReturnToInventoryButton");
        returnToInvButtonObj.transform.SetParent(hudCanvas.transform, false);

        RectTransform rt = returnToInvButtonObj.AddComponent<RectTransform>();
        // Down large in the middle of the screen
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 65f);
        rt.sizeDelta = new Vector2(460f, 84f);

        Image btnBg = returnToInvButtonObj.AddComponent<Image>();
        btnBg.color = new Color(0.12f, 0.28f, 0.20f, 0.96f);

        Outline outline = returnToInvButtonObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.82f, 0.45f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

        returnToInvButton = returnToInvButtonObj.AddComponent<Button>();
        returnToInvButton.onClick.AddListener(OnReturnHeldRockToInventoryClicked);

        // Satchel icon on the left
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(returnToInvButtonObj.transform, false);
        RectTransform iconRt = iconObj.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(16f, 0f);
        iconRt.sizeDelta = new Vector2(58f, 58f);

        Image iconImg = iconObj.AddComponent<Image>();
        iconImg.sprite = inventoryIconSprite;
        iconImg.raycastTarget = false;

        // Button text
        GameObject txtObj = new GameObject("ButtonText");
        txtObj.transform.SetParent(returnToInvButtonObj.transform, false);
        RectTransform txtRt = txtObj.AddComponent<RectTransform>();
        txtRt.anchorMin = new Vector2(0f, 0f);
        txtRt.anchorMax = new Vector2(1f, 1f);
        txtRt.offsetMin = new Vector2(85f, 0f);
        txtRt.offsetMax = new Vector2(-15f, 0f);

        returnToInvText = txtObj.AddComponent<TextMeshProUGUI>();
        returnToInvText.fontSize = 17;
        returnToInvText.alignment = TextAlignmentOptions.MidlineLeft;
        returnToInvText.color = Color.white;
        returnToInvText.raycastTarget = false;
        returnToInvText.text = "<color=#F6C878><b>STORE IN SATCHEL</b></color>\n<size=12><color=#A8D5BA>Return held rock to inventory [R]</color></size>";

        returnToInvButtonObj.SetActive(false);
    }

    public void OnReturnHeldRockToInventoryClicked()
    {
        if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
        if (player == null || player.HeldRocks.Count == 0) return;

        RockItem heldRock = player.HeldRocks[player.HeldRocks.Count - 1];
        if (heldRock == null) return;

        if (InventoryManager.Instance != null && !InventoryManager.Instance.CanAddItem(heldRock))
        {
            ShowToastNotification($"Cannot store: {heldRock.GetWeight():F1} kg exceeds 200 kg capacity!", new Color(1f, 0.4f, 0.4f));
            return;
        }

        player.ReleaseHeldRock(heldRock);

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.AddItem(heldRock);
        }
    }

    private void UpdateHUDButtonVisuals()
    {
        if (hudWeightBadgeText == null || InventoryManager.Instance == null) return;

        float cur = InventoryManager.Instance.CurrentWeight;
        float max = InventoryManager.Instance.MaxWeight;
        hudWeightBadgeText.text = $"{cur:F1} / {max:F0} KG";

        float ratio = InventoryManager.Instance.WeightRatio;
        if (ratio > 0.90f) hudWeightBadgeText.color = new Color(1.0f, 0.35f, 0.35f, 1.0f);
        else if (ratio > 0.70f) hudWeightBadgeText.color = new Color(1.0f, 0.75f, 0.30f, 1.0f);
        else hudWeightBadgeText.color = new Color(0.96f, 0.82f, 0.50f, 1.0f);
    }

    // =========================================================================
    // 3. MASTERWORK AAA INVENTORY MODAL WITH BACK ARROW
    // =========================================================================
    private void BuildInventoryModal()
    {
        Canvas hudCanvas = GetComponentInParent<Canvas>();
        if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();
        if (hudCanvas == null) return;

        if (modalRoot == null)
        {
            Transform existing = hudCanvas.transform.Find("ExpeditionInventoryModal");
            if (existing != null) modalRoot = existing.gameObject;
        }

        // If the modal already exists in the scene, DO NOT destroy it or overwrite its RectTransform!
        if (modalRoot != null)
        {
            LinkExistingModalElements(modalRoot);
            modalRoot.SetActive(false);
            return;
        }

        modalRoot = new GameObject("ExpeditionInventoryModal");
        modalRoot.transform.SetParent(hudCanvas.transform, false);

        RectTransform rootRt = modalRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        Image backdrop = modalRoot.AddComponent<Image>();
        backdrop.color = new Color(0.03f, 0.04f, 0.06f, 0.94f);

        // Main Center Frame (1600 x 920)
        GameObject windowObj = new GameObject("WindowPanel");
        windowObj.transform.SetParent(modalRoot.transform, false);
        RectTransform winRt = windowObj.AddComponent<RectTransform>();
        winRt.anchorMin = new Vector2(0.5f, 0.5f);
        winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(1600f, 920f);
        winRt.anchoredPosition = Vector2.zero;

        Image winBg = windowObj.AddComponent<Image>();
        winBg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

        Outline winOutline = windowObj.AddComponent<Outline>();
        winOutline.effectColor = new Color(0.85f, 0.72f, 0.42f, 0.8f);
        winOutline.effectDistance = new Vector2(2f, -2f);

        // ---------------------------------------------------------
        // HEADER BAR WITH PROMINENT BACK ARROW
        // ---------------------------------------------------------
        GameObject headerObj = new GameObject("HeaderBar");
        headerObj.transform.SetParent(windowObj.transform, false);
        RectTransform headRt = headerObj.AddComponent<RectTransform>();
        headRt.anchorMin = new Vector2(0f, 1f);
        headRt.anchorMax = new Vector2(1f, 1f);
        headRt.pivot = new Vector2(0.5f, 1f);
        headRt.anchoredPosition = Vector2.zero;
        headRt.sizeDelta = new Vector2(0f, 125f);

        Image headBg = headerObj.AddComponent<Image>();
        headBg.color = new Color(0.04f, 0.05f, 0.08f, 0.95f);

        // ◄ BACK ARROW BUTTON ON LEFT
        GameObject backBtnObj = new GameObject("HeaderBackButton");
        backBtnObj.transform.SetParent(headerObj.transform, false);
        RectTransform bbRt = backBtnObj.AddComponent<RectTransform>();
        bbRt.anchorMin = new Vector2(0f, 0.5f);
        bbRt.anchorMax = new Vector2(0f, 0.5f);
        bbRt.pivot = new Vector2(0f, 0.5f);
        bbRt.anchoredPosition = new Vector2(30f, 0f);
        bbRt.sizeDelta = new Vector2(140f, 56f);

        Image bbImg = backBtnObj.AddComponent<Image>();
        bbImg.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);

        Outline bbOutline = backBtnObj.AddComponent<Outline>();
        bbOutline.effectColor = new Color(0.95f, 0.82f, 0.45f, 0.8f);
        bbOutline.effectDistance = new Vector2(1.5f, -1.5f);

        headerBackButton = backBtnObj.AddComponent<Button>();
        headerBackButton.onClick.AddListener(CloseInventory);

        GameObject bbTxtObj = new GameObject("Text");
        bbTxtObj.transform.SetParent(backBtnObj.transform, false);
        RectTransform bbtRt = bbTxtObj.AddComponent<RectTransform>();
        bbtRt.anchorMin = Vector2.zero;
        bbtRt.anchorMax = Vector2.one;
        bbtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI bbTxt = bbTxtObj.AddComponent<TextMeshProUGUI>();
        bbTxt.fontSize = 16;
        bbTxt.fontStyle = FontStyles.Bold;
        bbTxt.alignment = TextAlignmentOptions.Center;
        bbTxt.color = new Color(0.96f, 0.85f, 0.55f);
        bbTxt.text = "← BACK";
        bbTxt.raycastTarget = false;

        // Title and Subtitle
        GameObject titleObj = new GameObject("HeaderTitle");
        titleObj.transform.SetParent(headerObj.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.5f);
        titleRt.anchorMax = new Vector2(0f, 0.5f);
        titleRt.pivot = new Vector2(0f, 0.5f);
        titleRt.anchoredPosition = new Vector2(190f, 0f);
        titleRt.sizeDelta = new Vector2(440f, 95f);

        TextMeshProUGUI titleTxt = titleObj.AddComponent<TextMeshProUGUI>();
        titleTxt.fontSize = 24;
        titleTxt.text = "<color=#F6C878><b>EXPEDITION FIELD SATCHEL</b></color>\n<size=12><color=#8FA0B5>JBEL IGHOUD SITE • ~300,000 BP • HOMO SAPIENS LITHIC HORIZON</color></size>";

        // Death Stranding Encumbrance Gauge
        GameObject weightBarRoot = new GameObject("WeightEncumbranceBar");
        weightBarRoot.transform.SetParent(headerObj.transform, false);
        RectTransform wbRt = weightBarRoot.AddComponent<RectTransform>();
        wbRt.anchorMin = new Vector2(0.5f, 0.5f);
        wbRt.anchorMax = new Vector2(0.5f, 0.5f);
        wbRt.pivot = new Vector2(0.5f, 0.5f);
        wbRt.anchoredPosition = new Vector2(100f, 0f);
        wbRt.sizeDelta = new Vector2(680f, 80f);

        GameObject weightTextObj = new GameObject("WeightText");
        weightTextObj.transform.SetParent(weightBarRoot.transform, false);
        RectTransform wtRt = weightTextObj.AddComponent<RectTransform>();
        wtRt.anchorMin = new Vector2(0f, 1f);
        wtRt.anchorMax = new Vector2(1f, 1f);
        wtRt.pivot = new Vector2(0.5f, 1f);
        wtRt.anchoredPosition = new Vector2(0f, 0f);
        wtRt.sizeDelta = new Vector2(0f, 26f);

        weightText = weightTextObj.AddComponent<TextMeshProUGUI>();
        weightText.fontSize = 14;
        weightText.fontStyle = FontStyles.Bold;
        weightText.color = Color.white;
        weightText.text = "<b>CARRY LOAD: 0.0 / 200.0 KG (0%)</b>";

        GameObject trackObj = new GameObject("BarTrack");
        trackObj.transform.SetParent(weightBarRoot.transform, false);
        RectTransform trackRt = trackObj.AddComponent<RectTransform>();
        trackRt.anchorMin = new Vector2(0f, 0f);
        trackRt.anchorMax = new Vector2(1f, 0f);
        trackRt.pivot = new Vector2(0.5f, 0f);
        trackRt.anchoredPosition = new Vector2(0f, 14f);
        trackRt.sizeDelta = new Vector2(0f, 20f);

        Image trackImg = trackObj.AddComponent<Image>();
        trackImg.color = new Color(0.10f, 0.13f, 0.19f, 1.0f);

        Outline trackOutline = trackObj.AddComponent<Outline>();
        trackOutline.effectColor = new Color(0.25f, 0.35f, 0.50f, 0.6f);
        trackOutline.effectDistance = new Vector2(1f, -1f);

        GameObject fillObj = new GameObject("BarFill");
        fillObj.transform.SetParent(trackObj.transform, false);
        RectTransform fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.sizeDelta = Vector2.zero;

        weightBarFill = fillObj.AddComponent<Image>();
        weightBarFill.type = Image.Type.Filled;
        weightBarFill.fillMethod = Image.FillMethod.Horizontal;
        weightBarFill.fillOrigin = 0;
        weightBarFill.fillAmount = 0.05f;
        weightBarFill.color = new Color(0.0f, 0.90f, 0.64f, 1.0f);

        GameObject statusBadgeObj = new GameObject("StatusBadge");
        statusBadgeObj.transform.SetParent(weightBarRoot.transform, false);
        RectTransform sbRt = statusBadgeObj.AddComponent<RectTransform>();
        sbRt.anchorMin = new Vector2(1f, 1f);
        sbRt.anchorMax = new Vector2(1f, 1f);
        sbRt.pivot = new Vector2(1f, 1f);
        sbRt.anchoredPosition = new Vector2(0f, 2f);
        sbRt.sizeDelta = new Vector2(150f, 26f);

        weightStatusBadgeBg = statusBadgeObj.AddComponent<Image>();
        weightStatusBadgeBg.color = new Color(0.12f, 0.40f, 0.25f, 0.95f);

        GameObject sbTextObj = new GameObject("BadgeText");
        sbTextObj.transform.SetParent(statusBadgeObj.transform, false);
        RectTransform sbtRt = sbTextObj.AddComponent<RectTransform>();
        sbtRt.anchorMin = Vector2.zero;
        sbtRt.anchorMax = Vector2.one;
        sbtRt.sizeDelta = Vector2.zero;

        weightStatusBadgeText = sbTextObj.AddComponent<TextMeshProUGUI>();
        weightStatusBadgeText.fontSize = 11;
        weightStatusBadgeText.fontStyle = FontStyles.Bold;
        weightStatusBadgeText.alignment = TextAlignmentOptions.Center;
        weightStatusBadgeText.color = Color.white;
        weightStatusBadgeText.text = "OPTIMAL LOAD";

        // CLOSE 'X' BUTTON (Plain ASCII X)
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(headerObj.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(1f, 0.5f);
        closeRt.anchorMax = new Vector2(1f, 0.5f);
        closeRt.pivot = new Vector2(0.5f, 0.5f);
        closeRt.anchoredPosition = new Vector2(-60f, 0f);
        closeRt.sizeDelta = new Vector2(68f, 68f);

        Image closeImg = closeObj.AddComponent<Image>();
        closeImg.color = new Color(0.65f, 0.18f, 0.15f, 0.95f);

        Outline closeOutline = closeObj.AddComponent<Outline>();
        closeOutline.effectColor = new Color(0.96f, 0.82f, 0.50f, 0.9f);
        closeOutline.effectDistance = new Vector2(2f, -2f);

        Button closeBtn = closeObj.AddComponent<Button>();
        closeBtn.onClick.AddListener(CloseInventory);

        GameObject closeTxtObj = new GameObject("CloseText");
        closeTxtObj.transform.SetParent(closeObj.transform, false);
        RectTransform cTxtRt = closeTxtObj.AddComponent<RectTransform>();
        cTxtRt.anchorMin = Vector2.zero;
        cTxtRt.anchorMax = Vector2.one;
        cTxtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI cTxt = closeTxtObj.AddComponent<TextMeshProUGUI>();
        cTxt.fontSize = 32;
        cTxt.fontStyle = FontStyles.Bold;
        cTxt.alignment = TextAlignmentOptions.Center;
        cTxt.color = Color.white;
        cTxt.text = "X";
        cTxt.raycastTarget = false;

        // ---------------------------------------------------------
        // BODY SPLIT-PANE
        // ---------------------------------------------------------
        GameObject bodyObj = new GameObject("BodySplitPane");
        bodyObj.transform.SetParent(windowObj.transform, false);
        RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(28f, 28f);
        bodyRt.offsetMax = new Vector2(-28f, -140f);

        BuildItemListPane(bodyObj);
        BuildDossierPane(bodyObj);
    }

    private void BuildItemListPane(GameObject parent)
    {
        GameObject leftPane = new GameObject("LeftPane_ItemList");
        leftPane.transform.SetParent(parent.transform, false);
        RectTransform lpRt = leftPane.AddComponent<RectTransform>();
        lpRt.anchorMin = new Vector2(0f, 0f);
        lpRt.anchorMax = new Vector2(0.48f, 1f);
        lpRt.sizeDelta = Vector2.zero;

        Image lpBg = leftPane.AddComponent<Image>();
        lpBg.color = new Color(0.04f, 0.06f, 0.09f, 0.85f);

        Outline lpOutline = leftPane.AddComponent<Outline>();
        lpOutline.effectColor = new Color(0.20f, 0.28f, 0.38f, 0.6f);
        lpOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject listHead = new GameObject("ListHeader");
        listHead.transform.SetParent(leftPane.transform, false);
        RectTransform lhRt = listHead.AddComponent<RectTransform>();
        lhRt.anchorMin = new Vector2(0f, 1f);
        lhRt.anchorMax = new Vector2(1f, 1f);
        lhRt.pivot = new Vector2(0.5f, 1f);
        lhRt.anchoredPosition = Vector2.zero;
        lhRt.sizeDelta = new Vector2(0f, 48f);

        itemCountText = listHead.AddComponent<TextMeshProUGUI>();
        itemCountText.fontSize = 14;
        itemCountText.fontStyle = FontStyles.Bold;
        itemCountText.alignment = TextAlignmentOptions.MidlineLeft;
        itemCountText.margin = new Vector4(24f, 0f, 0f, 0f);
        itemCountText.color = new Color(0.96f, 0.82f, 0.50f);
        itemCountText.text = "COLLECTED LITHIC SPECIMENS (0)";

        GameObject scrollObj = new GameObject("ItemScrollRect");
        scrollObj.transform.SetParent(leftPane.transform, false);
        RectTransform sRt = scrollObj.AddComponent<RectTransform>();
        sRt.anchorMin = Vector2.zero;
        sRt.anchorMax = Vector2.one;
        sRt.offsetMin = new Vector2(12f, 12f);
        sRt.offsetMax = new Vector2(-12f, -54f);

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewObj = new GameObject("Viewport");
        viewObj.transform.SetParent(scrollObj.transform, false);
        RectTransform vRt = viewObj.AddComponent<RectTransform>();
        vRt.anchorMin = Vector2.zero;
        vRt.anchorMax = Vector2.one;
        vRt.sizeDelta = Vector2.zero;
        viewObj.AddComponent<RectMask2D>();

        GameObject contentObj = new GameObject("Content");
        contentObj.transform.SetParent(viewObj.transform, false);
        RectTransform cRt = contentObj.AddComponent<RectTransform>();
        cRt.anchorMin = new Vector2(0f, 1f);
        cRt.anchorMax = new Vector2(1f, 1f);
        cRt.pivot = new Vector2(0.5f, 1f);
        cRt.anchoredPosition = Vector2.zero;
        cRt.sizeDelta = new Vector2(0f, 400f);

        VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10f;
        vlg.padding = new RectOffset(6, 6, 8, 8);
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vRt;
        scrollRect.content = cRt;
        itemListContent = contentObj.transform;

        emptyStateObj = new GameObject("EmptyStateNotice");
        emptyStateObj.transform.SetParent(leftPane.transform, false);
        RectTransform esRt = emptyStateObj.AddComponent<RectTransform>();
        esRt.anchorMin = new Vector2(0.08f, 0.2f);
        esRt.anchorMax = new Vector2(0.92f, 0.8f);
        esRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI esTxt = emptyStateObj.AddComponent<TextMeshProUGUI>();
        esTxt.fontSize = 15;
        esTxt.alignment = TextAlignmentOptions.Center;
        esTxt.enableWordWrapping = true;
        esTxt.color = new Color(0.68f, 0.78f, 0.90f);
        esTxt.text = "<color=#F6C878><b>Expedition Satchel is Empty</b></color>\n\n" +
                     "Walk through the prehistoric cave & landscape.\n\n" +
                     "Approach rocks, tap <b>INSPECT</b>, and press <b>'STORE IN SATCHEL'</b> to add specimens to your inventory.";
    }

    private void BuildDossierPane(GameObject parent)
    {
        GameObject rightPane = new GameObject("RightPane_Dossier");
        rightPane.transform.SetParent(parent.transform, false);
        RectTransform rpRt = rightPane.AddComponent<RectTransform>();
        rpRt.anchorMin = new Vector2(0.50f, 0f);
        rpRt.anchorMax = new Vector2(1f, 1f);
        rpRt.sizeDelta = Vector2.zero;

        Image rpBg = rightPane.AddComponent<Image>();
        rpBg.color = new Color(0.05f, 0.07f, 0.10f, 0.90f);

        Outline outline = rightPane.AddComponent<Outline>();
        outline.effectColor = new Color(0.24f, 0.32f, 0.44f, 0.6f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        dossierEmptyNotice = new GameObject("DossierEmptyNotice");
        dossierEmptyNotice.transform.SetParent(rightPane.transform, false);
        RectTransform denRt = dossierEmptyNotice.AddComponent<RectTransform>();
        denRt.anchorMin = Vector2.zero;
        denRt.anchorMax = Vector2.one;
        denRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI denTxt = dossierEmptyNotice.AddComponent<TextMeshProUGUI>();
        denTxt.fontSize = 16;
        denTxt.alignment = TextAlignmentOptions.Center;
        denTxt.color = new Color(0.55f, 0.65f, 0.78f);
        denTxt.text = "Select a rock specimen from the left satchel roster to view detailed lithic analysis.";

        dossierContentGroup = new GameObject("DossierContentGroup");
        dossierContentGroup.transform.SetParent(rightPane.transform, false);
        RectTransform dcgRt = dossierContentGroup.AddComponent<RectTransform>();
        dcgRt.anchorMin = Vector2.zero;
        dcgRt.anchorMax = Vector2.one;
        dcgRt.offsetMin = new Vector2(30f, 30f);
        dcgRt.offsetMax = new Vector2(-30f, -30f);

        // Top Section: Large 3D Model Render & Pedestal
        GameObject topSection = new GameObject("DossierTopSection");
        topSection.transform.SetParent(dossierContentGroup.transform, false);
        RectTransform tsRt = topSection.AddComponent<RectTransform>();
        tsRt.anchorMin = new Vector2(0f, 1f);
        tsRt.anchorMax = new Vector2(1f, 1f);
        tsRt.pivot = new Vector2(0.5f, 1f);
        tsRt.anchoredPosition = Vector2.zero;
        tsRt.sizeDelta = new Vector2(0f, 160f);

        // Pedestal Glow
        GameObject pedObj = new GameObject("PedestalGlow");
        pedObj.transform.SetParent(topSection.transform, false);
        RectTransform pRt = pedObj.AddComponent<RectTransform>();
        pRt.anchorMin = new Vector2(0f, 0.5f);
        pRt.anchorMax = new Vector2(0f, 0.5f);
        pRt.pivot = new Vector2(0f, 0.5f);
        pRt.anchoredPosition = Vector2.zero;
        pRt.sizeDelta = new Vector2(160f, 160f);

        dossierPedestalImage = pedObj.AddComponent<Image>();
        dossierPedestalImage.sprite = pedestalGlowSprite;
        dossierPedestalImage.color = new Color(1f, 1f, 1f, 0.85f);
        dossierPedestalImage.raycastTarget = false;

        // In-game 3D Model Render
        GameObject modelObj = new GameObject("InGameModelPreview");
        modelObj.transform.SetParent(topSection.transform, false);
        RectTransform mRt = modelObj.AddComponent<RectTransform>();
        mRt.anchorMin = new Vector2(0f, 0.5f);
        mRt.anchorMax = new Vector2(0f, 0.5f);
        mRt.pivot = new Vector2(0f, 0.5f);
        mRt.anchoredPosition = new Vector2(5f, 0f);
        mRt.sizeDelta = new Vector2(150f, 150f);

        dossierModelImage = modelObj.AddComponent<Image>();
        dossierModelImage.preserveAspect = true;
        dossierModelImage.color = Color.white;
        dossierModelImage.raycastTarget = false;

        // Title and Era
        GameObject titleGroup = new GameObject("DossierTitleGroup");
        titleGroup.transform.SetParent(topSection.transform, false);
        RectTransform tgRt = titleGroup.AddComponent<RectTransform>();
        tgRt.anchorMin = new Vector2(0f, 0f);
        tgRt.anchorMax = new Vector2(1f, 1f);
        tgRt.offsetMin = new Vector2(175f, 0f);
        tgRt.offsetMax = Vector2.zero;

        dossierTitleText = titleGroup.AddComponent<TextMeshProUGUI>();
        dossierTitleText.fontSize = 24;
        dossierTitleText.fontStyle = FontStyles.Bold;
        dossierTitleText.enableWordWrapping = true;
        dossierTitleText.color = new Color(0.96f, 0.82f, 0.50f);
        dossierTitleText.text = "[ IN-GAME ROCK SPECIMEN ]";

        // Stats Grid: 4 Metric Cards
        GameObject statsGrid = new GameObject("StatsGrid");
        statsGrid.transform.SetParent(dossierContentGroup.transform, false);
        RectTransform sgRt = statsGrid.AddComponent<RectTransform>();
        sgRt.anchorMin = new Vector2(0f, 1f);
        sgRt.anchorMax = new Vector2(1f, 1f);
        sgRt.pivot = new Vector2(0.5f, 1f);
        sgRt.anchoredPosition = new Vector2(0f, -170f);
        sgRt.sizeDelta = new Vector2(0f, 105f);

        GridLayoutGroup glg = statsGrid.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(170f, 96f);
        glg.spacing = new Vector2(12f, 10f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 4;

        dossierWeightStatText = CreateMetricCard(statsGrid, "WEIGHT", "0.0 kg", new Color(0.96f, 0.82f, 0.50f));
        dossierHardnessStatText = CreateMetricCard(statsGrid, "MOHS HARDNESS", "0.0 / 20", new Color(0.40f, 0.85f, 1.0f));
        dossierDurabilityStatText = CreateMetricCard(statsGrid, "DURABILITY", "0 / 0 Hits", new Color(0.50f, 0.95f, 0.65f));
        dossierToughnessStatText = CreateMetricCard(statsGrid, "TOUGHNESS", "0.00", new Color(0.95f, 0.65f, 0.35f));

        // Narrative / Description
        GameObject descObj = new GameObject("DossierDescription");
        descObj.transform.SetParent(dossierContentGroup.transform, false);
        RectTransform descRt = descObj.AddComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.offsetMin = new Vector2(0f, 90f);
        descRt.offsetMax = new Vector2(0f, -290f);

        dossierDescText = descObj.AddComponent<TextMeshProUGUI>();
        dossierDescText.fontSize = 15;
        dossierDescText.lineSpacing = 8f;
        dossierDescText.enableWordWrapping = true;
        dossierDescText.color = new Color(0.88f, 0.92f, 0.98f);
        dossierDescText.text = "Archaeological field notes...";

        // Action Buttons Row (Bottom 70px)
        GameObject actionsRow = new GameObject("ActionsRow");
        actionsRow.transform.SetParent(dossierContentGroup.transform, false);
        RectTransform actRt = actionsRow.AddComponent<RectTransform>();
        actRt.anchorMin = new Vector2(0f, 0f);
        actRt.anchorMax = new Vector2(1f, 0f);
        actRt.pivot = new Vector2(0.5f, 0f);
        actRt.anchoredPosition = Vector2.zero;
        actRt.sizeDelta = new Vector2(0f, 70f);

        HorizontalLayoutGroup hlg = actionsRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        dossierInspect3DButton = CreateActionButton(actionsRow, "🔍 3D FULLSCREEN INSPECT", new Color(0.14f, 0.38f, 0.28f, 0.96f), OnInspect3DClicked);
        dossierDropButton = CreateActionButton(actionsRow, "⬇ DROP TO GROUND", new Color(0.48f, 0.20f, 0.18f, 0.96f), OnDropClicked);
        dossierEquipButton = CreateActionButton(actionsRow, "✋ EQUIP TO HAND", new Color(0.58f, 0.42f, 0.18f, 0.96f), OnEquipClicked);
        dossierBookButton = CreateActionButton(actionsRow, "📖 ARCHAEOLOGICAL CODEX", new Color(0.22f, 0.32f, 0.48f, 0.96f), OnBookClicked);
    }

    private TextMeshProUGUI CreateMetricCard(GameObject parent, string title, string defaultVal, Color accentColor)
    {
        GameObject cardObj = new GameObject($"MetricCard_{title}");
        cardObj.transform.SetParent(parent.transform, false);

        Image cardBg = cardObj.AddComponent<Image>();
        cardBg.color = new Color(0.08f, 0.11f, 0.16f, 0.95f);

        Outline o = cardObj.AddComponent<Outline>();
        o.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.4f);
        o.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(cardObj.transform, false);
        RectTransform tRt = titleObj.AddComponent<RectTransform>();
        tRt.anchorMin = new Vector2(0f, 1f);
        tRt.anchorMax = new Vector2(1f, 1f);
        tRt.pivot = new Vector2(0.5f, 1f);
        tRt.anchoredPosition = new Vector2(0f, -8f);
        tRt.sizeDelta = new Vector2(0f, 20f);

        TextMeshProUGUI tTxt = titleObj.AddComponent<TextMeshProUGUI>();
        tTxt.fontSize = 11;
        tTxt.fontStyle = FontStyles.Bold;
        tTxt.alignment = TextAlignmentOptions.Center;
        tTxt.color = new Color(0.65f, 0.75f, 0.88f);
        tTxt.text = title;
        tTxt.raycastTarget = false;

        GameObject valObj = new GameObject("Value");
        valObj.transform.SetParent(cardObj.transform, false);
        RectTransform vRt = valObj.AddComponent<RectTransform>();
        vRt.anchorMin = new Vector2(0f, 0f);
        vRt.anchorMax = new Vector2(1f, 1f);
        vRt.offsetMin = new Vector2(4f, 8f);
        vRt.offsetMax = new Vector2(-4f, -28f);

        TextMeshProUGUI vTxt = valObj.AddComponent<TextMeshProUGUI>();
        vTxt.fontSize = 17;
        vTxt.fontStyle = FontStyles.Bold;
        vTxt.alignment = TextAlignmentOptions.Center;
        vTxt.color = accentColor;
        vTxt.text = defaultVal;
        vTxt.raycastTarget = false;

        return vTxt;
    }

    private Button CreateActionButton(GameObject parent, string text, Color bgColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject($"ActionBtn_{text}");
        btnObj.transform.SetParent(parent.transform, false);

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        btn.onClick.AddListener(onClick);

        Outline o = btnObj.AddComponent<Outline>();
        o.effectColor = new Color(1f, 0.95f, 0.75f, 0.45f);
        o.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject txtObj = new GameObject("Text");
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform tRt = txtObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI t = txtObj.AddComponent<TextMeshProUGUI>();
        t.fontSize = 13;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color = Color.white;
        t.text = text;
        t.raycastTarget = false;

        return btn;
    }

    // =========================================================================
    // 4. TOAST NOTIFICATION BANNER
    // =========================================================================
    private void BuildToastBanner()
    {
        Canvas hudCanvas = GetComponentInParent<Canvas>();
        if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();
        if (hudCanvas == null) return;

        if (toastBannerObj == null)
        {
            Transform existing = hudCanvas.transform.Find("InventoryToastBanner");
            if (existing != null) toastBannerObj = existing.gameObject;
        }

        if (toastBannerObj != null)
        {
            if (toastBg == null) toastBg = toastBannerObj.GetComponent<Image>();
            if (toastText == null) toastText = toastBannerObj.GetComponentInChildren<TextMeshProUGUI>();
            toastBannerObj.SetActive(false);
            return;
        }

        toastBannerObj = new GameObject("InventoryToastBanner");
        toastBannerObj.transform.SetParent(hudCanvas.transform, false);

        RectTransform rt = toastBannerObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -25f);
        rt.sizeDelta = new Vector2(660f, 56f);

        toastBg = toastBannerObj.AddComponent<Image>();
        toastBg.color = new Color(0.06f, 0.09f, 0.14f, 0.96f);

        Outline o = toastBannerObj.AddComponent<Outline>();
        o.effectColor = new Color(0.92f, 0.78f, 0.42f, 0.85f);
        o.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject txtObj = new GameObject("ToastText");
        txtObj.transform.SetParent(toastBannerObj.transform, false);
        RectTransform tRt = txtObj.AddComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero;
        tRt.anchorMax = Vector2.one;
        tRt.offsetMin = new Vector2(18f, 0f);
        tRt.offsetMax = new Vector2(-18f, 0f);

        toastText = txtObj.AddComponent<TextMeshProUGUI>();
        toastText.fontSize = 15;
        toastText.fontStyle = FontStyles.Bold;
        toastText.alignment = TextAlignmentOptions.Center;
        toastText.color = Color.white;
        toastText.raycastTarget = false;
        toastText.text = "";

        toastBannerObj.SetActive(false);
    }

    private void LinkExistingModalElements(GameObject root)
    {
        if (root == null) return;

        // Header Buttons
        if (headerBackButton == null)
        {
            var btn = root.transform.Find("WindowPanel/HeaderBar/HeaderBackButton");
            if (btn != null) headerBackButton = btn.GetComponent<Button>();
        }
        if (headerBackButton != null)
        {
            headerBackButton.onClick.RemoveListener(CloseInventory);
            headerBackButton.onClick.AddListener(CloseInventory);
        }

        if (headerCloseButton == null)
        {
            var btn = root.transform.Find("WindowPanel/HeaderBar/CloseButton");
            if (btn != null) headerCloseButton = btn.GetComponent<Button>();
        }
        if (headerCloseButton != null)
        {
            headerCloseButton.onClick.RemoveListener(CloseInventory);
            headerCloseButton.onClick.AddListener(CloseInventory);
        }

        // Weight Section
        if (weightText == null)
        {
            var t = root.transform.Find("WindowPanel/HeaderBar/WeightEncumbranceBar/WeightText");
            if (t != null) weightText = t.GetComponent<TextMeshProUGUI>();
        }
        if (weightBarFill == null)
        {
            var f = root.transform.Find("WindowPanel/HeaderBar/WeightEncumbranceBar/BarTrack/BarFill");
            if (f != null) weightBarFill = f.GetComponent<Image>();
        }
        if (weightStatusBadgeText == null)
        {
            var s = root.transform.Find("WindowPanel/HeaderBar/WeightEncumbranceBar/StatusBadge/BadgeText");
            if (s != null) weightStatusBadgeText = s.GetComponent<TextMeshProUGUI>();
        }
        if (weightStatusBadgeBg == null)
        {
            var s = root.transform.Find("WindowPanel/HeaderBar/WeightEncumbranceBar/StatusBadge");
            if (s != null) weightStatusBadgeBg = s.GetComponent<Image>();
        }

        // Left Pane (Item List)
        if (itemListContent == null)
        {
            var c = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/ItemScrollRect/Viewport/Content");
            if (c == null) c = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/ScrollView/Viewport/Content");
            if (c == null) c = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/Scroll View/Viewport/Content");
            if (c != null) itemListContent = c;
        }
        if (emptyStateObj == null)
        {
            var e = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/EmptyStateNotice");
            if (e == null) e = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/EmptyNotice");
            if (e != null) emptyStateObj = e.gameObject;
        }
        if (itemCountText == null)
        {
            var ct = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/ListHeader/CountText");
            if (ct == null) ct = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/ListHeader");
            if (ct == null) ct = root.transform.Find("WindowPanel/BodySplitPane/LeftPane_ItemList/PaneHeader/CountText");
            if (ct != null) itemCountText = ct.GetComponent<TextMeshProUGUI>();
        }

        // Right Pane (Dossier)
        if (dossierEmptyNotice == null)
        {
            var en = root.transform.Find("WindowPanel/BodySplitPane/RightPane_Dossier/DossierEmptyNotice");
            if (en == null) en = root.transform.Find("WindowPanel/BodySplitPane/RightPane_Dossier/EmptyDossierNotice");
            if (en != null) dossierEmptyNotice = en.gameObject;
        }
        if (dossierContentGroup == null)
        {
            var dc = root.transform.Find("WindowPanel/BodySplitPane/RightPane_Dossier/DossierContentGroup");
            if (dc == null) dc = root.transform.Find("WindowPanel/BodySplitPane/RightPane_Dossier/DossierContent");
            if (dc != null) dossierContentGroup = dc.gameObject;
        }

        Transform dossierRoot = dossierContentGroup != null ? dossierContentGroup.transform : root.transform;

        if (dossierModelImage == null)
        {
            var m = dossierRoot.Find("DossierTopSection/SpecimenIllustration");
            if (m == null) m = dossierRoot.Find("ModelPedestalArea/ModelImage");
            if (m != null) dossierModelImage = m.GetComponent<Image>();
        }
        if (dossierPedestalImage == null)
        {
            var p = dossierRoot.Find("ModelPedestalArea/PedestalGlow");
            if (p != null) dossierPedestalImage = p.GetComponent<Image>();
        }
        if (dossierTitleText == null)
        {
            var tt = dossierRoot.Find("DossierTopSection/DossierTitleGroup");
            if (tt == null) tt = dossierRoot.Find("DossierTitle");
            if (tt != null) dossierTitleText = tt.GetComponent<TextMeshProUGUI>();
        }
        if (dossierWeightStatText == null)
        {
            var ws = dossierRoot.Find("StatsGrid/MetricCard_WEIGHT/Value");
            if (ws == null) ws = dossierRoot.Find("StatsGrid/MetricCard_WEIGHT/ValueText");
            if (ws != null) dossierWeightStatText = ws.GetComponent<TextMeshProUGUI>();
        }
        if (dossierHardnessStatText == null)
        {
            var hs = dossierRoot.Find("StatsGrid/MetricCard_HARDNESS/Value");
            if (hs == null) hs = dossierRoot.Find("StatsGrid/MetricCard_MOHS HARDNESS/ValueText");
            if (hs != null) dossierHardnessStatText = hs.GetComponent<TextMeshProUGUI>();
        }
        if (dossierDurabilityStatText == null)
        {
            var ds = dossierRoot.Find("StatsGrid/MetricCard_DURABILITY/Value");
            if (ds == null) ds = dossierRoot.Find("StatsGrid/MetricCard_KNAP DURABILITY/ValueText");
            if (ds != null) dossierDurabilityStatText = ds.GetComponent<TextMeshProUGUI>();
        }
        if (dossierToughnessStatText == null)
        {
            var ts = dossierRoot.Find("StatsGrid/MetricCard_TOUGHNESS/Value");
            if (ts == null) ts = dossierRoot.Find("StatsGrid/MetricCard_FRACTURE TOUGHNESS/ValueText");
            if (ts != null) dossierToughnessStatText = ts.GetComponent<TextMeshProUGUI>();
        }
        if (dossierDescText == null)
        {
            var dt = dossierRoot.Find("DossierDescription");
            if (dt == null) dt = dossierRoot.Find("DossierDescPanel/Scroll View/Viewport/Content/DescText");
            if (dt != null) dossierDescText = dt.GetComponent<TextMeshProUGUI>();
        }

        // Action Buttons
        Transform actionsParent = dossierRoot.Find("ActionsRow");
        if (actionsParent == null) actionsParent = dossierRoot.Find("ActionButtonBar");
        if (actionsParent == null) actionsParent = dossierRoot;

        for (int i = 0; i < actionsParent.childCount; i++)
        {
            Transform child = actionsParent.GetChild(i);
            string cName = child.name;
            Button b = child.GetComponent<Button>();
            if (b == null) continue;

            if (cName.Contains("INSPECT") || cName.Contains("3D"))
            {
                if (dossierInspect3DButton == null) dossierInspect3DButton = b;
                b.onClick.RemoveListener(OnInspect3DClicked);
                b.onClick.AddListener(OnInspect3DClicked);
            }
            else if (cName.Contains("DROP"))
            {
                if (dossierDropButton == null) dossierDropButton = b;
                b.onClick.RemoveListener(OnDropClicked);
                b.onClick.AddListener(OnDropClicked);
            }
            else if (cName.Contains("EQUIP") || cName.Contains("TAKE") || cName.Contains("HAND"))
            {
                if (dossierEquipButton == null) dossierEquipButton = b;
                b.onClick.RemoveListener(OnEquipClicked);
                b.onClick.AddListener(OnEquipClicked);
            }
            else if (cName.Contains("CODEX") || cName.Contains("BOOK"))
            {
                if (dossierBookButton == null) dossierBookButton = b;
                b.onClick.RemoveListener(OnBookClicked);
                b.onClick.AddListener(OnBookClicked);
            }
        }
    }

    [ContextMenu("Auto Link UI From Hierarchy")]
    public void AutoLinkUIFromHierarchy()
    {
        Canvas hudCanvas = GetComponentInParent<Canvas>();
        if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();
        if (hudCanvas == null) return;

        if (hudButtonObj == null)
        {
            var b = hudCanvas.transform.Find("InventoryHUDButton");
            if (b != null) hudButtonObj = b.gameObject;
        }
        if (hudButtonObj != null)
        {
            if (hudInvButton == null) hudInvButton = hudButtonObj.GetComponent<Button>();
            if (hudInvIconImg == null) hudInvIconImg = hudButtonObj.GetComponent<Image>();
            if (hudWeightBadgeText == null) hudWeightBadgeText = hudButtonObj.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (returnToInvButtonObj == null)
        {
            var r = hudCanvas.transform.Find("ReturnToInventoryButton");
            if (r != null) returnToInvButtonObj = r.gameObject;
        }
        if (returnToInvButtonObj != null)
        {
            if (returnToInvButton == null) returnToInvButton = returnToInvButtonObj.GetComponent<Button>();
            if (returnToInvText == null) returnToInvText = returnToInvButtonObj.GetComponentInChildren<TextMeshProUGUI>();
        }

        if (modalRoot == null)
        {
            var m = hudCanvas.transform.Find("ExpeditionInventoryModal");
            if (m != null) modalRoot = m.gameObject;
        }
        if (modalRoot != null)
        {
            LinkExistingModalElements(modalRoot);
        }

        if (toastBannerObj == null)
        {
            var t = hudCanvas.transform.Find("InventoryToastBanner");
            if (t != null) toastBannerObj = t.gameObject;
        }
        if (toastBannerObj != null)
        {
            if (toastBg == null) toastBg = toastBannerObj.GetComponent<Image>();
            if (toastText == null) toastText = toastBannerObj.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    public void ShowToastNotification(string message, Color highlightColor)
    {
        if (toastBannerObj == null || toastText == null) return;
        toastText.text = message;
        toastBannerObj.SetActive(true);
        toastTimer = 3.2f;
    }

    // =========================================================================
    // 5. INTERACTION & NAVIGATION
    // =========================================================================
    public void ToggleInventory()
    {
        if (isOpen) CloseInventory();
        else OpenInventory();
    }

    public void OpenInventory()
    {
        isOpen = true;

        if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
        if (player != null)
        {
            player.SetControlsLocked(true);
        }

        if (modalRoot != null) modalRoot.SetActive(true);

        RefreshInventoryUI();
    }

    public void CloseInventory()
    {
        isOpen = false;

        if (modalRoot != null) modalRoot.SetActive(false);

        if (player != null && (ArchaeologicalBookUI.Instance == null || !ArchaeologicalBookUI.Instance.IsOpen)
            && (RockInspectionManager.Instance == null || !RockInspectionManager.Instance.IsInspecting))
        {
            player.SetControlsLocked(false);
        }
    }

    private void Update()
    {
        if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();

        // Large "Return to Inventory" button visibility when item in hand
        if (returnToInvButtonObj != null)
        {
            bool hasHeldRock = player != null && player.HeldRockCount > 0;
            bool shouldShowReturnBtn = hasHeldRock && !isOpen && (RockInspectionManager.Instance == null || !RockInspectionManager.Instance.IsInspecting);
            if (returnToInvButtonObj.activeSelf != shouldShowReturnBtn)
            {
                returnToInvButtonObj.SetActive(shouldShowReturnBtn);
            }
        }

        // Keyboard shortcuts
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
            {
                ToggleInventory();
            }
            else if (isOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseInventory();
            }
            else if (!isOpen && Keyboard.current.rKey.wasPressedThisFrame && player != null && player.HeldRockCount > 0)
            {
                OnReturnHeldRockToInventoryClicked();
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleInventory();
        }
        else if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseInventory();
        }
        else if (!isOpen && Input.GetKeyDown(KeyCode.R) && player != null && player.HeldRockCount > 0)
        {
            OnReturnHeldRockToInventoryClicked();
        }
#endif

        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            if (toastTimer <= 0f && toastBannerObj != null)
            {
                toastBannerObj.SetActive(false);
            }
        }
    }

    private void OnInventoryDataChanged()
    {
        UpdateHUDButtonVisuals();
        if (isOpen)
        {
            RefreshInventoryUI();
        }
    }

    private void RefreshInventoryUI()
    {
        if (InventoryManager.Instance == null) return;

        float curWeight = InventoryManager.Instance.CurrentWeight;
        float maxWeight = InventoryManager.Instance.MaxWeight;
        float ratio = InventoryManager.Instance.WeightRatio;

        // 1. Update Weight Bar (Death Stranding Style)
        if (weightBarFill != null)
        {
            weightBarFill.fillAmount = ratio;
            if (ratio > 0.90f) weightBarFill.color = new Color(0.95f, 0.28f, 0.25f);
            else if (ratio > 0.70f) weightBarFill.color = new Color(0.98f, 0.70f, 0.22f);
            else weightBarFill.color = new Color(0.0f, 0.90f, 0.64f);
        }

        if (weightText != null)
        {
            weightText.text = $"<b>CARRY LOAD: {curWeight:F1} / {maxWeight:F1} KG ({(ratio * 100f):F0}%)</b>";
        }

        if (weightStatusBadgeText != null && weightStatusBadgeBg != null)
        {
            if (ratio > 0.95f)
            {
                weightStatusBadgeText.text = "MAX CAPACITY";
                weightStatusBadgeBg.color = new Color(0.60f, 0.15f, 0.15f, 0.95f);
            }
            else if (ratio > 0.70f)
            {
                weightStatusBadgeText.text = "HEAVY LOAD";
                weightStatusBadgeBg.color = new Color(0.60f, 0.38f, 0.12f, 0.95f);
            }
            else
            {
                weightStatusBadgeText.text = "OPTIMAL LOAD";
                weightStatusBadgeBg.color = new Color(0.12f, 0.40f, 0.25f, 0.95f);
            }
        }

        // 2. Clear old item cards
        foreach (var card in activeItemCards)
        {
            if (card != null) Destroy(card);
        }
        activeItemCards.Clear();

        var items = InventoryManager.Instance.Items;
        if (itemCountText != null)
        {
            itemCountText.text = $"COLLECTED LITHIC SPECIMENS ({items.Count})";
        }

        if (items.Count == 0)
        {
            if (emptyStateObj != null) emptyStateObj.SetActive(true);
            selectedRock = null;
            UpdateDossier(null);
            return;
        }

        if (emptyStateObj != null) emptyStateObj.SetActive(false);

        if (selectedRock == null || !items.Contains(selectedRock))
        {
            selectedRock = items[0];
        }

        // Build item cards with in-game 3D model renders
        for (int i = 0; i < items.Count; i++)
        {
            RockItem item = items[i];
            if (item == null) continue;

            GameObject card = BuildItemCard(item, item == selectedRock);
            activeItemCards.Add(card);
        }

        UpdateDossier(selectedRock);
    }

    private GameObject BuildItemCard(RockItem rock, bool isSelected)
    {
        GameObject cardObj = new GameObject($"Card_{rock.name}");
        cardObj.transform.SetParent(itemListContent, false);

        RectTransform cRt = cardObj.AddComponent<RectTransform>();
        cRt.sizeDelta = new Vector2(0f, 84f);

        Image cardBg = cardObj.AddComponent<Image>();
        if (cardFrameSprite != null)
        {
            cardBg.sprite = cardFrameSprite;
            cardBg.type = Image.Type.Sliced;
            cardBg.color = isSelected ? new Color(1.2f, 1.15f, 1.0f) : Color.white;
        }
        else
        {
            cardBg.color = isSelected ? new Color(0.18f, 0.26f, 0.38f, 0.98f) : new Color(0.08f, 0.11f, 0.16f, 0.90f);
        }

        Outline outline = cardObj.AddComponent<Outline>();
        outline.effectColor = isSelected ? new Color(0.96f, 0.82f, 0.50f, 0.95f) : new Color(0.20f, 0.26f, 0.36f, 0.6f);
        outline.effectDistance = isSelected ? new Vector2(2f, -2f) : new Vector2(1f, -1f);

        Button btn = cardObj.AddComponent<Button>();
        RockItem currentRock = rock;
        btn.onClick.AddListener(() => OnItemCardSelected(currentRock));

        // Left Colored Accent Strip (Rarity / Hardness Tier)
        GameObject stripObj = new GameObject("TierAccentStrip");
        stripObj.transform.SetParent(cardObj.transform, false);
        RectTransform stRt = stripObj.AddComponent<RectTransform>();
        stRt.anchorMin = new Vector2(0f, 0f);
        stRt.anchorMax = new Vector2(0f, 1f);
        stRt.pivot = new Vector2(0f, 0.5f);
        stRt.anchoredPosition = Vector2.zero;
        stRt.sizeDelta = new Vector2(6f, 0f);

        Image stripImg = stripObj.AddComponent<Image>();
        stripImg.color = GetRockTierColor(rock);
        stripImg.raycastTarget = false;

        // IN-GAME 3D MODEL PREVIEW THUMBNAIL
        GameObject thumbObj = new GameObject("Thumbnail");
        thumbObj.transform.SetParent(cardObj.transform, false);
        RectTransform thRt = thumbObj.AddComponent<RectTransform>();
        thRt.anchorMin = new Vector2(0f, 0.5f);
        thRt.anchorMax = new Vector2(0f, 0.5f);
        thRt.pivot = new Vector2(0f, 0.5f);
        thRt.anchoredPosition = new Vector2(16f, 0f);
        thRt.sizeDelta = new Vector2(70f, 70f);

        Image thumbImg = thumbObj.AddComponent<Image>();
        thumbImg.sprite = GetRockModelSprite(rock);
        thumbImg.preserveAspect = true;
        thumbImg.color = Color.white;
        thumbImg.raycastTarget = false;

        // Rock Name and Classification Subtitle
        GameObject textGroup = new GameObject("TextGroup");
        textGroup.transform.SetParent(cardObj.transform, false);
        RectTransform tgRt = textGroup.AddComponent<RectTransform>();
        tgRt.anchorMin = new Vector2(0f, 0f);
        tgRt.anchorMax = new Vector2(1f, 1f);
        tgRt.offsetMin = new Vector2(98f, 0f);
        tgRt.offsetMax = new Vector2(-130f, 0f);

        string rName = rock.rockData != null ? rock.rockData.rockName : rock.name;
        string rType = rock.rockData != null ? rock.rockData.GetTierCategory() : "Lithic Core";

        TextMeshProUGUI nameTxt = textGroup.AddComponent<TextMeshProUGUI>();
        nameTxt.fontSize = 15;
        nameTxt.fontStyle = isSelected ? FontStyles.Bold : FontStyles.Normal;
        nameTxt.color = isSelected ? new Color(0.96f, 0.82f, 0.50f) : Color.white;
        nameTxt.alignment = TextAlignmentOptions.MidlineLeft;
        nameTxt.raycastTarget = false;
        nameTxt.text = $"<b>{rName}</b>\n<size=11><color=#8EA0B5>{rType}</color></size>";

        // Weight Pill Badge on Right
        GameObject weightPill = new GameObject("WeightPill");
        weightPill.transform.SetParent(cardObj.transform, false);
        RectTransform wpRt = weightPill.AddComponent<RectTransform>();
        wpRt.anchorMin = new Vector2(1f, 0.5f);
        wpRt.anchorMax = new Vector2(1f, 0.5f);
        wpRt.pivot = new Vector2(1f, 0.5f);
        wpRt.anchoredPosition = new Vector2(-14f, 0f);
        wpRt.sizeDelta = new Vector2(100f, 34f);

        Image wpBg = weightPill.AddComponent<Image>();
        wpBg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        wpBg.raycastTarget = false;

        Outline wpO = weightPill.AddComponent<Outline>();
        wpO.effectColor = new Color(0.35f, 0.45f, 0.60f, 0.6f);
        wpO.effectDistance = new Vector2(1f, -1f);

        GameObject wpTextObj = new GameObject("PillText");
        wpTextObj.transform.SetParent(weightPill.transform, false);
        RectTransform wptRt = wpTextObj.AddComponent<RectTransform>();
        wptRt.anchorMin = Vector2.zero;
        wptRt.anchorMax = Vector2.one;
        wptRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI wpTxt = wpTextObj.AddComponent<TextMeshProUGUI>();
        wpTxt.fontSize = 13;
        wpTxt.fontStyle = FontStyles.Bold;
        wpTxt.alignment = TextAlignmentOptions.Center;
        wpTxt.color = new Color(0.96f, 0.82f, 0.50f);
        wpTxt.text = $"{rock.GetWeight():F1} KG";
        wpTxt.raycastTarget = false;

        return cardObj;
    }

    private Color GetRockTierColor(RockItem rock)
    {
        if (rock == null) return Color.gray;
        string n = rock.name.ToLower();
        if (n.Contains("silex") || n.Contains("flint")) return new Color(0.96f, 0.78f, 0.32f);
        if (n.Contains("grès") || n.Contains("gres") || n.Contains("sandstone")) return new Color(0.88f, 0.48f, 0.32f);
        return new Color(0.55f, 0.75f, 0.92f);
    }

    private void OnItemCardSelected(RockItem rock)
    {
        selectedRock = rock;
        RefreshInventoryUI();
    }

    private void UpdateDossier(RockItem rock)
    {
        if (rock == null)
        {
            if (dossierEmptyNotice != null) dossierEmptyNotice.SetActive(true);
            if (dossierContentGroup != null) dossierContentGroup.SetActive(false);
            return;
        }

        if (dossierEmptyNotice != null) dossierEmptyNotice.SetActive(false);
        if (dossierContentGroup != null) dossierContentGroup.SetActive(true);

        RockData data = rock.rockData;
        string rName = data != null ? data.rockName : rock.name;
        float hardness = data != null ? data.hardness : rock.strength;
        float toughness = data != null ? data.toughness : 0.6f;
        int curHits = rock.HitsRemaining;
        int maxHits = rock.MaxHits;
        float mass = rock.GetWeight();
        string tier = data != null ? data.GetTierCategory() : "Lithic Core";

        if (dossierModelImage != null)
        {
            dossierModelImage.sprite = GetRockModelSprite(rock);
            dossierModelImage.enabled = dossierModelImage.sprite != null;
            dossierModelImage.color = Color.white;
        }

        if (dossierTitleText != null)
        {
            dossierTitleText.text =
                $"<color=#F6C878><size=24><b>[ {rName.ToUpper()} ]</b></size></color>\n" +
                $"<size=12><color=#8EA0B5>ARCHAEOLOGICAL CLASSIFICATION: {tier.ToUpper()} • JBEL IGHOUD SITE</color></size>";
        }

        if (dossierWeightStatText != null) dossierWeightStatText.text = $"{mass:F1} KG";
        if (dossierHardnessStatText != null) dossierHardnessStatText.text = $"{hardness:F1} / 20.0";
        if (dossierDurabilityStatText != null) dossierDurabilityStatText.text = $"{curHits} / {maxHits} Strikes";
        if (dossierToughnessStatText != null) dossierToughnessStatText.text = $"{toughness:F2}";

        if (dossierDescText != null)
        {
            float maxCap = InventoryManager.Instance != null ? InventoryManager.Instance.MaxWeight : 200f;
            float weightPercent = (mass / maxCap) * 100f;

            string notes = data != null && !string.IsNullOrEmpty(data.historicalNotes)
                ? data.historicalNotes
                : "Authentic paleolithic mineral nodule excavated at Jbel Ighoud, Morocco.";

            string desc = data != null && !string.IsNullOrEmpty(data.description)
                ? data.description
                : "Rock specimen suitable for knapping or percussion.";

            dossierDescText.text =
                $"<b>ARCHAEOLOGICAL DISCOVERY & CONTEXT:</b>\n{notes}\n\n" +
                $"<b>LITHIC CHARACTERISTICS:</b>\n{desc}\n\n" +
                $"<color=#64748B><size=12>• Weight Impact: Occupies {weightPercent:F1}% of your 200 kg expedition capacity.\n" +
                $"• Drop back into the cave environment at any time to recover encumbrance allowance.</size></color>";
        }
    }

    // =========================================================================
    // 6. CONTEXTUAL ACTION HANDLERS
    // =========================================================================
    private void OnInspect3DClicked()
    {
        if (selectedRock == null) return;
        RockItem rock = selectedRock;
        CloseInventory();

        // FULLSCREEN 3D MODEL INSPECTION WITHOUT CLUTTER
        if (RockInspectionManager.Instance != null)
        {
            RockInspectionManager.Instance.OpenFullScreenInspection(rock, returnToInv: true);
        }
    }

    private void OnDropClicked()
    {
        if (selectedRock == null || InventoryManager.Instance == null) return;
        RockItem rockToDrop = selectedRock;
        InventoryManager.Instance.DropItem(rockToDrop);
    }

    private void OnEquipClicked()
    {
        if (selectedRock == null || InventoryManager.Instance == null) return;
        RockItem rockToEquip = selectedRock;
        InventoryManager.Instance.EquipToHand(rockToEquip);
    }

    private void OnBookClicked()
    {
        if (selectedRock == null || selectedRock.rockData == null) return;
        RockData data = selectedRock.rockData;
        CloseInventory();

        if (ArchaeologicalBookUI.Instance != null)
        {
            ArchaeologicalBookUI.Instance.OpenBook(data);
        }
    }
}
