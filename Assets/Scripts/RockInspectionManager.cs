using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.UI;

using UnityEngine.InputSystem;

using TMPro;

using JbelIghoud.Lithic;



#if ENABLE_INPUT_SYSTEM

using UnityEngine.InputSystem.EnhancedTouch;

using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

#endif



/// <summary>

/// Resident Evil Style 3D Item Inspection System.

/// When inspecting a rock, gameplay dims and the isolated 3D rock is presented in the center

/// rendered via a studio camera to a high-res inspection viewport.

/// The player can smoothly rotate it in 3D, inspect conchoidal facets, view stats,

/// click the archaeological book icon on the left to read field guide notes, or pick it up.

/// </summary>

public class RockInspectionManager : MonoBehaviour

{

    private static RockInspectionManager _instance;

    public static RockInspectionManager Instance

    {

        get

        {

            if (_instance == null)

            {

                _instance = FindAnyObjectByType<RockInspectionManager>();

                if (_instance == null)

                {

                    Canvas c = FindAnyObjectByType<Canvas>();

                    GameObject go = c != null ? c.gameObject : new GameObject("RockInspectionManager");

                    _instance = go.AddComponent<RockInspectionManager>();

                }

            }

            if (_instance != null) _instance.EnsureInitialized();

            return _instance;

        }

    }



    private bool isInitialized = false;

    private bool isInspecting = false;

    public bool IsInspecting => isInspecting;



    private TopDownPlayerController player;

    private RockItem currentRockItem;



    // 3D Studio Stage

    private GameObject studioRoot;

    private RenderTexture studioRT;

    private Camera studioCam;

    private Light studioKeyLight;

    private Light studioRimLight;

    private GameObject turntableObj;

    private GameObject rockModelInstance;



    // UI Elements

    private GameObject inspectionCanvasObj;

    private RawImage rockViewportRawImage;

    private TextMeshProUGUI statsText;

    private Button takeButton;

    private Button returnButton;
    private GameObject statsCardObj;
    private GameObject bottomBarObj;
    private GameObject fullScreenBackBtnObj;
    private bool returnToInventoryOnClose = false;



    // Turntable and framing settings

    private float targetFramedSize = 0.55f;

    private float currentCamDist = 1.4f;

    private Vector2 angularVelocity = Vector2.zero;

    private float timeSinceLastDrag = 0f;

    private float rotationSensitivity = 0.35f;

    private float rotationDamping = 4.0f;

    private float idleTurntableSpeed = 0.25f;

    private Vector2 lastPointerPos = Vector2.zero;

    private bool isDragging = false;



    public void EnsureInitialized()

    {

        if (isInitialized) return;

        isInitialized = true;

        _instance = this;



#if ENABLE_INPUT_SYSTEM

        EnhancedTouchSupport.Enable();

#endif

        BuildStudioStage();

        BuildUI();

        CloseInspectionInternal();

    }



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



    private void Start()

    {

        player = FindAnyObjectByType<TopDownPlayerController>();

    }



    private void BuildStudioStage()

    {

        if (studioRoot != null) return;



        // Isolated studio root in the deep void (-1000 Y) so it never collides with environment

        studioRoot = new GameObject("Inspection3DStudio");

        studioRoot.transform.position = new Vector3(0f, -1000f, 0f);



        // Studio RenderTexture

        studioRT = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);

        studioRT.name = "RT_RockInspectionStudio";



        // Studio Camera

        GameObject camObj = new GameObject("StudioCamera");

        camObj.transform.SetParent(studioRoot.transform, false);

        camObj.transform.localPosition = new Vector3(0f, 0f, -1.4f);

        studioCam = camObj.AddComponent<Camera>();

        studioCam.clearFlags = CameraClearFlags.SolidColor;

        studioCam.backgroundColor = new Color(0f, 0f, 0f, 0f); // Pure transparent

        studioCam.targetTexture = studioRT;

        studioCam.fieldOfView = 36f;

        studioCam.nearClipPlane = 0.1f;

        studioCam.farClipPlane = 20f;



        // Key Light (Warm golden sunlight highlighting sharp conchoidal ridges)

        GameObject keyObj = new GameObject("StudioKeyLight");

        keyObj.transform.SetParent(studioRoot.transform, false);

        keyObj.transform.localPosition = new Vector3(1.6f, 2.2f, -1.2f);

