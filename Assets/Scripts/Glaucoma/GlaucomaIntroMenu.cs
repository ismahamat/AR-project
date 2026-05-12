using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRInputFeatureUsageBool = UnityEngine.XR.InputFeatureUsage<bool>;
using XRInputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class GlaucomaIntroMenu : MonoBehaviour
{
    private enum MenuState
    {
        Carousel,
        HandicapSelection,
        Simulation
    }

    private enum IconStyle
    {
        Glaucoma,
        Daltonism,
        Nystagmus,
        DiabeticRetinopathy,
        Visuospatial
    }

    private sealed class HandicapCard
    {
        public string label;
        public bool available;
        public Button button;
        public RectTransform rectTransform;
        public Graphic border;
        public CanvasGroup canvasGroup;
        public Vector2 basePosition;
    }

    [Header("Menu")]
    public Canvas introCanvas;
    public Button testerButton;
    public string welcomeTitle = "Bienvenue dans Disability - AR";
    [TextArea(2, 4)]
    public string welcomeSubtitle = "Un parcours immersif pour ressentir l'impact des handicaps dans des taches du quotidien.";
    public float carouselSlideDuration = 6f;
    public float carouselRadius = 270f;
    public float carouselDepth = 155f;
    [Range(1f, 16f)]
    public float carouselSmoothness = 8f;
    [Range(1f, 16f)]
    public float menuSmoothness = 9f;
    public KeyCode keyboardSkipKey = KeyCode.Space;

    [Header("Experience")]
    public GlaucomaSimulationController simulationController;
    public GameObject passthroughLayer;
    public GameObject[] simulationObjects;

    [Header("Simulation layout")]
    public Transform headsetAnchor;
    public Transform worldInfoText;
    public Transform awarenessTargetsRoot;
    public float infoTextDistance = 1.25f;
    public float targetsDistance = 2.1f;

    [Header("Fallback controls")]
    public bool allowKeyboardStart = true;
    public bool allowControllerStart = true;
    public KeyCode keyboardStartKey = KeyCode.Space;
    [Range(0.05f, 1f)]
    public float triggerStartThreshold = 0.35f;

    private readonly List<XRInputDevice> xrDevices = new List<XRInputDevice>();
    private readonly List<HandicapCard> handicapCards = new List<HandicapCard>();
    private readonly List<RectTransform> carouselCards = new List<RectTransform>();
    private readonly List<CanvasGroup> carouselCardGroups = new List<CanvasGroup>();
    private readonly Dictionary<IconStyle, Sprite> realImageCache = new Dictionary<IconStyle, Sprite>();
    private readonly string[] carouselTitles =
    {
        "Comprendre par l'experience",
        "Un handicap, une perception",
        "Des defis du quotidien"
    };

    private readonly string[] carouselBodies =
    {
        "Le casque modifie volontairement ce que vous voyez pour simuler une limitation visuelle.",
        "Chaque module reproduit un effet different afin de comparer les difficultes de perception.",
        "Vous devrez accomplir de petites taches simples pour ressentir concretement les obstacles."
    };

    private bool simulationStarted;
    private bool triggerWasPressed;
    private bool waitingForSelectionInputRelease;
    private MenuState currentState = MenuState.Carousel;
    private RectTransform menuRoot;
    private RectTransform contentPanel;
    private RectTransform carouselRoot;
    private RectTransform selectionRoot;
    private Text carouselTitleText;
    private Text carouselBodyText;
    private Text carouselProgressText;
    private float carouselTimer;
    private float carouselVisualIndex;
    private float activationInputLockedUntil;
    private int currentCarouselIndex;
    private int selectedHandicapIndex;
    private Font defaultFont;

    private void Awake()
    {
        if (testerButton != null)
        {
            testerButton.onClick.AddListener(StartSimulation);
        }
    }

    private void Start()
    {
        EnsureMenuCanvas();
        BuildMenuInterface();
        ShowIntro();
    }

    private void Update()
    {
        if (currentState == MenuState.Simulation)
        {
            return;
        }

        if (currentState == MenuState.Carousel)
        {
            UpdateCarousel();
            if (IsSkipPressed())
            {
                ShowHandicapSelection();
            }

            return;
        }

        if (currentState == MenuState.HandicapSelection)
        {
            UpdateHandicapSelectionInput();
            UpdateSelectionCardTransforms(Time.deltaTime);
        }
    }

    public void ShowIntro()
    {
        simulationStarted = false;
        currentState = MenuState.Carousel;
        carouselTimer = 0f;
        currentCarouselIndex = 0;
        carouselVisualIndex = 0f;

        if (introCanvas != null)
        {
            introCanvas.gameObject.SetActive(true);
        }

        ShowCarousel();
        SetSimulationObjectsActive(false);

        if (passthroughLayer != null)
        {
            passthroughLayer.SetActive(false);
        }

        if (simulationController != null)
        {
            simulationController.SetSimulationActive(false);
        }
    }

    public void StartSimulation()
    {
        if (simulationStarted)
        {
            return;
        }

        simulationStarted = true;
        currentState = MenuState.Simulation;

        if (introCanvas != null)
        {
            introCanvas.gameObject.SetActive(false);
        }

        if (passthroughLayer != null)
        {
#if UNITY_EDITOR
            passthroughLayer.SetActive(false);
#else
            passthroughLayer.SetActive(true);
#endif
        }

        SetSimulationObjectsActive(true);
        RecenterSimulationContent();

        if (simulationController != null)
        {
            simulationController.SetSimulationActive(true);
        }
    }

    private void EnsureMenuCanvas()
    {
        if (introCanvas == null)
        {
            GameObject canvasObject = new GameObject("Global_Disability_Menu_Canvas");
            introCanvas = canvasObject.AddComponent<Canvas>();
            introCanvas.renderMode = RenderMode.WorldSpace;
            introCanvas.sortingOrder = 20;
            canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (introCanvas.GetComponent<GraphicRaycaster>() == null)
        {
            introCanvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        if (introCanvas.worldCamera == null)
        {
            introCanvas.worldCamera = ResolveMenuCamera();
        }

        if (introCanvas.renderMode != RenderMode.WorldSpace)
        {
            introCanvas.renderMode = RenderMode.WorldSpace;
        }

        RectTransform canvasTransform = introCanvas.transform as RectTransform;
        if (canvasTransform != null)
        {
            canvasTransform.sizeDelta = new Vector2(1400f, 900f);
            PositionCanvasInFrontOfHeadset(canvasTransform);
        }
    }

    private Camera ResolveMenuCamera()
    {
        if (simulationController != null && simulationController.targetCamera != null)
        {
            return simulationController.targetCamera;
        }

        Transform anchor = ResolveHeadsetAnchor();
        if (anchor != null)
        {
            Camera anchorCamera = anchor.GetComponent<Camera>();
            if (anchorCamera != null)
            {
                return anchorCamera;
            }
        }

        return Camera.main;
    }

    private void PositionCanvasInFrontOfHeadset(RectTransform canvasTransform)
    {
        Transform anchor = ResolveHeadsetAnchor();
        if (anchor == null)
        {
            canvasTransform.position = new Vector3(0f, 1.45f, 2.6f);
            canvasTransform.rotation = Quaternion.identity;
            canvasTransform.localScale = Vector3.one * 0.0018f;
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        canvasTransform.position = anchor.position + forward * 2.35f + Vector3.up * -0.05f;
        canvasTransform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        canvasTransform.localScale = Vector3.one * 0.0018f;
    }

    private void BuildMenuInterface()
    {
        defaultFont = ResolveDefaultFont();
        ClearMenuCanvas();

        menuRoot = CreateRect("Global_Menu_Root", introCanvas.transform as RectTransform);
        Stretch(menuRoot);

        Image backdrop = menuRoot.gameObject.AddComponent<Image>();
        backdrop.color = new Color(0.006f, 0.006f, 0.008f, 0.66f);

        AddBackgroundDepth(menuRoot);

        contentPanel = CreateRect("Vision_Glass_Window", menuRoot);
        SetRect(contentPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1240f, 790f));
        RoundedRectGraphic panelGlass = contentPanel.gameObject.AddComponent<RoundedRectGraphic>();
        panelGlass.cornerRadius = 34f;
        panelGlass.cornerSegments = 12;
        panelGlass.color = new Color(0.09f, 0.09f, 0.095f, 0.56f);
        AddVisionGlassWindowDecor(contentPanel);

        carouselRoot = CreateRect("Welcome_Carousel", contentPanel);
        Stretch(carouselRoot);
        BuildCarousel();

        selectionRoot = CreateRect("Handicap_Selection", contentPanel);
        Stretch(selectionRoot);
        BuildHandicapSelection();
    }

    private static Font ResolveDefaultFont()
    {
        try
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }
        }
        catch (System.ArgumentException)
        {
        }

        return Font.CreateDynamicFontFromOSFont("Arial", 24);
    }

    private void ClearMenuCanvas()
    {
        Transform canvasTransform = introCanvas.transform;
        for (int i = canvasTransform.childCount - 1; i >= 0; i--)
        {
            Destroy(canvasTransform.GetChild(i).gameObject);
        }

        testerButton = null;
        handicapCards.Clear();
        carouselCards.Clear();
        carouselCardGroups.Clear();
    }

    private void BuildCarousel()
    {
        Text welcome = CreateText("Welcome_Title", carouselRoot, welcomeTitle, 46, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(welcome.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(1080f, 78f));

        Text subtitle = CreateText("Welcome_Subtitle", carouselRoot, welcomeSubtitle, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
        subtitle.color = new Color(0.86f, 0.9f, 0.94f, 0.92f);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(960f, 58f));

        RectTransform cardsRoot = CreateRect("Carousel_3D_Cards", carouselRoot);
        SetRect(cardsRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -26f), new Vector2(1010f, 330f));

        for (int i = 0; i < carouselTitles.Length; i++)
        {
            RectTransform card = CreateCarouselCard(cardsRoot, i);
            carouselCards.Add(card);
            carouselCardGroups.Add(card.GetComponent<CanvasGroup>());
        }

        carouselTitleText = CreateText("Carousel_Current_Title", carouselRoot, string.Empty, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(carouselTitleText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(980f, 54f));

        carouselBodyText = CreateText("Carousel_Current_Body", carouselRoot, string.Empty, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
        carouselBodyText.color = new Color(0.86f, 0.91f, 0.95f, 0.92f);
        SetRect(carouselBodyText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 101f), new Vector2(980f, 52f));

        carouselProgressText = CreateText("Carousel_Progress", carouselRoot, string.Empty, 18, FontStyle.Normal, TextAnchor.MiddleCenter);
        carouselProgressText.color = new Color(0.78f, 0.8f, 0.84f, 0.78f);
        SetRect(carouselProgressText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(820f, 34f));

        Button skipButton = CreateButton("Button_Skip_Carousel", carouselRoot, "Passer", new Color(1f, 1f, 1f, 0.18f));
        SetRect(skipButton.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(220f, 50f));
        AddVisionButtonDecor(skipButton.transform as RectTransform);
        Text skipText = skipButton.GetComponentInChildren<Text>();
        if (skipText != null)
        {
            skipText.fontSize = 22;
            skipText.color = new Color(0.95f, 0.98f, 1f, 1f);
            skipText.alignment = TextAnchor.MiddleCenter;
            skipText.horizontalOverflow = HorizontalWrapMode.Overflow;
            skipText.verticalOverflow = VerticalWrapMode.Overflow;
            Stretch(skipText.rectTransform);
            skipText.transform.SetAsLastSibling();
        }

        skipButton.onClick.AddListener(ShowHandicapSelection);

        UpdateCarouselTexts();
    }

    private RectTransform CreateCarouselCard(RectTransform parent, int index)
    {
        RectTransform card = CreateRect("Carousel_Card_" + index, parent);
        SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 218f));
        card.gameObject.AddComponent<CanvasGroup>();

        RoundedRectGraphic background = card.gameObject.AddComponent<RoundedRectGraphic>();
        background.cornerRadius = 28f;
        background.cornerSegments = 12;
        background.color = new Color(1f, 1f, 1f, 0.12f);
        AddVisionCardDecor(card, GetSlideAccent(index), true);

        RectTransform icon = BuildSlideIcon(card, index);
        SetRect(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(150f, 92f));

        Text title = CreateText("Carousel_Card_Title", card, carouselTitles[index], 22, FontStyle.Bold, TextAnchor.MiddleCenter);
        title.color = new Color(0.94f, 0.97f, 1f, 0.96f);
        SetRect(title.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 58f), new Vector2(246f, 54f));
        return card;
    }

    private void BuildHandicapSelection()
    {
        Text title = CreateText("Selection_Title", selectionRoot, "Choisissez un handicap à tester", 42, FontStyle.Bold, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(1060f, 72f));

        RectTransform grid = CreateRect("Handicap_Cards_Grid", selectionRoot);
        SetRect(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -16f), new Vector2(1080f, 540f));

        CreateHandicapCard(grid, 0, "Glaucome", IconStyle.Glaucoma, true, new Vector2(-352f, 116f));
        CreateHandicapCard(grid, 1, "Daltonisme", IconStyle.Daltonism, false, new Vector2(0f, 116f));
        CreateHandicapCard(grid, 2, "Nystagmus", IconStyle.Nystagmus, false, new Vector2(352f, 116f));
        CreateHandicapCard(grid, 3, "Rétinopathie diabétique", IconStyle.DiabeticRetinopathy, false, new Vector2(-176f, -150f));
        CreateHandicapCard(grid, 4, "Troubles visiospatiaux", IconStyle.Visuospatial, false, new Vector2(176f, -150f));

        Text hint = CreateText("Selection_Hint", selectionRoot, "Entree / Espace pour lancer le glaucome. Les autres modules seront ajoutes ensuite.", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
        hint.color = new Color(0.73f, 0.8f, 0.88f, 0.78f);
        SetRect(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(980f, 50f));

        UpdateSelectedHandicap(0);
    }

    private void CreateHandicapCard(RectTransform parent, int index, string label, IconStyle iconStyle, bool available, Vector2 position)
    {
        Button cardButton = CreateButton("Card_" + label.Replace(" ", "_"), parent, string.Empty, available ? new Color(1f, 1f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0.07f));
        RectTransform rect = cardButton.transform as RectTransform;
        SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(292f, 214f));
        cardButton.interactable = available;
        CanvasGroup canvasGroup = cardButton.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = available ? 1f : 0.72f;

        Graphic border = cardButton.targetGraphic;
        AddVisionCardDecor(rect, GetHandicapAccent(iconStyle), available);

        RectTransform icon = TryGetRealHandicapImage(iconStyle, out Sprite realImage)
            ? BuildRealImageIcon(rect, realImage, available)
            : BuildHandicapIcon(rect, iconStyle, available);
        SetRect(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(214f, 96f));

        Text labelText = CreateText("Label", rect, label, label.Length > 18 ? 20 : 25, FontStyle.Bold, TextAnchor.MiddleCenter);
        labelText.color = available ? new Color(0.96f, 0.98f, 1f, 1f) : new Color(0.68f, 0.72f, 0.78f, 0.78f);
        SetRect(labelText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(258f, 58f));

        if (!available)
        {
            Text unavailable = CreateText("Unavailable", rect, "Bientot", 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            unavailable.color = new Color(0.93f, 0.82f, 0.46f, 0.82f);
            SetRect(unavailable.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-58f, -24f), new Vector2(96f, 30f));
        }

        HandicapCard card = new HandicapCard
        {
            label = label,
            available = available,
            button = cardButton,
            rectTransform = rect,
            border = border,
            canvasGroup = canvasGroup,
            basePosition = position
        };
        handicapCards.Add(card);

        if (available)
        {
            cardButton.onClick.AddListener(StartSimulation);
            testerButton = cardButton;
        }
    }

    private void ShowCarousel()
    {
        if (carouselRoot != null)
        {
            carouselRoot.gameObject.SetActive(true);
        }

        if (selectionRoot != null)
        {
            selectionRoot.gameObject.SetActive(false);
        }

        PositionCanvasInFrontOfHeadset(introCanvas.transform as RectTransform);
        UpdateCarouselTexts();
    }

    private void ShowHandicapSelection()
    {
        currentState = MenuState.HandicapSelection;
        activationInputLockedUntil = Time.unscaledTime + 0.35f;
        waitingForSelectionInputRelease = true;

        if (carouselRoot != null)
        {
            carouselRoot.gameObject.SetActive(false);
        }

        if (selectionRoot != null)
        {
            selectionRoot.gameObject.SetActive(true);
        }

        UpdateSelectedHandicap(0);
        UpdateSelectionCardTransforms(1f);
        PositionCanvasInFrontOfHeadset(introCanvas.transform as RectTransform);
    }

    private void UpdateCarousel()
    {
        float blend = 1f - Mathf.Exp(-carouselSmoothness * Time.deltaTime);
        carouselVisualIndex = Mathf.Lerp(carouselVisualIndex, currentCarouselIndex, blend);
        UpdateCarouselCardTransforms();

        carouselTimer += Time.deltaTime;
        if (carouselTimer < carouselSlideDuration)
        {
            return;
        }

        carouselTimer = 0f;
        currentCarouselIndex++;
        UpdateCarouselTexts();
    }

    private void UpdateCarouselTexts()
    {
        int displayIndex = GetCarouselDisplayIndex();
        if (carouselTitleText != null)
        {
            carouselTitleText.text = carouselTitles[displayIndex];
        }

        if (carouselBodyText != null)
        {
            carouselBodyText.text = carouselBodies[displayIndex];
        }

        if (carouselProgressText != null)
        {
            carouselProgressText.text = (displayIndex + 1) + " / " + carouselTitles.Length + " - Espace, Entree, Esc ou gachette pour passer";
        }

        UpdateCarouselCardTransforms();
    }

    private int GetCarouselDisplayIndex()
    {
        if (carouselTitles.Length == 0)
        {
            return 0;
        }

        return ((currentCarouselIndex % carouselTitles.Length) + carouselTitles.Length) % carouselTitles.Length;
    }

    private void UpdateCarouselCardTransforms()
    {
        int cardCount = carouselCards.Count;
        if (cardCount == 0)
        {
            return;
        }

        for (int i = 0; i < carouselCards.Count; i++)
        {
            RectTransform card = carouselCards[i];
            float offset = Mathf.Repeat(i - carouselVisualIndex + cardCount * 0.5f, cardCount) - cardCount * 0.5f;
            float distance = Mathf.Abs(offset);
            float normalizedDistance = Mathf.Clamp01(distance / Mathf.Max(1f, cardCount * 0.5f));
            float angle = offset * 58f;
            float radians = angle * Mathf.Deg2Rad;
            float bob = Mathf.Sin(Time.time * 1.45f + i * 0.8f) * Mathf.Lerp(5f, 2f, normalizedDistance);

            Vector3 targetPosition = new Vector3(
                Mathf.Sin(radians) * carouselRadius,
                -distance * 18f + bob,
                -distance * carouselDepth);

            Quaternion targetRotation = Quaternion.Euler(0f, -angle * 0.86f, offset * -2.5f);
            float targetScale = Mathf.Lerp(1.1f, 0.76f, normalizedDistance);

            card.anchoredPosition3D = targetPosition;
            card.localRotation = targetRotation;
            card.localScale = Vector3.one * targetScale;

            if (i < carouselCardGroups.Count && carouselCardGroups[i] != null)
            {
                carouselCardGroups[i].alpha = Mathf.Lerp(1f, 0.42f, normalizedDistance);
                carouselCardGroups[i].interactable = distance < 0.45f;
                carouselCardGroups[i].blocksRaycasts = distance < 0.45f;
            }

            if (distance < 0.18f)
            {
                card.SetAsLastSibling();
            }
        }
    }

    private void UpdateHandicapSelectionInput()
    {
        int direction = ReadHorizontalSelectionDirection();
        if (direction != 0)
        {
            int nextIndex = Mathf.Clamp(selectedHandicapIndex + direction, 0, handicapCards.Count - 1);
            UpdateSelectedHandicap(nextIndex);
        }

        bool activationPressed = IsKeyboardStartPressed() || IsControllerStartPressed() || IsTesterClickedWithMouse();
        if (waitingForSelectionInputRelease)
        {
            if (!activationPressed)
            {
                waitingForSelectionInputRelease = false;
            }

            return;
        }

        bool activationAllowed = Time.unscaledTime >= activationInputLockedUntil;
        if (activationAllowed
            && activationPressed
            && selectedHandicapIndex >= 0
            && selectedHandicapIndex < handicapCards.Count
            && handicapCards[selectedHandicapIndex].available)
        {
            StartSimulation();
        }
    }

    private int ReadHorizontalSelectionDirection()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame || keyboard.qKey.wasPressedThisFrame)
            {
                return -1;
            }

            if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                return 1;
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.Q))
        {
            return -1;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            return 1;
        }
