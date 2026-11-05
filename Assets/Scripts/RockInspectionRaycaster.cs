using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

/// <summary>
/// Hidden raycast system for pointing at rocks.
/// When pointing at a rock via camera center ray or touching it on mobile,
/// an elegant archaeological inspection prompt appears.
/// Tapping the prompt or pressing [E] opens the Resident Evil style 3D item inspection view.
/// </summary>
public class RockInspectionRaycaster : MonoBehaviour
{
    private static RockInspectionRaycaster _instance;
    public static RockInspectionRaycaster Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<RockInspectionRaycaster>();
                if (_instance == null)
                {
                    Canvas c = FindAnyObjectByType<Canvas>();
                    GameObject go = c != null ? c.gameObject : new GameObject("RockInspectionRaycaster");
                    _instance = go.AddComponent<RockInspectionRaycaster>();
                }
            }
            return _instance;
        }
    }

    [Header("Raycast Settings")]
    [Tooltip("Maximum interaction distance from camera to target a rock.")]
    [SerializeField] private float maxInteractionDistance = 14.0f;

    [Tooltip("Maximum interaction distance from player to target a rock.")]
    [SerializeField] private float maxPlayerDistance = 5.0f;

    [Tooltip("Layers checked for interactable rocks.")]
    [SerializeField] private LayerMask rockLayerMask = ~0; // Everything by default

    [Header("UI Visuals")]
    [Tooltip("Dedicated contextual HUD button on the mobile control panel.")]
    [SerializeField] private Button inspectHUDButton;

    [Tooltip("World or screen space reticle bracket / prompt.")]
    private GameObject reticleUI;
    private TextMeshProUGUI promptText;
    private CanvasGroup canvasGroup;

    private RockItem currentTargetedRock = null;
    private Camera mainCamera;
    private TopDownPlayerController player;

    public RockItem CurrentTargetedRock => currentTargetedRock;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        if (maxInteractionDistance < 14f) maxInteractionDistance = 14f;
        if (maxPlayerDistance < 5f) maxPlayerDistance = 5f;

#if ENABLE_INPUT_SYSTEM
        EnhancedTouchSupport.Enable();