        studioKeyLight = keyObj.AddComponent<Light>();

        studioKeyLight.type = LightType.Directional;

        studioKeyLight.color = new Color(1.0f, 0.95f, 0.82f);

        studioKeyLight.intensity = 1.4f;

        keyObj.transform.LookAt(studioRoot.transform.position);



        // Rim Light (Cool fill highlighting silhouette facets)

        GameObject rimObj = new GameObject("StudioRimLight");

        rimObj.transform.SetParent(studioRoot.transform, false);

        rimObj.transform.localPosition = new Vector3(-2.2f, -0.6f, 1.8f);

        studioRimLight = rimObj.AddComponent<Light>();

        studioRimLight.type = LightType.Directional;

        studioRimLight.color = new Color(0.65f, 0.82f, 1.0f);

        studioRimLight.intensity = 0.9f;

        rimObj.transform.LookAt(studioRoot.transform.position);



        // Turntable Root

        turntableObj = new GameObject("StudioTurntable");

        turntableObj.transform.SetParent(studioRoot.transform, false);

        turntableObj.transform.localPosition = Vector3.zero;



        // Rock Mesh Holder

        rockModelInstance = new GameObject("InspectedRockMesh");

        rockModelInstance.transform.SetParent(turntableObj.transform, false);

        rockModelInstance.AddComponent<MeshFilter>();