#endif
        return 0;
    }

    private void UpdateSelectedHandicap(int index)
    {
        if (handicapCards.Count == 0)
        {
            selectedHandicapIndex = 0;
            return;
        }

        selectedHandicapIndex = Mathf.Clamp(index, 0, handicapCards.Count - 1);
        for (int i = 0; i < handicapCards.Count; i++)
        {
            bool selected = i == selectedHandicapIndex;
            HandicapCard card = handicapCards[i];
            if (card.border != null)
            {
                if (selected && card.available)
                {
                    card.border.color = new Color(0.86f, 0.92f, 1f, 0.22f);
                }
                else if (selected)
                {
                    card.border.color = new Color(0.86f, 0.82f, 0.72f, 0.16f);
                }
                else
                {
                    card.border.color = card.available
                        ? new Color(1f, 1f, 1f, 0.12f)
                        : new Color(1f, 1f, 1f, 0.07f);
                }
            }

            if (selected && card.rectTransform != null)
            {
                card.rectTransform.SetAsLastSibling();
            }
        }
    }

    private void UpdateSelectionCardTransforms(float deltaTime)
    {
        if (handicapCards.Count == 0)
        {
            return;
        }

        float blend = 1f - Mathf.Exp(-menuSmoothness * deltaTime);
        for (int i = 0; i < handicapCards.Count; i++)
        {
            HandicapCard card = handicapCards[i];
            if (card.rectTransform == null)
            {
                continue;
            }

            bool selected = i == selectedHandicapIndex;
            float horizontalOffset = Mathf.Clamp((card.basePosition.x - handicapCards[selectedHandicapIndex].basePosition.x) / 380f, -1.25f, 1.25f);
            float bob = Mathf.Sin(Time.time * 1.25f + i * 0.73f) * (selected ? 7f : 3f);
            Vector3 targetPosition = new Vector3(
                card.basePosition.x,
                card.basePosition.y + bob,
                selected ? -46f : -Mathf.Abs(horizontalOffset) * 22f);

            Quaternion targetRotation = selected
                ? Quaternion.Euler(0f, 0f, 0f)
                : Quaternion.Euler(0f, -horizontalOffset * 12f, horizontalOffset * -1.8f);

            float targetScale = selected ? 1.075f : card.available ? 0.96f : 0.9f;
            card.rectTransform.anchoredPosition3D = Vector3.Lerp(card.rectTransform.anchoredPosition3D, targetPosition, blend);
            card.rectTransform.localRotation = Quaternion.Slerp(card.rectTransform.localRotation, targetRotation, blend);
            card.rectTransform.localScale = Vector3.Lerp(card.rectTransform.localScale, Vector3.one * targetScale, blend);

            if (card.canvasGroup != null)
            {
                float targetAlpha = selected ? 1f : card.available ? 0.86f : 0.58f;
                card.canvasGroup.alpha = Mathf.Lerp(card.canvasGroup.alpha, targetAlpha, blend);
            }
        }
    }

    private bool IsSkipPressed()
    {
        return IsKeyboardStartPressed() || IsControllerStartPressed() || IsTesterClickedWithMouse();
    }

    private void SetSimulationObjectsActive(bool active)
    {
        if (simulationObjects == null)
        {
            return;
        }

        for (int i = 0; i < simulationObjects.Length; i++)
        {
            if (simulationObjects[i] != null)
            {
                simulationObjects[i].SetActive(active);
            }
        }
    }

    private void RecenterSimulationContent()
    {
        Transform anchor = ResolveHeadsetAnchor();
        if (anchor == null)
        {
            return;
        }

        Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }

        Vector3 basePosition = anchor.position;
        Transform infoText = ResolveNamedTransform(worldInfoText, "Glaucoma_Info_Text");
        if (infoText != null)
        {
            infoText.position = basePosition + forward * infoTextDistance + Vector3.up * -0.1f;
            infoText.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        Transform targetsRoot = ResolveNamedTransform(awarenessTargetsRoot, "Peripheral_Awareness_Targets");
        if (targetsRoot != null)
        {
            targetsRoot.position = basePosition + forward * targetsDistance + Vector3.up * -1.35f;
            targetsRoot.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
    }

    private Transform ResolveHeadsetAnchor()
    {
        if (headsetAnchor != null)
        {
            return headsetAnchor;
        }

        if (simulationController != null && simulationController.headsetAnchor != null)
        {
            headsetAnchor = simulationController.headsetAnchor;
            return headsetAnchor;
        }

        if (Camera.main != null)
        {
            headsetAnchor = Camera.main.transform;
            return headsetAnchor;
        }

        return null;
    }

    private static Transform ResolveNamedTransform(Transform assignedTransform, string objectName)
    {
        if (assignedTransform != null)
        {
            return assignedTransform;
        }

        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }

    private bool IsControllerStartPressed()
    {
        if (!allowControllerStart)
        {
            return false;
        }

#if UNITY_EDITOR
        return IsInputSystemGamepadStartPressed();
#else
        return IsMetaStartPressed()
            || IsInputSystemGamepadStartPressed()
            || IsXRControllerStartPressed();
#endif
    }

    private bool IsMetaStartPressed()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        bool buttonPressed = OVRInput.GetDown(OVRInput.Button.One)
            || OVRInput.GetDown(OVRInput.Button.Two)
            || OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger)
            || OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);

        bool triggerPressed = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) >= triggerStartThreshold
            || OVRInput.Get(OVRInput.Axis1D.SecondaryIndexTrigger) >= triggerStartThreshold;

        bool triggerJustPressed = triggerPressed && !triggerWasPressed;
        triggerWasPressed = triggerPressed;

        return buttonPressed || triggerJustPressed;