#endif
        CreateReticleUI();
    }

    private void Start()
    {
        mainCamera = Camera.main;
        player = FindAnyObjectByType<TopDownPlayerController>();
    }

    private void CreateReticleUI()
    {
        // Find existing HUD canvas
        Canvas hudCanvas = FindAnyObjectByType<Canvas>();
        if (hudCanvas == null)
        {
            GameObject cObj = new GameObject("InspectionPromptCanvas");
            hudCanvas = cObj.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cObj.AddComponent<CanvasScaler>();
            cObj.AddComponent<GraphicRaycaster>();
        }

        Transform existingPrompt = hudCanvas.transform.Find("RockTargetPrompt");
        if (existingPrompt != null)
        {
            reticleUI = existingPrompt.gameObject;
            canvasGroup = reticleUI.GetComponent<CanvasGroup>();
            promptText = reticleUI.GetComponentInChildren<TextMeshProUGUI>();
            SetupInspectHUDButton(hudCanvas);
            return;
        }

        reticleUI = new GameObject("RockTargetPrompt");
        reticleUI.transform.SetParent(hudCanvas.transform, false);

        canvasGroup = reticleUI.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;

        RectTransform rt = reticleUI.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(340f, 90f);
        rt.anchorMin = new Vector2(0.5f, 0.35f);
        rt.anchorMax = new Vector2(0.5f, 0.35f);
        rt.anchoredPosition = Vector2.zero;

        // Background styling
        Image bgImg = reticleUI.AddComponent<Image>();
        bgImg.color = new Color(0.08f, 0.08f, 0.12f, 0.85f);

        // Add Button component for touch tap on mobile
        Button btn = reticleUI.AddComponent<Button>();
        btn.onClick.AddListener(OnPromptClicked);

        // Text prompt
        GameObject textObj = new GameObject("PromptText");
        textObj.transform.SetParent(reticleUI.transform, false);
        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        promptText = textObj.AddComponent<TextMeshProUGUI>();
        promptText.fontSize = 17;
        promptText.alignment = TextAlignmentOptions.Center;
        promptText.color = new Color(1.0f, 0.92f, 0.75f, 1.0f);
        promptText.text = "<b>[ TAP TO INSPECT ROCK ]</b>";

        // Setup dedicated right-thumb InspectButton on the mobile HUD
        SetupInspectHUDButton(hudCanvas);
    }

    private void SetupInspectHUDButton(Canvas hudCanvas)
    {
        if (hudCanvas == null) return;

        Transform existingInspect = hudCanvas.transform.Find("InspectButton");
        GameObject inspGo = null;
        if (existingInspect != null)
        {
            inspGo = existingInspect.gameObject;
        }
        else
        {
            inspGo = new GameObject("InspectButton");
            inspGo.transform.SetParent(hudCanvas.transform, false);
        }

        RectTransform irt = inspGo.GetComponent<RectTransform>();
        if (irt == null) irt = inspGo.AddComponent<RectTransform>();
        irt.anchorMin = new Vector2(1f, 0f);
        irt.anchorMax = new Vector2(1f, 0f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.anchoredPosition = new Vector2(-360f, 220f);
        irt.sizeDelta = new Vector2(120f, 120f);

        Image inspImg = inspGo.GetComponent<Image>();
        if (inspImg == null) inspImg = inspGo.AddComponent<Image>();
        inspImg.color = new Color(0.88f, 0.65f, 0.22f, 0.92f); // Prehistoric amber gold

        Transform pickTr = hudCanvas.transform.Find("PickButton");
        if (pickTr != null)
        {
            Image pImg = pickTr.GetComponent<Image>();
            if (pImg != null && pImg.sprite != null) inspImg.sprite = pImg.sprite;
        }

        inspectHUDButton = inspGo.GetComponent<Button>();
        if (inspectHUDButton == null) inspectHUDButton = inspGo.AddComponent<Button>();

        Transform textChild = inspGo.transform.Find("Text");
        GameObject inspTxtObj = textChild != null ? textChild.gameObject : new GameObject("Text");
        inspTxtObj.transform.SetParent(inspGo.transform, false);

        RectTransform trt = inspTxtObj.GetComponent<RectTransform>();
        if (trt == null) trt = inspTxtObj.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;

        TextMeshProUGUI itxt = inspTxtObj.GetComponent<TextMeshProUGUI>();
        if (itxt == null) itxt = inspTxtObj.AddComponent<TextMeshProUGUI>();
        itxt.fontSize = 16;
        itxt.fontStyle = FontStyles.Bold;
        itxt.alignment = TextAlignmentOptions.Center;
        itxt.color = Color.white;
        itxt.text = "INSPECT";

        inspectHUDButton.onClick.RemoveListener(OnPromptClicked);
        inspectHUDButton.onClick.AddListener(OnPromptClicked);
        inspGo.SetActive(false);
    }

    private void Update()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // If currently in inspection or smash mode, hide prompt
        if (RockInspectionManager.Instance != null && RockInspectionManager.Instance.IsInspecting)
        {
            SetPromptVisible(false);
            return;
        }

        if (RockDragManager.Instance != null && RockDragManager.Instance.IsInSmashMode)
        {
            SetPromptVisible(false);
            return;
        }

        PerformHiddenRaycast();

        // Keyboard interact shortcut [E]
        if (currentTargetedRock != null)
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                OnPromptClicked();
                return;
            }
#else
            if (Input.GetKeyDown(KeyCode.E))
            {
                OnPromptClicked();
                return;
            }