        rockModelInstance.AddComponent<MeshRenderer>();

    }



    private void BuildUI()

    {

        Canvas hudCanvas = GetComponentInParent<Canvas>();

        if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();



        if (inspectionCanvasObj == null && hudCanvas != null)
        {
            Transform existing = hudCanvas.transform.Find("RockInspectionView");
            if (existing != null) inspectionCanvasObj = existing.gameObject;
        }

        if (inspectionCanvasObj != null)
        {
            inspectionCanvasObj.SetActive(false);
            return;
        }



        inspectionCanvasObj = new GameObject("RockInspectionView");

        if (hudCanvas != null)

        {

            inspectionCanvasObj.transform.SetParent(hudCanvas.transform, false);

        }



        RectTransform rootRt = inspectionCanvasObj.AddComponent<RectTransform>();

        rootRt.anchorMin = Vector2.zero;

        rootRt.anchorMax = Vector2.one;

        rootRt.sizeDelta = Vector2.zero;



        // Darkened cinematic vignette backdrop

        Image backdrop = inspectionCanvasObj.AddComponent<Image>();

        backdrop.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);



        // ==========================================

        // CENTER: 3D Rock Viewport (RawImage)

        // ==========================================

        GameObject viewportObj = new GameObject("Rock3DViewport");

        viewportObj.transform.SetParent(inspectionCanvasObj.transform, false);

        RectTransform vpRt = viewportObj.AddComponent<RectTransform>();

        vpRt.anchorMin = new Vector2(0.5f, 0.5f);

        vpRt.anchorMax = new Vector2(0.5f, 0.5f);

        vpRt.sizeDelta = new Vector2(740f, 740f);

        vpRt.anchoredPosition = new Vector2(90f, 0f); // Framed nicely next to left info card



        rockViewportRawImage = viewportObj.AddComponent<RawImage>();

        rockViewportRawImage.texture = studioRT;

        rockViewportRawImage.color = Color.white;



        // ==========================================

        // LEFT SIDE: Specimen Info Card (Razor-Sharp TextMeshPro)

        // ==========================================

        GameObject statsObj = new GameObject("SpecimenInfoCard");
        statsCardObj = statsObj;

        statsObj.transform.SetParent(inspectionCanvasObj.transform, false);

        RectTransform statsRt = statsObj.AddComponent<RectTransform>();

        statsRt.anchorMin = new Vector2(0.035f, 0.5f);

        statsRt.anchorMax = new Vector2(0.035f, 0.5f);

        statsRt.pivot = new Vector2(0f, 0.5f);

        statsRt.sizeDelta = new Vector2(400f, 580f);

        statsRt.anchoredPosition = Vector2.zero;



        Image statsBg = statsObj.AddComponent<Image>();

        statsBg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);



        Outline outline = statsObj.AddComponent<Outline>();

        outline.effectColor = new Color(0.28f, 0.36f, 0.48f, 0.45f);

        outline.effectDistance = new Vector2(1.5f, -1.5f);



        GameObject statsTextObj = new GameObject("SpecimenInfoContent");

        statsTextObj.transform.SetParent(statsObj.transform, false);

        RectTransform stRt = statsTextObj.AddComponent<RectTransform>();

        stRt.anchorMin = Vector2.zero;

        stRt.anchorMax = Vector2.one;

        stRt.offsetMin = new Vector2(24f, 22f);

        stRt.offsetMax = new Vector2(-24f, -22f);



        statsText = statsTextObj.AddComponent<TextMeshProUGUI>();

        statsText.fontSize = 15;

        statsText.enableWordWrapping = true;

        statsText.alignment = TextAlignmentOptions.TopLeft;

        statsText.color = Color.white;

        statsText.lineSpacing = 12f;



        // ==========================================

        // BOTTOM BAR: Take & Put Back Buttons

        // ==========================================

        GameObject bottomBar = new GameObject("InspectionBottomBar");
        bottomBarObj = bottomBar;

        bottomBar.transform.SetParent(inspectionCanvasObj.transform, false);

        RectTransform bbRt = bottomBar.AddComponent<RectTransform>();

        bbRt.anchorMin = new Vector2(0.38f, 0.04f);

        bbRt.anchorMax = new Vector2(0.82f, 0.13f);

        bbRt.sizeDelta = Vector2.zero;



        // Take Button

        GameObject takeObj = new GameObject("TakeButton");

        takeObj.transform.SetParent(bottomBar.transform, false);

        RectTransform takeRt = takeObj.AddComponent<RectTransform>();

        takeRt.anchorMin = new Vector2(0.05f, 0.08f);

        takeRt.anchorMax = new Vector2(0.48f, 0.92f);

        takeRt.sizeDelta = Vector2.zero;



        Image takeImg = takeObj.AddComponent<Image>();

        takeImg.color = new Color(0.18f, 0.45f, 0.28f, 0.95f); // Natural jade

        takeButton = takeObj.AddComponent<Button>();

        takeButton.onClick.AddListener(OnTakeButtonClicked);



        GameObject takeTextObj = new GameObject("TakeText");

        takeTextObj.transform.SetParent(takeObj.transform, false);

        RectTransform ttRt = takeTextObj.AddComponent<RectTransform>();

        ttRt.anchorMin = Vector2.zero;

        ttRt.anchorMax = Vector2.one;

        ttRt.sizeDelta = Vector2.zero;



        TextMeshProUGUI takeTxt = takeTextObj.AddComponent<TextMeshProUGUI>();

        takeTxt.fontSize = 16;

        takeTxt.fontStyle = FontStyles.Bold;

        takeTxt.alignment = TextAlignmentOptions.Center;

        takeTxt.color = Color.white;

        takeTxt.text = "TAKE / PICK UP";



        // Put Back Button

        GameObject returnObj = new GameObject("ReturnButton");

        returnObj.transform.SetParent(bottomBar.transform, false);

        RectTransform retRt = returnObj.AddComponent<RectTransform>();

        retRt.anchorMin = new Vector2(0.52f, 0.08f);

        retRt.anchorMax = new Vector2(0.95f, 0.92f);

        retRt.sizeDelta = Vector2.zero;



        Image retImg = returnObj.AddComponent<Image>();

        retImg.color = new Color(0.35f, 0.20f, 0.18f, 0.95f);

        returnButton = returnObj.AddComponent<Button>();

        returnButton.onClick.AddListener(OnReturnButtonClicked);



        GameObject retTextObj = new GameObject("ReturnText");

        retTextObj.transform.SetParent(returnObj.transform, false);

        RectTransform rtRt = retTextObj.AddComponent<RectTransform>();

        rtRt.anchorMin = Vector2.zero;

        rtRt.anchorMax = Vector2.one;

        rtRt.sizeDelta = Vector2.zero;



        TextMeshProUGUI retTxt = retTextObj.AddComponent<TextMeshProUGUI>();

        retTxt.fontSize = 16;

        retTxt.fontStyle = FontStyles.Bold;

        retTxt.alignment = TextAlignmentOptions.Center;

        retTxt.color = Color.white;

        retTxt.text = "PUT BACK";

        // ==========================================
        // TOP-LEFT: Fullscreen Back to Satchel Button
        // ==========================================
        fullScreenBackBtnObj = new GameObject("FullScreenBackButton");
        fullScreenBackBtnObj.transform.SetParent(inspectionCanvasObj.transform, false);
        RectTransform fsbRt = fullScreenBackBtnObj.AddComponent<RectTransform>();
        fsbRt.anchorMin = new Vector2(0f, 1f);
        fsbRt.anchorMax = new Vector2(0f, 1f);
        fsbRt.pivot = new Vector2(0f, 1f);
        fsbRt.anchoredPosition = new Vector2(40f, -40f);
        fsbRt.sizeDelta = new Vector2(240f, 56f);

        Image fsbBg = fullScreenBackBtnObj.AddComponent<Image>();
        fsbBg.color = new Color(0.08f, 0.10f, 0.14f, 0.94f);

        Outline fsbOutline = fullScreenBackBtnObj.AddComponent<Outline>();
        fsbOutline.effectColor = new Color(0.82f, 0.65f, 0.28f, 0.85f);
        fsbOutline.effectDistance = new Vector2(2f, -2f);

        Button fsbButton = fullScreenBackBtnObj.AddComponent<Button>();
        fsbButton.onClick.AddListener(CloseInspection);

        GameObject fsbTextObj = new GameObject("Text");
        fsbTextObj.transform.SetParent(fullScreenBackBtnObj.transform, false);
        RectTransform fsbtRt = fsbTextObj.AddComponent<RectTransform>();
        fsbtRt.anchorMin = Vector2.zero;
        fsbtRt.anchorMax = Vector2.one;
        fsbtRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI fsbText = fsbTextObj.AddComponent<TextMeshProUGUI>();
        fsbText.fontSize = 15;
        fsbText.fontStyle = FontStyles.Bold;
        fsbText.alignment = TextAlignmentOptions.Center;
        fsbText.color = new Color(0.96f, 0.88f, 0.68f);
        fsbText.text = "< BACK TO SATCHEL";

        fullScreenBackBtnObj.SetActive(false);


    }



    public void OpenFullScreenInspection(RockItem rockItem, bool returnToInv = true)
    {
        if (rockItem == null) return;

        returnToInventoryOnClose = returnToInv;
        currentRockItem = rockItem;
        isInspecting = true;
        currentCamDist = 1.35f;
        angularVelocity = Vector2.zero;
        timeSinceLastDrag = 0f;

        if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
        if (player != null) player.SetControlsLocked(true);

        SetupInspectedModel(rockItem);

        if (statsCardObj != null) statsCardObj.SetActive(false);
        if (bottomBarObj != null) bottomBarObj.SetActive(false);
        if (fullScreenBackBtnObj != null) fullScreenBackBtnObj.SetActive(true);

        if (rockViewportRawImage != null)
        {
            RectTransform vpRt = rockViewportRawImage.rectTransform;
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = Vector2.zero;
            vpRt.anchoredPosition = Vector2.zero;
            vpRt.sizeDelta = Vector2.zero;
        }

        if (inspectionCanvasObj != null)
        {
            inspectionCanvasObj.transform.SetAsLastSibling();
            var bg = inspectionCanvasObj.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.04f, 0.05f, 0.08f, 1.0f);
            inspectionCanvasObj.SetActive(true);
        }
        if (studioRoot != null) studioRoot.SetActive(true);
        if (studioCam != null) studioCam.enabled = true;
    }

    public void OpenInspection(RockItem rockItem)
    {
        if (rockItem == null) return;

        returnToInventoryOnClose = false;
        currentRockItem = rockItem;
        isInspecting = true;
        currentCamDist = 1.4f;
        angularVelocity = Vector2.zero;
        timeSinceLastDrag = 0f;

        if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
        if (player != null) player.SetControlsLocked(true);

        SetupInspectedModel(rockItem);
        PopulateUI(rockItem);

        if (statsCardObj != null) statsCardObj.SetActive(true);
        if (bottomBarObj != null) bottomBarObj.SetActive(true);
        if (fullScreenBackBtnObj != null) fullScreenBackBtnObj.SetActive(false);

        if (rockViewportRawImage != null)
        {
            RectTransform vpRt = rockViewportRawImage.rectTransform;
            vpRt.anchorMin = new Vector2(0.5f, 0.5f);
            vpRt.anchorMax = new Vector2(0.5f, 0.5f);
            vpRt.sizeDelta = new Vector2(740f, 740f);
            vpRt.anchoredPosition = new Vector2(90f, 0f);
        }

        if (inspectionCanvasObj != null)
        {
            inspectionCanvasObj.transform.SetAsLastSibling();
            var bg = inspectionCanvasObj.GetComponent<Image>();
            if (bg != null) bg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);
            inspectionCanvasObj.SetActive(true);
        }
        if (studioRoot != null) studioRoot.SetActive(true);
        if (studioCam != null) studioCam.enabled = true;
    }



    private void SetupInspectedModel(RockItem rockItem)

    {

        if (rockModelInstance == null) return;



        MeshFilter mf = rockModelInstance.GetComponent<MeshFilter>();

        MeshRenderer mr = rockModelInstance.GetComponent<MeshRenderer>();



        Mesh sourceMesh = null;

        if (rockItem.rockData != null && rockItem.rockData.rockMesh != null)

        {

            sourceMesh = rockItem.rockData.rockMesh;

        }

        else

        {

            MeshFilter srcMf = rockItem.GetComponent<MeshFilter>();

            if (srcMf != null) sourceMesh = srcMf.sharedMesh;

        }



        mf.sharedMesh = sourceMesh;



        // Apply authentic material / renderer

        MeshRenderer srcMr = rockItem.GetComponent<MeshRenderer>();

        if (srcMr != null)

        {

            mr.sharedMaterial = srcMr.sharedMaterial;

        }



        // Calculate auto-framing scale so the rock sits beautifully in view

        float modelBoundsSize = sourceMesh != null ? sourceMesh.bounds.size.magnitude : 0.08f;

        float frameScale = targetFramedSize / Mathf.Max(0.01f, modelBoundsSize);

        rockModelInstance.transform.localScale = Vector3.one * frameScale;



        // Center mesh bounds offset

        if (sourceMesh != null)

        {

            rockModelInstance.transform.localPosition = -sourceMesh.bounds.center * frameScale;

        }

        else

        {

            rockModelInstance.transform.localPosition = Vector3.zero;

        }



        // Reset turntable rotation

        if (turntableObj != null)

        {

            turntableObj.transform.localRotation = Quaternion.identity;

        }

    }



    private void PopulateUI(RockItem rockItem)

    {

        if (rockItem == null) return;



        RockData data = rockItem.rockData;

        string rName = data != null ? data.rockName : rockItem.name;

        float hardness = data != null ? data.hardness : rockItem.strength;

        float toughness = data != null ? data.toughness : 0.6f;

        int curHits = rockItem.HitsRemaining;

        int maxHits = rockItem.MaxHits;

        float mass = rockItem != null ? rockItem.GetWeight() : (data != null ? data.mass : 1.2f);



        string tier = data != null ? data.GetTierCategory() : "Lithic Core";

        string tagDesc = data != null ? data.tags.ToString().Replace(", ", " • ") : "Hammerstone";



        string discoveryNarrative = "";

        RockType rType = data != null ? data.rockType : RockType.Silex;



        if (rType == RockType.Silex || rName.ToLower().Contains("silex") || rName.ToLower().Contains("flint"))

        {

            discoveryNarrative =

                "<b>ARCHAEOLOGICAL DISCOVERY:</b>\n" +

                "Central to the landmark <i>Homo sapiens</i> (~315,000 BP) lithic industry at Jbel Ighoud. Deliberately knapped Levallois cores struck to produce razor-sharp triangular points and spear blades.";

        }

        else if (rType == RockType.Gres || rName.ToLower().Contains("grès") || rName.ToLower().Contains("sandstone"))

        {

            discoveryNarrative =

                "<b>ARCHAEOLOGICAL DISCOVERY:</b>\n" +

                "Heavy quartzitic hammerstone (<i>percuteur</i>) with visible impact pitting. Used to strike sharp flint flakes and shatter bone for marrow.";

        }

        else

        {

            discoveryNarrative =

                "<b>ARCHAEOLOGICAL DISCOVERY:</b>\n" +

                "Karstic cave bedrock nodule. Friable chalk stone used for hearth linings, shelter wall carving, and white mineral pigments.";

        }



        if (statsText != null)

        {

            statsText.text =

                $"<color=#F6C878><b><size=22>[ {rName.ToUpper()} ]</size></b></color>\n" +

                $"<color=#9EAFC2><size=12>MIDDLE PALEOLITHIC • ~300,000 BP\nJBEL IGHOUD SITE, MOROCCO</size></color>\n\n" +

                $"<color=#384354>────────────────────────</color>\n\n" +

                $"<b>SPECIMEN ANALYSIS:</b>\n\n" +

                $"• <b>Mohs Hardness:</b> <color=#F6C878>{hardness:F1}</color> / 20.0\n" +

                $"• <b>Classification:</b> {tier}\n" +

                $"• <b>Toughness:</b> {toughness:F2}\n" +

                $"• <b>Durability:</b> {curHits} / {maxHits} Strikes\n" +

                $"• <b>Specimen Mass:</b> <color=#F6C878>{mass:F1} kg</color>\n" +

                $"• <b>Satchel Load:</b> {(InventoryManager.Instance != null ? InventoryManager.Instance.CurrentWeight : 0f):F1} / {(InventoryManager.Instance != null ? InventoryManager.Instance.MaxWeight : 200f):F1} kg\n" +

                $"• <b>Prehistoric Role:</b> <color=#89D99D>{tagDesc}</color>\n\n" +

                $"<color=#384354>────────────────────────</color>\n\n" +

                $"{discoveryNarrative}\n\n" +

                $"<color=#64748B><size=11>• Drag with touch / mouse to rotate in 3D\n• Scroll wheel to zoom</size></color>";

        }



        if (takeButton != null)

        {

            TextMeshProUGUI takeTxt = takeButton.GetComponentInChildren<TextMeshProUGUI>();

            Image takeImg = takeButton.GetComponent<Image>();



            bool canAdd = InventoryManager.Instance == null || InventoryManager.Instance.CanAddItem(mass);

            takeButton.interactable = canAdd;



            if (takeTxt != null)

            {

                takeTxt.text = canAdd ? $"STORE IN SATCHEL (+{mass:F1} kg)" : $"SATCHEL FULL (>{(InventoryManager.Instance != null ? InventoryManager.Instance.MaxWeight : 200f):F0} kg)";

            }

            if (takeImg != null)

            {

                takeImg.color = canAdd ? new Color(0.18f, 0.45f, 0.28f, 0.95f) : new Color(0.55f, 0.20f, 0.20f, 0.85f);

            }

        }

    }



    private void Update()

    {

        if (!isInspecting) return;



        // Handle Touch and Mouse 3D rotation

        HandleRotationInput();



        // Keyboard shortcuts

#if ENABLE_INPUT_SYSTEM

        if (Keyboard.current != null)

        {

            if (Keyboard.current.escapeKey.wasPressedThisFrame)

            {

                OnReturnButtonClicked();

            }

            else if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)

            {

                OnTakeButtonClicked();

            }

        }