#else
        return false;
#endif
    }

    private static bool IsInputSystemGamepadStartPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Gamepad gamepad = Gamepad.current;
        return gamepad != null
            && (gamepad.buttonSouth.wasPressedThisFrame
                || gamepad.buttonEast.wasPressedThisFrame
                || gamepad.buttonWest.wasPressedThisFrame
                || gamepad.buttonNorth.wasPressedThisFrame
                || gamepad.leftTrigger.wasPressedThisFrame
                || gamepad.rightTrigger.wasPressedThisFrame
                || gamepad.startButton.wasPressedThisFrame);
#else
        return false;
#endif
    }

    private bool IsXRControllerStartPressed()
    {
        XRInputDevices.GetDevicesWithCharacteristics(XRInputDeviceCharacteristics.Controller, xrDevices);

        for (int i = 0; i < xrDevices.Count; i++)
        {
            XRInputDevice device = xrDevices[i];
            if (!device.isValid)
            {
                continue;
            }

            if (IsXRButtonPressed(device, XRCommonUsages.primaryButton)
                || IsXRButtonPressed(device, XRCommonUsages.secondaryButton)
                || IsXRButtonPressed(device, XRCommonUsages.triggerButton)
                || IsXRButtonPressed(device, XRCommonUsages.gripButton)
                || IsXRButtonPressed(device, XRCommonUsages.menuButton))
            {
                return true;
            }

            if (device.TryGetFeatureValue(XRCommonUsages.trigger, out float trigger)
                && trigger >= triggerStartThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsXRButtonPressed(XRInputDevice device, XRInputFeatureUsageBool usage)
    {
        return device.TryGetFeatureValue(usage, out bool pressed) && pressed;
    }

    private bool IsKeyboardStartPressed()
    {
        if (!allowKeyboardStart)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null)
        {
            return false;
        }

        return Keyboard.current.spaceKey.wasPressedThisFrame
            || Keyboard.current.enterKey.wasPressedThisFrame
            || Keyboard.current.numpadEnterKey.wasPressedThisFrame
            || Keyboard.current.escapeKey.wasPressedThisFrame
            || IsConfiguredSkipKeyPressed(Keyboard.current);
#else
        return Input.GetKeyDown(keyboardStartKey)
            || Input.GetKeyDown(KeyCode.Return)
            || Input.GetKeyDown(KeyCode.Escape)
            || Input.GetKeyDown(keyboardSkipKey);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private bool IsConfiguredSkipKeyPressed(Keyboard keyboard)
    {
        if (keyboard == null)
        {
            return false;
        }

        switch (keyboardSkipKey)
        {
            case KeyCode.Escape:
                return keyboard.escapeKey.wasPressedThisFrame;
            case KeyCode.Return:
                return keyboard.enterKey.wasPressedThisFrame;
            default:
                return keyboard.spaceKey.wasPressedThisFrame;
        }
    }
#endif

    private bool IsTesterClickedWithMouse()
    {
        if (testerButton == null || introCanvas == null)
        {
            return false;
        }

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
        {
            return false;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
#else
        if (!Input.GetMouseButtonDown(0))
        {
            return false;
        }

        Vector2 mousePosition = Input.mousePosition;
#endif

        RectTransform buttonTransform = testerButton.transform as RectTransform;
        return buttonTransform != null
            && RectTransformUtility.RectangleContainsScreenPoint(buttonTransform, mousePosition, introCanvas.worldCamera);
    }

    private RectTransform CreateRect(string objectName, RectTransform parent)
    {
        GameObject child = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        RectTransform rectTransform = child.transform as RectTransform;
        rectTransform.SetParent(parent, false);
        return rectTransform;
    }

    private Text CreateText(string objectName, RectTransform parent, string content, int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        RectTransform rectTransform = CreateRect(objectName, parent);
        Text text = rectTransform.gameObject.AddComponent<Text>();
        text.text = content;
        text.font = defaultFont;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private Image CreateImage(string objectName, RectTransform parent, Sprite sprite)
    {
        RectTransform rectTransform = CreateRect(objectName, parent);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        return image;
    }

    private Button CreateButton(string objectName, RectTransform parent, string label, Color backgroundColor)
    {
        RectTransform rectTransform = CreateRect(objectName, parent);
        RoundedRectGraphic background = rectTransform.gameObject.AddComponent<RoundedRectGraphic>();
        background.cornerRadius = 28f;
        background.cornerSegments = 12;
        background.color = backgroundColor;
        Button button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        if (!string.IsNullOrEmpty(label))
        {
            Text text = CreateText("Text", rectTransform, label, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);
        }

        return button;
    }

    private void AddBackgroundDepth(RectTransform parent)
    {
        Image topBand = CreateDecorImage("Ambient_Top_Band", parent, new Color(1f, 1f, 1f, 0.035f));
        SetStretch(topBand.rectTransform, 0f, 620f, 0f, 0f);

        Image lowerBand = CreateDecorImage("Ambient_Lower_Band", parent, new Color(0f, 0f, 0f, 0.08f));
        SetStretch(lowerBand.rectTransform, 0f, 0f, 0f, 650f);

        Image horizon = CreateDecorImage("Ambient_Horizon_Line", parent, new Color(1f, 1f, 1f, 0.035f));
        SetRect(horizon.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -48f), new Vector2(1040f, 3f));
    }

    private void AddVisionGlassWindowDecor(RectTransform parent)
    {
        Image shadow = CreateDecorImage("Window_Depth_Shadow", parent, new Color(0f, 0f, 0f, 0.08f));
        SetStretch(shadow.rectTransform, 24f, -24f, 24f, 24f);
        shadow.rectTransform.SetAsFirstSibling();

        RoundedRectGraphic innerLight = CreateRoundedDecor("Window_Inner_Light", parent, new Color(1f, 1f, 1f, 0.055f), 30f);
        SetStretch(innerLight.rectTransform, 18f, 18f, 18f, 18f);

        RoundedRectGraphic topSheen = CreateRoundedDecor("Window_Top_Sheen", parent, new Color(1f, 1f, 1f, 0.09f), 26f);
        topSheen.rectTransform.anchorMin = new Vector2(0f, 1f);
        topSheen.rectTransform.anchorMax = new Vector2(1f, 1f);
        topSheen.rectTransform.pivot = new Vector2(0.5f, 1f);
        topSheen.rectTransform.offsetMin = new Vector2(34f, -96f);
        topSheen.rectTransform.offsetMax = new Vector2(-34f, -22f);

        Image hairline = CreateDecorImage("Window_Bottom_Hairline", parent, new Color(1f, 1f, 1f, 0.11f));
        hairline.rectTransform.anchorMin = new Vector2(0.18f, 0f);
        hairline.rectTransform.anchorMax = new Vector2(0.82f, 0f);
        hairline.rectTransform.pivot = new Vector2(0.5f, 0f);
        hairline.rectTransform.anchoredPosition = new Vector2(0f, 18f);
        hairline.rectTransform.sizeDelta = new Vector2(0f, 2f);
    }

    private void AddVisionCardDecor(RectTransform parent, Color accent, bool strong)
    {
        float opacity = strong ? 1f : 0.68f;

        RoundedRectGraphic shadow = CreateRoundedDecor("Depth_Shadow", parent, new Color(0f, 0f, 0f, 0.055f * opacity), 30f);
        SetStretch(shadow.rectTransform, 14f, -18f, 14f, 18f);
        shadow.rectTransform.SetAsFirstSibling();

        RoundedRectGraphic innerGlow = CreateRoundedDecor("Inner_Glass_Glow", parent, new Color(1f, 1f, 1f, 0.055f * opacity), 24f);
        SetStretch(innerGlow.rectTransform, 7f, 7f, 7f, 7f);

        RoundedRectGraphic glassRim = CreateRoundedDecor("Outer_Glass_Rim", parent, new Color(1f, 1f, 1f, 0.035f * opacity), 28f);
        SetStretch(glassRim.rectTransform, 2f, 2f, 2f, 2f);

        RoundedRectGraphic topSheen = CreateRoundedDecor("Top_Glass_Highlight", parent, new Color(1f, 1f, 1f, 0.105f * opacity), 20f);
        topSheen.rectTransform.anchorMin = new Vector2(0f, 1f);
        topSheen.rectTransform.anchorMax = new Vector2(1f, 1f);
        topSheen.rectTransform.pivot = new Vector2(0.5f, 1f);
        topSheen.rectTransform.offsetMin = new Vector2(16f, -46f);
        topSheen.rectTransform.offsetMax = new Vector2(-16f, -12f);

        RoundedRectGraphic floatingHighlight = CreateRoundedDecor("Floating_Top_Highlight", parent, new Color(1f, 1f, 1f, 0.18f * opacity), 4f);
        floatingHighlight.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        floatingHighlight.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        floatingHighlight.rectTransform.pivot = new Vector2(0.5f, 1f);
        floatingHighlight.rectTransform.anchoredPosition = new Vector2(0f, -10f);
        floatingHighlight.rectTransform.sizeDelta = new Vector2(86f, 5f);

        RoundedRectGraphic leftGlint = CreateRoundedDecor("Left_Edge_Glint", parent, new Color(1f, 1f, 1f, 0.09f * opacity), 4f);
        leftGlint.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        leftGlint.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        leftGlint.rectTransform.pivot = new Vector2(0f, 0.5f);
        leftGlint.rectTransform.anchoredPosition = new Vector2(8f, 6f);
        leftGlint.rectTransform.sizeDelta = new Vector2(4f, 72f);

        RoundedRectGraphic rightGlint = CreateRoundedDecor("Right_Edge_Glint", parent, new Color(accent.r, accent.g, accent.b, 0.12f * opacity), 4f);
        rightGlint.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        rightGlint.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rightGlint.rectTransform.pivot = new Vector2(1f, 0.5f);
        rightGlint.rectTransform.anchoredPosition = new Vector2(-8f, -8f);
        rightGlint.rectTransform.sizeDelta = new Vector2(4f, 58f);

        RoundedRectGraphic accentGlow = CreateRoundedDecor("Soft_Accent_Glow", parent, new Color(accent.r, accent.g, accent.b, 0.12f * opacity), 20f);
        accentGlow.rectTransform.anchorMin = new Vector2(0.08f, 0f);
        accentGlow.rectTransform.anchorMax = new Vector2(0.92f, 0f);
        accentGlow.rectTransform.pivot = new Vector2(0.5f, 0f);
        accentGlow.rectTransform.anchoredPosition = new Vector2(0f, 12f);
        accentGlow.rectTransform.sizeDelta = new Vector2(0f, 18f);

        RoundedRectGraphic accentPill = CreateRoundedDecor("Accent_Light_Pill", parent, new Color(accent.r, accent.g, accent.b, 0.34f * opacity), 5f);
        accentPill.rectTransform.anchorMin = new Vector2(0.5f, 0f);
        accentPill.rectTransform.anchorMax = new Vector2(0.5f, 0f);
        accentPill.rectTransform.pivot = new Vector2(0.5f, 0f);
        accentPill.rectTransform.anchoredPosition = new Vector2(0f, 11f);
        accentPill.rectTransform.sizeDelta = new Vector2(82f, 5f);

        Image rim = CreateDecorImage("Bottom_Light_Rim", parent, new Color(1f, 1f, 1f, 0.13f * opacity));
        rim.rectTransform.anchorMin = new Vector2(0.18f, 0f);
        rim.rectTransform.anchorMax = new Vector2(0.82f, 0f);
        rim.rectTransform.pivot = new Vector2(0.5f, 0f);
        rim.rectTransform.anchoredPosition = new Vector2(0f, 8f);
        rim.rectTransform.sizeDelta = new Vector2(0f, 2f);
    }

    private void AddVisionButtonDecor(RectTransform parent)
    {
        RoundedRectGraphic inner = CreateRoundedDecor("Button_Inner_Glass", parent, new Color(1f, 1f, 1f, 0.085f), 24f);
        SetStretch(inner.rectTransform, 5f, 5f, 5f, 5f);

        RoundedRectGraphic sheen = CreateRoundedDecor("Button_Top_Sheen", parent, new Color(1f, 1f, 1f, 0.14f), 20f);
        sheen.rectTransform.anchorMin = new Vector2(0f, 1f);
        sheen.rectTransform.anchorMax = new Vector2(1f, 1f);
        sheen.rectTransform.pivot = new Vector2(0.5f, 1f);
        sheen.rectTransform.offsetMin = new Vector2(14f, -24f);
        sheen.rectTransform.offsetMax = new Vector2(-14f, -8f);

        Image rim = CreateDecorImage("Button_Bottom_Rim", parent, new Color(1f, 1f, 1f, 0.16f));
        rim.rectTransform.anchorMin = new Vector2(0.24f, 0f);
        rim.rectTransform.anchorMax = new Vector2(0.76f, 0f);
        rim.rectTransform.pivot = new Vector2(0.5f, 0f);
        rim.rectTransform.anchoredPosition = new Vector2(0f, 7f);
        rim.rectTransform.sizeDelta = new Vector2(0f, 2f);
    }

    private static Color GetSlideAccent(int index)
    {
        switch (index)
        {
            case 0:
                return new Color(0.18f, 0.68f, 0.95f, 1f);
            case 1:
                return new Color(0.55f, 0.88f, 0.48f, 1f);
            default:
                return new Color(1f, 0.68f, 0.24f, 1f);
        }
    }

    private static Color GetHandicapAccent(IconStyle style)
    {
        switch (style)
        {
            case IconStyle.Glaucoma:
                return new Color(0.18f, 0.68f, 0.95f, 1f);
            case IconStyle.Daltonism:
                return new Color(0.9f, 0.45f, 0.22f, 1f);
            case IconStyle.Nystagmus:
                return new Color(0.55f, 0.72f, 1f, 1f);
            case IconStyle.DiabeticRetinopathy:
                return new Color(0.95f, 0.28f, 0.24f, 1f);
            default:
                return new Color(0.85f, 0.72f, 0.28f, 1f);
        }
    }

    private Image CreateDecorImage(string objectName, RectTransform parent, Color color)
    {
        RectTransform rectTransform = CreateRect(objectName, parent);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private RoundedRectGraphic CreateRoundedDecor(string objectName, RectTransform parent, Color color, float radius)
    {
        RectTransform rectTransform = CreateRect(objectName, parent);
        RoundedRectGraphic graphic = rectTransform.gameObject.AddComponent<RoundedRectGraphic>();
        graphic.cornerRadius = radius;
        graphic.cornerSegments = 10;
        graphic.color = color;
        graphic.raycastTarget = false;
        return graphic;
    }

    private RectTransform BuildSlideIcon(RectTransform parent, int index)
    {
        RectTransform root = CreateRect("Carousel_Icon", parent);
        RoundedRectGraphic background = root.gameObject.AddComponent<RoundedRectGraphic>();
        background.cornerRadius = 18f;
        background.cornerSegments = 10;
        background.color = index == 0
            ? new Color(0.18f, 0.48f, 0.72f, 0.32f)
            : index == 1 ? new Color(0.38f, 0.72f, 0.46f, 0.28f) : new Color(0.92f, 0.58f, 0.26f, 0.28f);

        Color accent = index == 0
            ? new Color(0.35f, 0.82f, 1f, 1f)
            : index == 1 ? new Color(0.72f, 0.95f, 0.62f, 1f) : new Color(1f, 0.76f, 0.34f, 1f);

        CreateIconBlock("Block_Left", root, new Vector2(-34f, 0f), new Vector2(46f, 46f), new Color(accent.r, accent.g, accent.b, 0.78f), 0f);
        CreateIconBlock("Block_Right", root, new Vector2(34f, 0f), new Vector2(46f, 46f), new Color(1f, 1f, 1f, 0.32f), 0f);
        CreateIconBlock("Block_Link", root, Vector2.zero, new Vector2(72f, 7f), new Color(1f, 1f, 1f, 0.78f), 0f);
        return root;
    }

    private RectTransform BuildHandicapIcon(RectTransform parent, IconStyle style, bool available)
    {
        RectTransform root = CreateRect("Icon", parent);
        RoundedRectGraphic background = root.gameObject.AddComponent<RoundedRectGraphic>();
        background.cornerRadius = 18f;
        background.cornerSegments = 10;
        background.color = available ? new Color(0.03f, 0.04f, 0.055f, 0.46f) : new Color(0.03f, 0.035f, 0.045f, 0.28f);

        float alpha = available ? 1f : 0.46f;
        switch (style)
        {
            case IconStyle.Glaucoma:
                CreateIconBlock("Dark_Field", root, Vector2.zero, new Vector2(160f, 92f), new Color(0f, 0f, 0f, 0.92f * alpha), 0f);
                CreateIconBlock("Central_Vision", root, Vector2.zero, new Vector2(72f, 52f), new Color(0.2f, 0.72f, 1f, alpha), 0f);
                CreateIconBlock("Pupil", root, Vector2.zero, new Vector2(24f, 24f), new Color(0.02f, 0.03f, 0.04f, alpha), 0f);
                break;
            case IconStyle.Daltonism:
                CreateIconBlock("Red", root, new Vector2(-52f, 0f), new Vector2(66f, 76f), new Color(0.9f, 0.18f, 0.16f, 0.86f * alpha), 0f);
                CreateIconBlock("Green", root, Vector2.zero, new Vector2(66f, 76f), new Color(0.12f, 0.65f, 0.25f, 0.86f * alpha), 0f);
                CreateIconBlock("Blue", root, new Vector2(52f, 0f), new Vector2(66f, 76f), new Color(0.18f, 0.34f, 0.95f, 0.86f * alpha), 0f);
                break;
            case IconStyle.Nystagmus:
                CreateEyeBlocks(root, alpha, new Color(0.16f, 0.42f, 0.75f, alpha));
                CreateIconBlock("Motion_One", root, Vector2.zero, new Vector2(190f, 7f), new Color(1f, 1f, 1f, 0.7f * alpha), -22f);
                CreateIconBlock("Motion_Two", root, Vector2.zero, new Vector2(190f, 7f), new Color(1f, 1f, 1f, 0.45f * alpha), 22f);
                break;
            case IconStyle.DiabeticRetinopathy:
                CreateEyeBlocks(root, alpha, new Color(0.45f, 0.12f, 0.08f, alpha));
                CreateIconBlock("Spot_Red_A", root, new Vector2(-48f, 28f), new Vector2(24f, 24f), new Color(0.82f, 0.04f, 0.02f, alpha), 0f);
                CreateIconBlock("Spot_Red_B", root, new Vector2(55f, -24f), new Vector2(30f, 30f), new Color(0.82f, 0.04f, 0.02f, alpha), 0f);
                CreateIconBlock("Spot_Yellow", root, new Vector2(78f, 24f), new Vector2(18f, 18f), new Color(0.95f, 0.74f, 0.24f, alpha), 0f);
                break;
            case IconStyle.Visuospatial:
                for (int i = -2; i <= 2; i++)
                {
                    CreateIconBlock("Grid_V_" + i, root, new Vector2(i * 34f, 0f), new Vector2(3f, 92f), new Color(0.72f, 0.78f, 0.86f, 0.42f * alpha), -8f);
                    CreateIconBlock("Grid_H_" + i, root, new Vector2(0f, i * 20f), new Vector2(188f, 3f), new Color(0.72f, 0.78f, 0.86f, 0.42f * alpha), -8f);
                }

                CreateIconBlock("Diagonal", root, Vector2.zero, new Vector2(150f, 8f), new Color(0.94f, 0.78f, 0.22f, alpha), -28f);
                CreateIconBlock("Anchor_A", root, new Vector2(-64f, 30f), new Vector2(34f, 34f), new Color(0.28f, 0.76f, 0.86f, alpha), 0f);
                CreateIconBlock("Anchor_B", root, new Vector2(70f, -32f), new Vector2(38f, 38f), new Color(0.9f, 0.28f, 0.46f, alpha), 0f);
                break;
        }

        return root;
    }

    private bool TryGetRealHandicapImage(IconStyle iconStyle, out Sprite sprite)
    {
        if (realImageCache.TryGetValue(iconStyle, out sprite))
        {
            return sprite != null;
        }

        string resourcePath = GetRealImageResourcePath(iconStyle);
        if (string.IsNullOrEmpty(resourcePath))
        {
            realImageCache[iconStyle] = null;
            return false;
        }

        sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        realImageCache[iconStyle] = sprite;
        return sprite != null;
    }

    private static string GetRealImageResourcePath(IconStyle iconStyle)
    {
        switch (iconStyle)
        {
            case IconStyle.Glaucoma:
                return "MenuImages/glaucoma_real";
            case IconStyle.Daltonism:
                return "MenuImages/daltonism_real";
            case IconStyle.Nystagmus:
                return "MenuImages/nystagmus_real";
            case IconStyle.DiabeticRetinopathy:
                return "MenuImages/diabetic_retinopathy_real";
            case IconStyle.Visuospatial:
                return "MenuImages/visuospatial_real";
            default:
                return null;
        }
    }

    private RectTransform BuildRealImageIcon(RectTransform parent, Sprite sprite, bool available)
    {
        RectTransform root = CreateRect("Real_Handicap_Image", parent);
        RoundedRectGraphic background = root.gameObject.AddComponent<RoundedRectGraphic>();
        background.cornerRadius = 18f;
        background.cornerSegments = 10;
        background.color = available ? new Color(0.032f, 0.032f, 0.036f, 0.5f) : new Color(0.032f, 0.032f, 0.036f, 0.28f);

        Mask mask = root.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        Image photo = CreateImage("Photo", root, sprite);
        photo.color = available ? Color.white : new Color(0.62f, 0.66f, 0.7f, 0.72f);
        photo.preserveAspect = true;
        Stretch(photo.rectTransform);

        RoundedRectGraphic glass = CreateRoundedDecor("Photo_Glass_Sheen", root, new Color(1f, 1f, 1f, 0.08f), 16f);
        glass.rectTransform.anchorMin = new Vector2(0f, 1f);
        glass.rectTransform.anchorMax = new Vector2(1f, 1f);
        glass.rectTransform.pivot = new Vector2(0.5f, 1f);
        glass.rectTransform.offsetMin = new Vector2(8f, -28f);
        glass.rectTransform.offsetMax = new Vector2(-8f, -6f);

        Image rim = CreateDecorImage("Photo_Rim", root, new Color(1f, 1f, 1f, 0.11f));
        rim.rectTransform.anchorMin = new Vector2(0.18f, 0f);
        rim.rectTransform.anchorMax = new Vector2(0.82f, 0f);
        rim.rectTransform.pivot = new Vector2(0.5f, 0f);
        rim.rectTransform.anchoredPosition = new Vector2(0f, 6f);
        rim.rectTransform.sizeDelta = new Vector2(0f, 2f);

        return root;
    }

    private void CreateEyeBlocks(RectTransform parent, float alpha, Color irisColor)
    {
        CreateIconBlock("Eye_White", parent, Vector2.zero, new Vector2(178f, 76f), new Color(0.82f, 0.88f, 0.93f, alpha), 0f);
        CreateIconBlock("Iris", parent, Vector2.zero, new Vector2(58f, 58f), irisColor, 0f);
        CreateIconBlock("Pupil", parent, Vector2.zero, new Vector2(24f, 24f), new Color(0.02f, 0.025f, 0.03f, alpha), 0f);
    }

    private Image CreateIconBlock(string objectName, RectTransform parent, Vector2 position, Vector2 size, Color color, float rotationZ)
    {
        RectTransform rectTransform = CreateRect(objectName, parent);
        SetRect(rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.anchoredPosition3D = Vector3.zero;
        rectTransform.localScale = Vector3.one;
    }

    private static void SetStretch(RectTransform rectTransform, float left, float bottom, float right, float top)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
        rectTransform.localScale = Vector3.one;
    }

    private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 size)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;
        rectTransform.localScale = Vector3.one;
    }

}

[DisallowMultipleComponent]
public sealed class RoundedRectGraphic : MaskableGraphic
{
    [Min(0f)] public float cornerRadius = 18f;
    [Range(2, 16)] public int cornerSegments = 8;

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();

        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(cornerRadius, rect.width * 0.5f, rect.height * 0.5f);
        int segments = Mathf.Max(2, cornerSegments);

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = color;
        vertex.position = rect.center;
        vertexHelper.AddVert(vertex);

        List<Vector2> points = new List<Vector2>((segments + 1) * 4);
        AddCorner(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, segments);
        AddCorner(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, segments);
        AddCorner(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, segments);
        AddCorner(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, segments);

        for (int i = 0; i < points.Count; i++)
        {
            vertex.position = points[i];
            vertexHelper.AddVert(vertex);
        }

        for (int i = 1; i <= points.Count; i++)
        {
            int next = i == points.Count ? 1 : i + 1;
            vertexHelper.AddTriangle(0, i, next);
        }
    }

    private static void AddCorner(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            float angle = Mathf.Lerp(startAngle, endAngle, i / (float)segments) * Mathf.Deg2Rad;
            points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
        }
    }
}
