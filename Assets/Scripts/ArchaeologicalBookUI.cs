using UnityEngine;
using UnityEngine.UI;
using JbelIghoud.Lithic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Prehistoric Archaeological Field Notebook / Codex UI.
/// Displays an edge-to-edge fullscreen antique field journal with
/// authentic in-game 3D model renders, geological analysis, and Jbel Ighoud cultural context.
/// </summary>
public class ArchaeologicalBookUI : MonoBehaviour
{
    private static ArchaeologicalBookUI _instance;
    public static ArchaeologicalBookUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<ArchaeologicalBookUI>();
                if (_instance == null)
                {
                    Canvas c = FindAnyObjectByType<Canvas>();
                    GameObject go = c != null ? c.gameObject : new GameObject("ArchaeologicalBookUI");
                    _instance = go.AddComponent<ArchaeologicalBookUI>();
                }
            }
            if (_instance != null) _instance.EnsureInitialized();
            return _instance;
        }
    }

    [Header("UI Canvas & Panels")]
    [SerializeField] private GameObject bookRoot;
    [SerializeField] private Image openBookBg;
    [SerializeField] private Image leftPageIllustration;
    [SerializeField] private Text leftPageMetaText;
    [SerializeField] private Text rightPageTitleText;
    [SerializeField] private Text rightPageBodyText;
    [SerializeField] private Button closeSealButton;
    [SerializeField] private Button backArrowButton;

    [Header("3D Model Render Sprites")]
    [SerializeField] private Sprite silexModelSprite;
    [SerializeField] private Sprite gresModelSprite;
    [SerializeField] private Sprite calcaireModelSprite;
    [SerializeField] private Sprite openBookSprite;
    [SerializeField] private Sprite waxSealSprite;

    private bool isOpen = false;
    public bool IsOpen => isOpen;

    private bool isInitialized = false;

    public void EnsureInitialized()
    {
        if (isInitialized) return;
        isInitialized = true;
        _instance = this;

        LoadSprites();
        BuildUIElements();
        CloseBook();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    public void LoadSprites()
    {
        if (openBookSprite == null)
            openBookSprite = LoadSprite("Assets/UI/Textures/ui_parchment_open_book_fullscreen.png") 
                             ?? LoadSprite("Assets/UI/Textures/ui_parchment_open_book.png");
        if (silexModelSprite == null)
            silexModelSprite = LoadSprite("Assets/UI/Textures/model_preview_silex.png");
        if (gresModelSprite == null)
            gresModelSprite = LoadSprite("Assets/UI/Textures/model_preview_gres.png");
        if (calcaireModelSprite == null)
            calcaireModelSprite = LoadSprite("Assets/UI/Textures/model_preview_calcaire.png");
        if (waxSealSprite == null)
            waxSealSprite = LoadSprite("Assets/UI/Textures/ui_wax_seal_close.png");
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

    public void BuildUIElements()
    {
        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) parentCanvas = FindAnyObjectByType<Canvas>();

        if (bookRoot == null && parentCanvas != null)
        {
            Transform existing = parentCanvas.transform.Find("ArchaeologicalBookModal");
            if (existing != null) bookRoot = existing.gameObject;
        }

        if (bookRoot != null)
        {
            LinkExistingBookElements(bookRoot);
            bookRoot.SetActive(false);
            return;
        }

        bookRoot = new GameObject("ArchaeologicalBookModal");
        if (parentCanvas != null) bookRoot.transform.SetParent(parentCanvas.transform, false);

        RectTransform rootRt = bookRoot.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.sizeDelta = Vector2.zero;

        // Dark ambient vignette backdrop with tap-to-close
        GameObject backdropObj = new GameObject("Backdrop");
        backdropObj.transform.SetParent(bookRoot.transform, false);
        RectTransform bdRt = backdropObj.AddComponent<RectTransform>();
        bdRt.anchorMin = Vector2.zero;
        bdRt.anchorMax = Vector2.one;
        bdRt.sizeDelta = Vector2.zero;

        Image backdrop = backdropObj.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.02f, 0.04f, 0.94f);
        Button bdBtn = backdropObj.AddComponent<Button>();
        bdBtn.onClick.AddListener(CloseBook);

        // Open Book Frame — Stretches edge-to-edge covering the entire screen
        GameObject bookFrame = new GameObject("OpenBookParchment");
        bookFrame.transform.SetParent(bookRoot.transform, false);
        RectTransform frameRt = bookFrame.AddComponent<RectTransform>();
        frameRt.anchorMin = Vector2.zero;
        frameRt.anchorMax = Vector2.one;
        frameRt.offsetMin = Vector2.zero;
        frameRt.offsetMax = Vector2.zero;
        frameRt.sizeDelta = Vector2.zero;

        openBookBg = bookFrame.AddComponent<Image>();
        if (openBookSprite != null)
        {
            openBookBg.sprite = openBookSprite;
            openBookBg.type = Image.Type.Simple;
            openBookBg.preserveAspect = false; // Stretch fully across screen!
        }
        else
        {
            openBookBg.color = new Color(0.93f, 0.89f, 0.79f);
        }

        // ==========================================
        // LEFT PAGE: In-Game 3D Model Render & Plate Details
        // ==========================================
        GameObject leftPanel = new GameObject("LeftPageContainer");
        leftPanel.transform.SetParent(bookFrame.transform, false);
        RectTransform leftRt = leftPanel.AddComponent<RectTransform>();
        leftRt.anchorMin = new Vector2(0.05f, 0.06f);
        leftRt.anchorMax = new Vector2(0.48f, 0.94f);
        leftRt.sizeDelta = Vector2.zero;

        // Illustration Image (Real in-game 3D model render)
        GameObject illObj = new GameObject("SpecimenIllustration");
        illObj.transform.SetParent(leftPanel.transform, false);
        RectTransform illRt = illObj.AddComponent<RectTransform>();
        illRt.anchorMin = new Vector2(0.12f, 0.38f);
        illRt.anchorMax = new Vector2(0.88f, 0.96f);
        illRt.sizeDelta = Vector2.zero;

        leftPageIllustration = illObj.AddComponent<Image>();
        leftPageIllustration.preserveAspect = true;
        leftPageIllustration.raycastTarget = false;
        leftPageIllustration.color = Color.white;

        // Meta Text Box (under illustration)
        GameObject metaObj = new GameObject("SpecimenMetaText");
        metaObj.transform.SetParent(leftPanel.transform, false);
        RectTransform metaRt = metaObj.AddComponent<RectTransform>();
        metaRt.anchorMin = new Vector2(0.08f, 0.04f);
        metaRt.anchorMax = new Vector2(0.92f, 0.36f);
        metaRt.sizeDelta = Vector2.zero;

        leftPageMetaText = metaObj.AddComponent<Text>();
        leftPageMetaText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        leftPageMetaText.fontSize = 15;
        leftPageMetaText.lineSpacing = 1.25f;
        leftPageMetaText.alignment = TextAnchor.UpperLeft;
        leftPageMetaText.color = new Color(0.24f, 0.17f, 0.11f, 1.0f); // Antique sepia ink
        leftPageMetaText.raycastTarget = false;

        // ==========================================
        // RIGHT PAGE: Archaeological Notes & Recipes
        // ==========================================
        GameObject rightPanel = new GameObject("RightPageContainer");
        rightPanel.transform.SetParent(bookFrame.transform, false);
        RectTransform rightRt = rightPanel.AddComponent<RectTransform>();
        rightRt.anchorMin = new Vector2(0.52f, 0.06f);
        rightRt.anchorMax = new Vector2(0.95f, 0.94f);
        rightRt.sizeDelta = Vector2.zero;

        // Title
        GameObject titleObj = new GameObject("RightPageTitle");
        titleObj.transform.SetParent(rightPanel.transform, false);
        RectTransform titleRt = titleObj.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.06f, 0.86f);
        titleRt.anchorMax = new Vector2(0.94f, 0.98f);
        titleRt.sizeDelta = Vector2.zero;

        rightPageTitleText = titleObj.AddComponent<Text>();
        rightPageTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rightPageTitleText.fontSize = 24;
        rightPageTitleText.fontStyle = FontStyle.Bold;
        rightPageTitleText.alignment = TextAnchor.MiddleLeft;
        rightPageTitleText.color = new Color(0.18f, 0.11f, 0.06f, 1.0f); // Dark iron-gall ink
        rightPageTitleText.raycastTarget = false;

        // Body Text
        GameObject bodyObj = new GameObject("RightPageBody");
        bodyObj.transform.SetParent(rightPanel.transform, false);
        RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.06f, 0.04f);
        bodyRt.anchorMax = new Vector2(0.94f, 0.85f);
        bodyRt.sizeDelta = Vector2.zero;

        rightPageBodyText = bodyObj.AddComponent<Text>();
        rightPageBodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        rightPageBodyText.fontSize = 15;
        rightPageBodyText.lineSpacing = 1.25f;
        rightPageBodyText.alignment = TextAnchor.UpperLeft;
        rightPageBodyText.color = new Color(0.20f, 0.15f, 0.10f, 1.0f);
        rightPageBodyText.raycastTarget = false;

        // ==========================================
        // TOP-LEFT CORNER: Back Arrow Button (No 'X' image on book!)
        // ==========================================
        GameObject backObj = new GameObject("BookBackArrowButton");
        backObj.transform.SetParent(bookFrame.transform, false);
        RectTransform bRt = backObj.AddComponent<RectTransform>();
        bRt.anchorMin = new Vector2(0f, 1f);
        bRt.anchorMax = new Vector2(0f, 1f);
        bRt.pivot = new Vector2(0f, 1f);
        bRt.anchoredPosition = new Vector2(30f, -25f);
        bRt.sizeDelta = new Vector2(160f, 44f);

        Image bImg = backObj.AddComponent<Image>();
        bImg.color = new Color(0.35f, 0.22f, 0.14f, 0.85f);

        Outline bOutline = backObj.AddComponent<Outline>();
        bOutline.effectColor = new Color(0.85f, 0.72f, 0.45f, 0.7f);
        bOutline.effectDistance = new Vector2(1.5f, -1.5f);

        backArrowButton = backObj.AddComponent<Button>();
        backArrowButton.onClick.AddListener(CloseBook);

        GameObject bTextObj = new GameObject("BackText");
        bTextObj.transform.SetParent(backObj.transform, false);
        RectTransform btRt = bTextObj.AddComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.sizeDelta = Vector2.zero;

        Text bTxt = bTextObj.AddComponent<Text>();
        bTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        bTxt.fontSize = 14;
        bTxt.fontStyle = FontStyle.Bold;
        bTxt.alignment = TextAnchor.MiddleCenter;
        bTxt.color = new Color(0.96f, 0.88f, 0.75f);
        bTxt.text = "← BACK";
        bTxt.raycastTarget = false;

        // ==========================================
        // TOP-RIGHT CORNER: Authentic Wax Seal Close Button (NO 'X' IMAGE!)
        // ==========================================
        GameObject sealObj = new GameObject("CloseWaxSealButton");
        sealObj.transform.SetParent(bookFrame.transform, false);
        RectTransform sealRt = sealObj.AddComponent<RectTransform>();
        sealRt.anchorMin = new Vector2(1f, 1f);
        sealRt.anchorMax = new Vector2(1f, 1f);
        sealRt.pivot = new Vector2(1f, 1f);
        sealRt.sizeDelta = new Vector2(85f, 85f);
        sealRt.anchoredPosition = new Vector2(-25f, -20f);

        Image sealImg = sealObj.AddComponent<Image>();
        if (waxSealSprite != null)
        {
            sealImg.sprite = waxSealSprite;
            sealImg.preserveAspect = true;
        }
        else
        {
            sealImg.color = new Color(0.75f, 0.15f, 0.15f);
        }

        closeSealButton = sealObj.AddComponent<Button>();
        closeSealButton.onClick.AddListener(CloseBook);

        GameObject sealLabel = new GameObject("SealLabel");
        sealLabel.transform.SetParent(sealObj.transform, false);
        RectTransform slRt = sealLabel.AddComponent<RectTransform>();
        slRt.anchorMin = Vector2.zero;
        slRt.anchorMax = Vector2.one;
        slRt.sizeDelta = Vector2.zero;

        Text st = sealLabel.AddComponent<Text>();
        st.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        st.fontSize = 12;
        st.fontStyle = FontStyle.Bold;
        st.alignment = TextAnchor.MiddleCenter;
        st.color = new Color(1f, 0.95f, 0.85f, 0.95f);
        st.text = "CLOSE";
        st.raycastTarget = false;
    }

    private void LinkExistingBookElements(GameObject root)
    {
        if (root == null) return;

        // Backdrop button
        var bd = root.transform.Find("Backdrop");
        if (bd != null)
        {
            var btn = bd.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveListener(CloseBook);
                btn.onClick.AddListener(CloseBook);
            }
        }

        // Open book bg
        if (openBookBg == null)
        {
            var f = root.transform.Find("OpenBookParchment");
            if (f != null) openBookBg = f.GetComponent<Image>();
        }

        // Left Page
        if (leftPageIllustration == null)
        {
            var ill = root.transform.Find("OpenBookParchment/LeftPageContent/IllustrationArea/Illustration");
            if (ill != null) leftPageIllustration = ill.GetComponent<Image>();
        }
        if (leftPageMetaText == null)
        {
            var m = root.transform.Find("OpenBookParchment/LeftPageContent/MetaPanel/MetaText");
            if (m != null) leftPageMetaText = m.GetComponent<Text>();
        }

        // Right Page
        if (rightPageTitleText == null)
        {
            var t = root.transform.Find("OpenBookParchment/RightPageContent/PageTitle");
            if (t != null) rightPageTitleText = t.GetComponent<Text>();
        }
        if (rightPageBodyText == null)
        {
            var b = root.transform.Find("OpenBookParchment/RightPageContent/PageBody");
            if (b != null) rightPageBodyText = b.GetComponent<Text>();
        }

        // Close and Back Buttons
        if (backArrowButton == null)
        {
            var btn = root.transform.Find("OpenBookParchment/RightPageContent/BackButton");
            if (btn != null) backArrowButton = btn.GetComponent<Button>();
        }
        if (backArrowButton != null)
        {
            backArrowButton.onClick.RemoveListener(CloseBook);
            backArrowButton.onClick.AddListener(CloseBook);
        }

        if (closeSealButton == null)
        {
            var btn = root.transform.Find("OpenBookParchment/SealCloseButton");
            if (btn != null) closeSealButton = btn.GetComponent<Button>();
        }
        if (closeSealButton != null)
        {
            closeSealButton.onClick.RemoveListener(CloseBook);
            closeSealButton.onClick.AddListener(CloseBook);
        }
    }

    private void Update()
    {
        if (!isOpen) return;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseBook();
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CloseBook();
        }