#endif
        }

        // Mobile touch check directly on screen
        CheckTouchRaycast();
    }

    private void PerformHiddenRaycast()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;
        if (player == null) player = FindAnyObjectByType<TopDownPlayerController>();
        if (promptText == null) CreateReticleUI();

        RockItem foundRock = null;

        // 1. Screen-center spherecast through camera
        Ray camRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.45f, 0f));
        RaycastHit[] camHits = Physics.SphereCastAll(camRay, 0.85f, maxInteractionDistance, rockLayerMask, QueryTriggerInteraction.Ignore);
        float closestDist = float.MaxValue;

        foreach (var h in camHits)
        {
            RockItem r = h.collider.GetComponentInParent<RockItem>();
            if (r != null && !r.isHeld)
            {
                // Check distance from player if player exists
                if (player != null && Vector3.Distance(player.transform.position, r.transform.position) > maxPlayerDistance)
                    continue;

                if (h.distance < closestDist)
                {
                    closestDist = h.distance;
                    foundRock = r;
                }
            }
        }

        // 2. Player forward spherecast fallback
        if (foundRock == null && player != null)
        {
            Vector3 pOrigin = player.transform.position + Vector3.up * 0.6f;
            Vector3 pDir = (player.transform.forward + Vector3.down * 0.25f).normalized;
            RaycastHit[] pHits = Physics.SphereCastAll(pOrigin, 0.8f, pDir, maxPlayerDistance, rockLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var h in pHits)
            {
                RockItem r = h.collider.GetComponentInParent<RockItem>();
                if (r != null && !r.isHeld)
                {
                    foundRock = r;
                    break;
                }
            }
        }

        // 3. Close proximity check (within 2.2m if facing)
        if (foundRock == null && player != null)
        {
            Collider[] overlaps = Physics.OverlapSphere(player.transform.position, 2.2f, rockLayerMask, QueryTriggerInteraction.Ignore);
            foreach (var col in overlaps)
            {
                RockItem r = col.GetComponentInParent<RockItem>();
                if (r != null && !r.isHeld)
                {
                    Vector3 toRock = (r.transform.position - player.transform.position).normalized;
                    if (Vector3.Dot(player.transform.forward, toRock) > 0.3f)
                    {
                        foundRock = r;
                        break;
                    }
                }
            }
        }

        currentTargetedRock = foundRock;

        if (currentTargetedRock != null)
        {
            string rockName = currentTargetedRock.rockData != null ? currentTargetedRock.rockData.rockName : currentTargetedRock.name;
            promptText.text = $"<color=#F6C878><b>[ {rockName.ToUpper()} ]</b></color>\n<size=13>TAP / PRESS [E] TO INSPECT</size>";
            SetPromptVisible(true);
        }
        else
        {
            SetPromptVisible(false);
        }
    }

    private void CheckTouchRaycast()
    {
#if ENABLE_INPUT_SYSTEM
        var touches = Touch.activeTouches;
        if (touches.Count > 0)
        {
            var touch = touches[0];
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                Ray touchRay = mainCamera.ScreenPointToRay(touch.screenPosition);
                if (Physics.Raycast(touchRay, out RaycastHit hit, maxInteractionDistance + 2f, rockLayerMask, QueryTriggerInteraction.Ignore))
                {
                    RockItem touchedRock = hit.collider.GetComponentInParent<RockItem>();
                    if (touchedRock != null && !touchedRock.isHeld)
                    {
                        currentTargetedRock = touchedRock;
                        OnPromptClicked();
                    }
                }
            }
        }
#endif
    }

    private void SetPromptVisible(bool visible)
    {
        if (canvasGroup != null)
        {
            float targetAlpha = visible ? 1f : 0f;
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, Time.deltaTime * 6f);
            canvasGroup.blocksRaycasts = visible;
        }

        if (inspectHUDButton != null)
        {
            bool showBtn = visible && (player == null || player.HeldRockCount < 2);
            if (inspectHUDButton.gameObject.activeSelf != showBtn)
            {
                inspectHUDButton.gameObject.SetActive(showBtn);
            }
        }
    }

    public void OnPromptClicked()
    {
        if (currentTargetedRock == null) return;

        if (RockInspectionManager.Instance != null)
        {
            RockInspectionManager.Instance.OpenInspection(currentTargetedRock);
            SetPromptVisible(false);
        }
    }
}