#else

        if (Input.GetKeyDown(KeyCode.Escape))

        {

            OnReturnButtonClicked();

        }

        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))

        {

            OnTakeButtonClicked();

        }

#endif

    }



    private void HandleRotationInput()

    {

        if (turntableObj == null) return;



        // If Archaeological Book is open, book absorbs input

        if (ArchaeologicalBookUI.Instance != null && ArchaeologicalBookUI.Instance.IsOpen) return;



        bool hasInput = false;

        Vector2 pointerDelta = Vector2.zero;



#if ENABLE_INPUT_SYSTEM

        var touches = Touch.activeTouches;

        if (touches.Count > 0)

        {

            var touch = touches[0];

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)

            {

                lastPointerPos = touch.screenPosition;

                isDragging = true;

                angularVelocity = Vector2.zero;

            }

            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Moved && isDragging)

            {

                pointerDelta = touch.screenPosition - lastPointerPos;

                lastPointerPos = touch.screenPosition;

                hasInput = true;

            }

            else if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended || touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)

            {

                isDragging = false;

            }

        }

        else if (Mouse.current != null)

        {

            if (Mouse.current.leftButton.wasPressedThisFrame)

            {

                lastPointerPos = Mouse.current.position.ReadValue();

                isDragging = true;

                angularVelocity = Vector2.zero;

            }

            else if (Mouse.current.leftButton.isPressed && isDragging)

            {

                Vector2 currentMouse = Mouse.current.position.ReadValue();

                pointerDelta = currentMouse - lastPointerPos;

                lastPointerPos = currentMouse;

                hasInput = true;

            }

            else if (Mouse.current.leftButton.wasReleasedThisFrame)

            {

                isDragging = false;

            }



            // Mouse wheel zoom

            float scroll = Mouse.current.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) > 0.01f && studioCam != null)

            {

                currentCamDist = Mathf.Clamp(currentCamDist - scroll * 0.002f, 0.85f, 2.2f);

                studioCam.transform.localPosition = new Vector3(0f, 0f, -currentCamDist);

            }

        }