#endif
    }

    public void OpenBook(RockData rockData)
    {
        if (bookRoot == null) BuildUIElements();

        if (openBookBg != null && openBookSprite != null)
        {
            openBookBg.sprite = openBookSprite;
        }

        isOpen = true;
        bookRoot.SetActive(true);

        TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
        if (player != null) player.SetControlsLocked(true);

        PopulatePageContent(rockData);
    }

    public void CloseBook()
    {
        isOpen = false;
        if (bookRoot != null)
        {
            bookRoot.SetActive(false);
        }

        TopDownPlayerController player = FindAnyObjectByType<TopDownPlayerController>();
        if (player != null && (InventoryUI.Instance == null || !InventoryUI.Instance.IsOpen))
        {
            player.SetControlsLocked(false);
        }
    }

    private Sprite GetRockSprite(RockType rType, string rockName)
    {
        string n = rockName != null ? rockName.ToLower() : "";
        if (rType == RockType.Silex || n.Contains("silex") || n.Contains("flint"))
        {
            if (silexModelSprite == null) silexModelSprite = LoadSprite("Assets/UI/Textures/model_preview_silex.png");
            return silexModelSprite;
        }
        if (rType == RockType.Gres || n.Contains("grès") || n.Contains("sandstone") || n.Contains("gres"))
        {
            if (gresModelSprite == null) gresModelSprite = LoadSprite("Assets/UI/Textures/model_preview_gres.png");
            return gresModelSprite;
        }
        if (calcaireModelSprite == null) calcaireModelSprite = LoadSprite("Assets/UI/Textures/model_preview_calcaire.png");
        return calcaireModelSprite;
    }

    private void PopulatePageContent(RockData data)
    {
        if (data == null) return;

        RockType rType = data.rockType;
        Sprite modelSprite = GetRockSprite(rType, data.rockName);

        if (leftPageIllustration != null)
        {
            leftPageIllustration.sprite = modelSprite;
            leftPageIllustration.enabled = modelSprite != null;
            leftPageIllustration.color = Color.white;
        }

        if (rType == RockType.Silex || data.rockName.ToLower().Contains("silex") || data.rockName.ToLower().Contains("flint"))
        {
            leftPageMetaText.text =
                "<b>PLATE XII — LEVALLOIS FLINT CORE (IN-GAME SPECIMEN)</b>\n" +
                "• Specimen No: JI-1961-FL01 (Jbel Ighoud, Morocco)\n" +
                "• Stratum: Layer 7 (Thermoluminescence dated ~300,000 BP)\n" +
                "• Mineral: Cryptocrystalline Silica (Chalcedony / Chert)\n" +
                "• Mohs Hardness: 20.0 / Mass: 1.4 kg / Knapped Levallois Core";

            rightPageTitleText.text = "LE SILEX (FLINT) — LITHIC INDUSTRY";
            rightPageBodyText.text =
                "<b>ARCHAEOLOGICAL DISCOVERY CONTEXT:</b>\n" +
                "Central to the landmark discovery of the earliest known <i>Homo sapiens</i> (~315,000 BP) at Jbel Ighoud. " +
                "Early humans collected local chert nodules, deliberately preparing turtle-back Levallois cores to strike razor-sharp triangular spear points and scrapers.\n\n" +
                "<b>EXPERIMENTAL LITHIC RECIPES:</b>\n" +
                "• <b>Striking with Grès (Sandstone Hammerstone):</b>\n" +
                "  Knaps crisp Levallois blades and micro-flakes with sharp conchoidal edges.\n" +
                "• <b>Flint-on-Flint Percussion:</b>\n" +
                "  Direct hard hammer percussion producing razor cutting flakes.\n" +
                "• <b>Incision on Calcaire (Limestone):</b>\n" +
                "  Carves soft cave bedrock, producing fine white chalk pigment.";
        }
        else if (rType == RockType.Gres || data.rockName.ToLower().Contains("grès") || data.rockName.ToLower().Contains("sandstone"))
        {
            leftPageMetaText.text =
                "<b>PLATE XIV — PERCUTEUR EN GRÈS (IN-GAME SPECIMEN)</b>\n" +
                "• Specimen No: JI-1961-GS02 (Jbel Ighoud, Morocco)\n" +
                "• Stratum: Middle Paleolithic Archaeological Horizon\n" +
                "• Mineral: Quartzite / Quartz-cemented Sandstone\n" +
                "• Mohs Hardness: 18.0 / Mass: 1.6 kg / Heavy Hammerstone";

            rightPageTitleText.text = "LE GRÈS (SANDSTONE) — PERCUTEUR";
            rightPageBodyText.text =
                "<b>ARCHAEOLOGICAL DISCOVERY CONTEXT:</b>\n" +
                "Coarse, heavy quartzitic cobbles utilized as primary percussion tools (<i>percuteurs</i>) across Jbel Ighoud. " +
                "The distinctive impact pitting on both ends reveals repeated high-velocity strikes used to knap flint and shatter bones for nutritious marrow.\n\n" +
                "<b>EXPERIMENTAL LITHIC RECIPES:</b>\n" +
                "• <b>Percussion on Silex (Flint Core):</b>\n" +
                "  Delivers focused kinetic shock to detach precise Levallois points without fracturing the hammerstone.\n" +
                "• <b>Crushing Calcaire (Limestone):</b>\n" +
                "  Crushes brittle limestone blocks into powdered pigment.\n" +
                "• <b>Sandstone-on-Sandstone Abrasion:</b>\n" +
                "  Grinds surfaces smooth; ancient quern and abrader function.";
        }
        else // Calcaire / Limestone
        {
            leftPageMetaText.text =
                "<b>PLATE XVI — CALCAIRE KARSTIQUE (IN-GAME SPECIMEN)</b>\n" +
                "• Specimen No: JI-1961-CL03 (Jbel Ighoud Cave Wall)\n" +
                "• Stratum: Cave Karstic Bedrock Formation\n" +
                "• Mineral: Calcium Carbonate (Calcite / Chalk)\n" +
                "• Mohs Hardness: 10.0 / Mass: 1.1 kg / Cave Bedrock Nodule";

            rightPageTitleText.text = "LE CALCAIRE (LIMESTONE) — CAVE ROCK";
            rightPageBodyText.text =
                "<b>ARCHAEOLOGICAL DISCOVERY CONTEXT:</b>\n" +
                "Forms the primary karstic cave shelter of Jbel Ighoud overlooking the Moroccan plains. " +
                "Soft, friable, and rich in natural fissures, early humans utilized it as hearth lining, wall incising canvas, and white chalk pigment for body marking and hides.\n\n" +
                "<b>EXPERIMENTAL LITHIC RECIPES:</b>\n" +
                "• <b>Slicing with Silex (Flint Blade):</b>\n" +
                "  Easily carved and grooved; generates chalk powder.\n" +
                "• <b>Pounding with Grès (Hammerstone):</b>\n" +
                "  Shatters rapidly under moderate impact into chalk clumps and dust.\n" +
                "• <b>Limestone Friction:</b>\n" +
                "  Rubbing blocks together creates fine white pigment powder.";
        }
    }
}
