using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GlobalPathologyMenu : MonoBehaviour
{
    enum MenuState { Intro, Selection }

    sealed class Slide
    {
        public string number;
        public string title;
        public string body;
    }

    sealed class Card
    {
        public string index;
        public string title;
        public string subject;
        public string sceneName;
        public bool unlocked;
        public RectTransform rect;
        public Image panel;
        public Image leftBar;
        public Image dot;
        public Text indexText;
        public Text titleText;
        public Text subjectText;
        public Text statusText;
    }

    [Header("Menu global")]
    public Transform headAnchor;
    public float distance = 3.5f;
    public float verticalOffset = -0.05f;
    public string glaucomaSceneName = "Sim_Glaucome";
    public string nystagmusSceneName = "Sim_Nystagmus";
    public string photophobiaSceneName = "Sim_Photophobie";
    public string dyslexiaSceneName = "Sim_Dyslexie";

    [Header("Contrôles manettes")]
    public OVRInput.Axis2D navigationAxis = OVRInput.Axis2D.SecondaryThumbstick;
    public float navigationDeadZone = 0.45f;
    public bool hapticFeedback = true;
    [Range(0f, 1f)] public float hapticAmplitude = 0.30f;
    public float hapticDuration = 0.05f;

    [Header("Palette AR clinique")]
    public Color paper = new Color(0.055f, 0.062f, 0.066f, 0.96f);
    public Color ink   = new Color(0.925f, 0.918f, 0.890f, 1f);
    public Color soft  = new Color(0.610f, 0.650f, 0.660f, 1f);
    public Color red   = new Color(0.280f, 0.620f, 0.720f, 1f);

    Canvas _canvas;
    RectTransform _root;
    RectTransform _introRoot;
    RectTransform _selectionRoot;
    Text _slideTitle;
    Text _slideBody;
    Text _slideCounter;
    Text _slideHint;
    Image _redDotIntro;
    Text _selectionTitle;
    Text _selectionHint;
    Text _selectionCounter;

    Font _sans;
    Font _sansBold;
    readonly List<Card> _cards = new List<Card>();
    MenuState _state = MenuState.Intro;
    int _slideIndex;
    int _selectedRow;
    int _selectedCol;
    bool _axisLocked;
    bool _confirmAxisHeld;
    bool _confirmReleased;
    float _time;
    float _hapticStopAt = -1f;

    static readonly Slide[] _slides =
    {
        new Slide { number = "01", title = "VISION\nEN AR.",        body = "Un laboratoire calme pour ressentir des pathologies visuelles en réalité augmentée." },
        new Slide { number = "02", title = "VOIR\nAUTREMENT.",      body = "Chaque module agit sur la scène réelle vue dans le casque, pas dans un décor VR." },
        new Slide { number = "03", title = "SIMULER\nAVEC MESURE.", body = "Contraste, champ visuel, oscillopsie, halos et repères spatiaux sont ajustables." },
        new Slide { number = "04", title = "CHOISIR\nET TESTER.",   body = "Joystick : naviguer. Gâchette : valider. B : retour." }
    };

    void Awake()
    {
        ApplyClinicalPalette();
        LoadFonts();
        ResolveHead();
        BuildMenu();
        ShowIntro();
    }

    void ApplyClinicalPalette()
    {
        distance = 3.50f;
        verticalOffset = -0.02f;
        paper = new Color(0.055f, 0.062f, 0.066f, 0.96f);
        ink = new Color(0.925f, 0.918f, 0.890f, 1f);
        soft = new Color(0.610f, 0.650f, 0.660f, 1f);
        red = new Color(0.280f, 0.620f, 0.720f, 1f);
    }

    void Start() => AttachToHead();

    void Update()
    {
        _time += Time.deltaTime;
        if (_state == MenuState.Intro) UpdateIntroInput();
        else UpdateSelectionInput();
        if (_redDotIntro != null)
        {
            float a = 0.85f + Mathf.Sin(_time * 2.4f) * 0.15f;
            Color c = red; c.a = a; _redDotIntro.color = c;
        }
        HapticTick();
    }

    void LoadFonts()
    {
        _sans = Font.CreateDynamicFontFromOSFont(
            new[] { "Helvetica Neue", "Helvetica", "Akzidenz-Grotesk Pro", "Univers", "Arial", "Roboto", "Noto Sans" }, 32);
        _sansBold = _sans;
        if (_sans == null) _sans = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_sansBold == null) _sansBold = _sans;
    }

    // ----------------- INTRO -----------------

    void ShowIntro()
    {
        _state = MenuState.Intro;
        _slideIndex = 0;
        _confirmReleased = false;
        _confirmAxisHeld = true;
        _introRoot.gameObject.SetActive(true);
        _selectionRoot.gameObject.SetActive(false);
        RefreshIntro();
    }

    void ShowSelection()
    {
        _state = MenuState.Selection;
        _confirmReleased = false;
        _confirmAxisHeld = true;
        _introRoot.gameObject.SetActive(false);
        _selectionRoot.gameObject.SetActive(true);
        _selectedRow = 0;
        _selectedCol = 0;
        _axisLocked = true;
        RefreshSelection();
    }

    void UpdateIntroInput()
    {
        if (ConfirmPressed())
        {
            Pulse();
            if (_slideIndex < _slides.Length - 1) { _slideIndex++; RefreshIntro(); }
            else ShowSelection();
        }
    }

    void RefreshIntro()
    {
        Slide s = _slides[_slideIndex];
        _slideTitle.text   = s.title;
        _slideBody.text    = s.body;
        _slideCounter.text = s.number + " / 04";
        _slideHint.text    = _slideIndex == _slides.Length - 1
            ? "GACHETTE  ->  OUVRIR LE CATALOGUE"
            : "GACHETTE  ->  SUIVANT";
    }

    // ----------------- SELECTION -----------------

    void UpdateSelectionInput()
    {
        Vector2 axis = CombinedNavigationAxis();
        bool inDead = Mathf.Abs(axis.x) < navigationDeadZone && Mathf.Abs(axis.y) < navigationDeadZone;
        if (inDead) _axisLocked = false;
        else if (!_axisLocked)
        {
            _axisLocked = true;
            int dx = 0, dy = 0;
            if (Mathf.Abs(axis.x) >= Mathf.Abs(axis.y)) dx = axis.x > 0f ? 1 : -1;
            else dy = axis.y > 0f ? -1 : 1;
            _selectedCol = PositiveModulo(_selectedCol + dx, 3);
            _selectedRow = PositiveModulo(_selectedRow + dy, 2);
            Pulse();
            RefreshSelection();
        }
        if (ConfirmPressed())
        {
            Card sel = SelectedCard();
            if (sel != null && sel.unlocked && !string.IsNullOrEmpty(sel.sceneName))
            {
                Pulse(0.55f, 0.10f);
                SceneManager.LoadScene(sel.sceneName);
            }
            else Pulse(0.12f, 0.04f);
        }
    }

    Card SelectedCard()
    {
        int i = _selectedRow * 3 + _selectedCol;
        return (i >= 0 && i < _cards.Count) ? _cards[i] : null;
    }

    void RefreshSelection()
    {
        Card sel = SelectedCard();
        for (int i = 0; i < _cards.Count; i++)
        {
            Card c = _cards[i];
            bool isSel = c == sel;
            // Selected: invert (black bg, white type, red dot). Unselected: paper bg, black type.
            if (isSel && c.unlocked)
            {
                c.panel.color = ink;
                c.titleText.color = paper;
                c.indexText.color = paper;
                c.subjectText.color = new Color(paper.r, paper.g, paper.b, 0.75f);
                c.statusText.color = red;
                c.statusText.text = "LANCER";
                c.leftBar.color = red;
                c.dot.color = red;
            }
            else if (c.unlocked)
            {
                c.panel.color = paper;
                c.titleText.color = ink;
                c.indexText.color = ink;
                c.subjectText.color = soft;
                c.statusText.color = ink;
                c.statusText.text = "ACTIF";
                c.leftBar.color = ink;
                c.dot.color = new Color(0, 0, 0, 0);
            }
            else
            {
                c.panel.color = paper;
                c.titleText.color = new Color(ink.r, ink.g, ink.b, 0.30f);
                c.indexText.color = new Color(ink.r, ink.g, ink.b, 0.30f);
                c.subjectText.color = new Color(ink.r, ink.g, ink.b, 0.25f);
                c.statusText.color = new Color(ink.r, ink.g, ink.b, 0.35f);
                c.statusText.text = "BIENTOT";
                c.leftBar.color = new Color(ink.r, ink.g, ink.b, 0.20f);
                c.dot.color = new Color(0, 0, 0, 0);
            }
        }
        _selectionCounter.text = "04 ACTIFS / 06";
    }

    // ----------------- BUILD -----------------

    void BuildMenu()
    {
        GameObject canvasGo = new GameObject("ClinicalArMenuCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.sortingOrder = 50;
        canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 14f;

        _root = canvasGo.GetComponent<RectTransform>();
        _root.sizeDelta = new Vector2(1500f, 960f);
        _root.localScale = Vector3.one * 0.0033f;

        // Paper
        Image bg = NewImage("Paper", _root, paper);
        Stretch(bg.rectTransform);

        // Top-left header
        Text wordmark = NewText("Wordmark", _root, "DISABILITY AR", 20, FontStyle.Bold, ink, _sansBold, letterSpacing: 4);
        wordmark.alignment = TextAnchor.MiddleLeft;
        Anchor(wordmark.rectTransform, 0.04f, 0.91f, 0.50f, 0.96f);

        // Top-right header
        Text yearMark = NewText("YearMark", _root, "SIMULATION MEDICALE / AR", 20, FontStyle.Normal, soft, _sans, letterSpacing: 3);
        yearMark.alignment = TextAnchor.MiddleRight;
        Anchor(yearMark.rectTransform, 0.50f, 0.91f, 0.96f, 0.96f);

        // Thick top divider
        Image topBar = NewImage("TopBar", _root, ink);
        Anchor(topBar.rectTransform, 0.04f, 0.885f, 0.96f, 0.895f);

        // Thick bottom divider
        Image bottomBar = NewImage("BottomBar", _root, ink);
        Anchor(bottomBar.rectTransform, 0.04f, 0.135f, 0.96f, 0.145f);

        _introRoot = CreateRect("Intro", _root);
        Stretch(_introRoot);
        BuildIntro();

        _selectionRoot = CreateRect("Selection", _root);
        Stretch(_selectionRoot);
        BuildSelection();
    }

    void BuildIntro()
    {
        // Massive serif/sans number left
        _slideCounter = NewText("Number", _introRoot, "01 / 04", 32, FontStyle.Bold, ink, _sansBold, letterSpacing: 4);
        _slideCounter.alignment = TextAnchor.UpperLeft;
        Anchor(_slideCounter.rectTransform, 0.04f, 0.80f, 0.40f, 0.86f);

        // Red dot top right
        Image dot = NewImage("RedDot", _introRoot, red);
        Anchor(dot.rectTransform, 0.935f, 0.82f, 0.96f, 0.86f);
        dot.sprite = MakeDiscSprite();
        _redDotIntro = dot;

        // Title — huge bold caps
        _slideTitle = NewText("Title", _introRoot, "", 140, FontStyle.Bold, ink, _sansBold);
        _slideTitle.alignment = TextAnchor.UpperLeft;
        Anchor(_slideTitle.rectTransform, 0.04f, 0.36f, 0.96f, 0.78f);

        // Body — quiet sans
        _slideBody = NewText("Body", _introRoot, "", 28, FontStyle.Normal, ink, _sans);
        _slideBody.alignment = TextAnchor.UpperLeft;
        Anchor(_slideBody.rectTransform, 0.04f, 0.18f, 0.85f, 0.34f);

        // Hint bottom left
        _slideHint = NewText("Hint", _introRoot, "", 22, FontStyle.Bold, ink, _sansBold, letterSpacing: 3);
        _slideHint.alignment = TextAnchor.MiddleLeft;
        Anchor(_slideHint.rectTransform, 0.04f, 0.07f, 0.70f, 0.12f);

        // Right corner hint
        Text rmark = NewText("RMark", _introRoot, "[ ! ]", 22, FontStyle.Bold, red, _sansBold);
        rmark.alignment = TextAnchor.MiddleRight;
        Anchor(rmark.rectTransform, 0.85f, 0.07f, 0.96f, 0.12f);
    }

    void BuildSelection()
    {
        // Massive title
        _selectionTitle = NewText("SelTitle", _selectionRoot, "MODULES AR.", 92, FontStyle.Bold, ink, _sansBold);
        _selectionTitle.alignment = TextAnchor.UpperLeft;
        Anchor(_selectionTitle.rectTransform, 0.04f, 0.74f, 0.70f, 0.86f);

        _selectionCounter = NewText("SelCounter", _selectionRoot, "02 ACTIFS / 06", 22, FontStyle.Bold, soft, _sansBold, letterSpacing: 4);
        _selectionCounter.alignment = TextAnchor.UpperRight;
        Anchor(_selectionCounter.rectTransform, 0.55f, 0.80f, 0.96f, 0.86f);

        Image accent = NewImage("AccentDot", _selectionRoot, red);
        Anchor(accent.rectTransform, 0.935f, 0.755f, 0.96f, 0.795f);
        accent.sprite = MakeDiscSprite();

        Text sub = NewText("Sub", _selectionRoot, "Selectionner une pathologie. Simulation visible en AR passthrough.", 24, FontStyle.Normal, soft, _sans);
        sub.alignment = TextAnchor.UpperLeft;
        Anchor(sub.rectTransform, 0.04f, 0.68f, 0.96f, 0.73f);

        // Card grid
        RectTransform deck = CreateRect("Deck", _selectionRoot);
        Anchor(deck, 0.04f, 0.18f, 0.96f, 0.66f);

        string[] titles   = { "GLAUCOME", "NYSTAGMUS", "PHOTOPHOBIE", "DYSLEXIE", "MYOPIE", "DALTONISME" };
        string[] subjects = { "Champ visuel", "Oscillopsie", "Lumiere", "Lecture", "Flou distance", "Couleurs" };
        string[] scenes   = { glaucomaSceneName, nystagmusSceneName, photophobiaSceneName, dyslexiaSceneName, "", "" };
        bool[] unlocked   = { true, true, true, true, false, false };

        for (int i = 0; i < 6; i++)
        {
            int col = i % 3;
            int row = i / 3;
            float xMin = col / 3f + 0.005f;
            float xMax = (col + 1) / 3f - 0.005f;
            float yMin = 1f - (row + 1) / 2f + 0.015f;
            float yMax = 1f - row / 2f - 0.015f;
            AddCard(deck, (i + 1).ToString("00"), titles[i], subjects[i], scenes[i], unlocked[i], xMin, yMin, xMax, yMax);
        }

        _selectionHint = NewText("SelHint", _selectionRoot, "JOYSTICK  ->  NAVIGUER     GACHETTE  ->  LANCER", 22, FontStyle.Bold, ink, _sansBold, letterSpacing: 3);
        _selectionHint.alignment = TextAnchor.MiddleCenter;
        Anchor(_selectionHint.rectTransform, 0.04f, 0.07f, 0.96f, 0.12f);
    }

    void AddCard(RectTransform parent, string index, string title, string subject, string sceneName, bool unlocked, float xMin, float yMin, float xMax, float yMax)
    {
        RectTransform rect = CreateRect(title, parent);
        Anchor(rect, xMin, yMin, xMax, yMax);

        Image panel = NewImage("Panel", rect, paper);
        Stretch(panel.rectTransform);

        // Hairline frame
        Image frame = NewImage("Frame", rect, ink);
        Stretch(frame.rectTransform);
        frame.sprite = MakeFrameSprite(2);
        frame.type = Image.Type.Sliced;

        // Left thick bar
        Image leftBar = NewImage("LeftBar", rect, ink);
        Anchor(leftBar.rectTransform, 0f, 0f, 0.035f, 1f);

        // Top-right red dot (selected only)
        Image dot = NewImage("Dot", rect, new Color(0, 0, 0, 0));
        Anchor(dot.rectTransform, 0.84f, 0.83f, 0.93f, 0.92f);
        dot.sprite = MakeDiscSprite();

        // Index big in top-right
        Text indexText = NewText("Index", rect, index, 64, FontStyle.Bold, ink, _sansBold);
        indexText.alignment = TextAnchor.UpperRight;
        Anchor(indexText.rectTransform, 0.50f, 0.62f, 0.80f, 0.92f);

        // Title
        Text titleText = NewText("Title", rect, title, 40, FontStyle.Bold, ink, _sansBold, letterSpacing: -1);
        titleText.alignment = TextAnchor.UpperLeft;
        Anchor(titleText.rectTransform, 0.10f, 0.40f, 0.92f, 0.62f);

        // Subject
        Text subj = NewText("Subject", rect, subject.ToUpper() + ".", 18, FontStyle.Normal, ink, _sans, letterSpacing: 2);
        subj.alignment = TextAnchor.UpperLeft;
        Anchor(subj.rectTransform, 0.10f, 0.26f, 0.92f, 0.38f);

        // Thick divider between subject and status
        Image divider = NewImage("Divider", rect, ink);
        Anchor(divider.rectTransform, 0.10f, 0.218f, 0.92f, 0.226f);

        Text status = NewText("Status", rect, unlocked ? "AVAILABLE" : "SOON", 20, FontStyle.Bold, ink, _sansBold, letterSpacing: 4);
        status.alignment = TextAnchor.LowerLeft;
        Anchor(status.rectTransform, 0.10f, 0.08f, 0.92f, 0.20f);

        _cards.Add(new Card
        {
            index = index, title = title, subject = subject, sceneName = sceneName, unlocked = unlocked,
            rect = rect, panel = panel, leftBar = leftBar, dot = dot,
            indexText = indexText, titleText = titleText, subjectText = subj, statusText = status
        });
    }

    // ----------------- HEAD + INPUT -----------------

    void ResolveHead()
    {
        if (headAnchor != null) return;
        OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null) headAnchor = rig.centerEyeAnchor;
        if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
    }

    void AttachToHead()
    {
        ResolveHead();
        if (headAnchor == null) return;
        transform.SetParent(headAnchor, false);
        transform.localPosition = new Vector3(0f, verticalOffset, distance);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }

    bool ConfirmPressed()
    {
        bool held = ConfirmHeldNow();
        if (!held) { _confirmReleased = true; return false; }
        if (!_confirmReleased) return false;
        _confirmReleased = false;
        return true;
    }

    bool ConfirmHeldNow()
    {
        float trig = Mathf.Max(
            Axis1DSafe(OVRInput.Axis1D.PrimaryIndexTrigger),
            Axis1DSafe(OVRInput.Axis1D.SecondaryIndexTrigger));
        return Held(OVRInput.Button.One) ||
               Held(OVRInput.Button.Three) ||
               Held(OVRInput.Button.PrimaryIndexTrigger) ||
               Held(OVRInput.Button.SecondaryIndexTrigger) ||
               trig > 0.55f ||
               KeyHeld(KeyCode.Space) || KeyHeld(KeyCode.Return);
    }

    static bool Held(OVRInput.Button b)
    { try { return OVRInput.Get(b, OVRInput.Controller.All); } catch { return false; } }

    void Pulse(float amp = -1f, float dur = -1f)
    {
        if (!hapticFeedback) return;
        float a = amp < 0f ? hapticAmplitude : amp;
        float d = dur < 0f ? hapticDuration : dur;
        try
        {
            OVRInput.SetControllerVibration(1f, a, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(1f, a, OVRInput.Controller.LTouch);
        } catch { }
        _hapticStopAt = Time.unscaledTime + d;
    }

    void HapticTick()
    {
        if (_hapticStopAt > 0f && Time.unscaledTime >= _hapticStopAt)
        {
            _hapticStopAt = -1f;
            try
            {
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.RTouch);
                OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.LTouch);
            } catch { }
        }
    }

    static bool Down(OVRInput.Button button)
    { try { return OVRInput.GetDown(button, OVRInput.Controller.All); } catch { return false; } }

    static Vector2 AxisSafe(OVRInput.Axis2D axis)
    { try { return OVRInput.Get(axis, OVRInput.Controller.All); } catch { return Vector2.zero; } }

    static float Axis1DSafe(OVRInput.Axis1D axis)
    { try { return OVRInput.Get(axis, OVRInput.Controller.All); } catch { return 0f; } }

    Vector2 CombinedNavigationAxis()
    {
        Vector2 primary = AxisSafe(OVRInput.Axis2D.PrimaryThumbstick);
        Vector2 secondary = AxisSafe(OVRInput.Axis2D.SecondaryThumbstick);
        Vector2 axis = secondary.sqrMagnitude >= primary.sqrMagnitude ? secondary : primary;
        if (axis.sqrMagnitude < 0.01f) axis = AxisSafe(navigationAxis);
        if (KeyHeld(KeyCode.LeftArrow) || KeyHeld(KeyCode.A)) axis.x = -1f;
        if (KeyHeld(KeyCode.RightArrow) || KeyHeld(KeyCode.D)) axis.x = 1f;
        if (KeyHeld(KeyCode.UpArrow) || KeyHeld(KeyCode.W)) axis.y = 1f;
        if (KeyHeld(KeyCode.DownArrow) || KeyHeld(KeyCode.S)) axis.y = -1f;
        return axis;
    }

    static bool KeyDown(KeyCode key)
    { try { return Input.GetKeyDown(key); } catch { return false; } }

    static bool KeyHeld(KeyCode key)
    { try { return Input.GetKey(key); } catch { return false; } }

    bool TriggerPressedOnce(ref bool held)
    {
        float value = Mathf.Max(
            Axis1DSafe(OVRInput.Axis1D.PrimaryIndexTrigger),
            Axis1DSafe(OVRInput.Axis1D.SecondaryIndexTrigger));
        bool pressed = value > 0.55f;
        bool down = pressed && !held;
        held = pressed;
        return down;
    }

    static int PositiveModulo(int v, int n) => ((v % n) + n) % n;

    // ----------------- HELPERS -----------------

    static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    Text NewText(string name, Transform parent, string text, int size, FontStyle style, Color color, Font font, int letterSpacing = 0)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text uiText = go.GetComponent<Text>();
        uiText.text = letterSpacing > 0 ? Tracked(text, letterSpacing) : text;
        uiText.font = font;
        uiText.fontSize = size;
        uiText.fontStyle = style;
        uiText.color = color;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;
        uiText.raycastTarget = false;
        uiText.supportRichText = false;
        return uiText;
    }

    static string Tracked(string s, int spacing)
    {
        if (string.IsNullOrEmpty(s) || spacing <= 0) return s;
        var sb = new System.Text.StringBuilder(s.Length * 2);
        string gap = new string(' ', Mathf.Max(1, spacing / 4));
        for (int i = 0; i < s.Length; i++) { sb.Append(s[i]); if (i < s.Length - 1) sb.Append(gap); }
        return sb.ToString();
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void Anchor(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
    {
        rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    // ----------------- SPRITES -----------------

    static Sprite _disc;
    static readonly Dictionary<int, Sprite> _frames = new Dictionary<int, Sprite>();

    static Sprite MakeFrameSprite(int border)
    {
        if (_frames.TryGetValue(border, out var cached)) return cached;
        int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool inBorder = x < border || y < border || x >= size - border || y >= size - border;
                tex.SetPixel(x, y, inBorder ? Color.white : new Color(0, 0, 0, 0));
            }
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect, new Vector4(border + 2, border + 2, border + 2, border + 2));
        _frames[border] = s;
        return s;
    }

    static Sprite MakeDiscSprite()
    {
        if (_disc != null) return _disc;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / (size * 0.5f);
                float a = d <= 0.94f ? 1f : Mathf.SmoothStep(1f, 0f, (d - 0.94f) / 0.06f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        _disc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        return _disc;
    }
}