#else

        if (Input.GetMouseButtonDown(0))

        {

            lastPointerPos = Input.mousePosition;

            isDragging = true;

            angularVelocity = Vector2.zero;

        }

        else if (Input.GetMouseButton(0) && isDragging)

        {

            Vector2 cur = Input.mousePosition;

            pointerDelta = cur - lastPointerPos;

            lastPointerPos = cur;

            hasInput = true;

        }

        else if (Input.GetMouseButtonUp(0))

        {

            isDragging = false;

        }

#endif



        if (hasInput)

        {

            timeSinceLastDrag = 0f;

            angularVelocity = new Vector2(-pointerDelta.x * rotationSensitivity, pointerDelta.y * rotationSensitivity);

        }

        else

        {

            timeSinceLastDrag += Time.deltaTime;

            // Smoothly damp angular velocity

            angularVelocity = Vector2.Lerp(angularVelocity, Vector2.zero, Time.deltaTime * rotationDamping);



            // Resident Evil idle slow turntable spin after 1.5s of inactivity

            if (timeSinceLastDrag > 1.5f)

            {

                angularVelocity.x = Mathf.MoveTowards(angularVelocity.x, idleTurntableSpeed, Time.deltaTime * 5f);

            }

        }



        // Apply 3D rotation to the turntable

        if (angularVelocity.sqrMagnitude > 0.001f)

        {

            turntableObj.transform.Rotate(Vector3.up, angularVelocity.x * Time.deltaTime * 45f, Space.World);

            turntableObj.transform.Rotate(Vector3.right, angularVelocity.y * Time.deltaTime * 45f, Space.Self);

        }

    }



    public void OnBookButtonClicked()

    {

        if (currentRockItem == null || currentRockItem.rockData == null) return;



        if (ArchaeologicalBookUI.Instance != null)

        {

            ArchaeologicalBookUI.Instance.OpenBook(currentRockItem.rockData);

        }

    }



    public void OnTakeButtonClicked()

    {

        if (currentRockItem == null) return;



        RockItem rockToTake = currentRockItem;

        float rockWeight = rockToTake.GetWeight();



        if (InventoryManager.Instance != null && !InventoryManager.Instance.CanAddItem(rockWeight))

        {

            Debug.LogWarning($"<color=#FF5555><b>[RockInspection]</b></color> Cannot store rock: Adding {rockWeight:F1} kg exceeds 200 kg limit!");

            return;

        }



        CloseInspection();



        if (InventoryManager.Instance != null)

        {

            InventoryManager.Instance.AddItem(rockToTake);

        }

        else if (player != null)

        {

            player.PickUpSpecificRock(rockToTake);

        }

    }



    public void OnReturnButtonClicked()

    {

        CloseInspection();

    }



    public void CloseInspection()
    {
        bool wasReturningToInv = returnToInventoryOnClose;
        returnToInventoryOnClose = false;

        CloseInspectionInternal();

        if (player != null)
        {
            player.SetControlsLocked(false);
        }

        if (wasReturningToInv && InventoryUI.Instance != null)
        {
            InventoryUI.Instance.OpenInventory();
        }
    }

    private void CloseInspectionInternal()
    {
        isInspecting = false;
        currentRockItem = null;

        if (fullScreenBackBtnObj != null) fullScreenBackBtnObj.SetActive(false);

        if (ArchaeologicalBookUI.Instance != null)
        {
            ArchaeologicalBookUI.Instance.CloseBook();
        }

        if (inspectionCanvasObj != null) inspectionCanvasObj.SetActive(false);
        if (studioRoot != null) studioRoot.SetActive(false);
        if (studioCam != null) studioCam.enabled = false;
    }

}

