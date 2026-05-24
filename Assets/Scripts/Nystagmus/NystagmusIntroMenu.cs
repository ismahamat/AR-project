using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class NystagmusIntroMenu : MonoBehaviour
{
    [Header("Lien simulation")]
    public NystagmusSim simulation;
    public Transform headAnchor;
    public float panelDistance = 3.5f;
    public float panelVerticalOffset = -0.05f;
    public float eyeDistance = 3.2f;
    public Vector3 eyeOffset = new Vector3(0.52f, 0.10f, 0f);

    [Header("Navigation")]
    public string menuSceneName = "SampleScene";

    [Header("Intensité HUD")]
    public OVRInput.Axis2D intensityAxis = OVRInput.Axis2D.SecondaryThumbstick;
    [Range(0f, 0.5f)] public float stickDeadZone = 0.20f;
    [Range(0.05f, 0.7f)] public float intensityStart = 0.35f;
    public float hudFadeDelay = 2.0f;
    public float hudFadeDuration = 0.6f;

    [Header("Palette AR clinique")]
    public Color paper = new Color(0.055f, 0.062f, 0.066f, 0.96f);
    public Color ink   = new Color(0.925f, 0.918f, 0.890f, 1f);
    public Color soft  = new Color(0.610f, 0.650f, 0.660f, 1f);
    public Color red   = new Color(0.280f, 0.620f, 0.720f, 1f);

    Canvas _baseCanvas;
    GameObject _introRig;
    GameObject _eyeRoot;
    Transform _eyeShake;
    Material _highlightMaterial;
    Canvas _hudCanvas;
    RectTransform _hudRoot;
    Image _hudFill;
    Image _hudBack;
    Image _hudRedDot;
    Text _hudPercent;
    Text _hudLabel;
    CanvasGroup _hudGroup;

    Font _sans;
    float _hudVisibility;
    float _lastInputTime = -10f;
    float _time;
    bool _explanationVisible;

    void Awake()
    {
        ApplyClinicalPalette();
        intensityStart = Mathf.Max(intensityStart, 0.35f);
        LoadFonts();
        if (simulation == null) simulation = GetComponent<NystagmusSim>();
        if (simulation == null) simulation = FindAnyObjectByType<NystagmusSim>();

        if (simulation != null)
        {
            simulation.intensity = intensityStart;
            if (!string.IsNullOrEmpty(menuSceneName)) simulation.menuSceneName = menuSceneName;
        }
        ResolveHead();
    }

    void ApplyClinicalPalette()
    {
        panelDistance = 3.50f;
        panelVerticalOffset = -0.02f;
        eyeDistance = 3.20f;
        paper = new Color(0.055f, 0.062f, 0.066f, 0.96f);
        ink = new Color(0.925f, 0.918f, 0.890f, 1f);
        soft = new Color(0.610f, 0.650f, 0.660f, 1f);
        red = new Color(0.280f, 0.620f, 0.720f, 1f);
    }

    void Start()
    {
        DisableBaseCanvas();
        BuildIntroRig();
        BuildEyeAnatomy();
        BuildHudSlider();
        SetExplanationVisible(true);
    }

    void Update()
    {
        _time += Time.deltaTime;
        if (simulation == null) return;

        var phase = simulation.CurrentPhase;
        if (phase == HandicapSimulationBase.Phase.Explanation)
        {
            if (!_explanationVisible) SetExplanationVisible(true);
            if (_hudGroup != null) _hudGroup.alpha = 0f;
            AnimateEye();
        }
        else if (phase == HandicapSimulationBase.Phase.Simulation)
        {
            if (_explanationVisible) SetExplanationVisible(false);
            UpdateHudSlider();
        }
        else
        {
            if (_explanationVisible) SetExplanationVisible(false);
            if (_hudGroup != null) _hudGroup.alpha = 0f;
        }

        if (_baseCanvas != null && _baseCanvas.gameObject.activeSelf)
            _baseCanvas.gameObject.SetActive(false);
    }

    void LoadFonts()
    {
        _sans = Font.CreateDynamicFontFromOSFont(
            new[] { "Helvetica Neue", "Helvetica", "Akzidenz-Grotesk Pro", "Univers", "Arial", "Roboto", "Noto Sans" }, 32);
        if (_sans == null) _sans = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void DisableBaseCanvas()
    {
        if (simulation == null) return;
        Transform t = simulation.transform.Find("PathologyInfoCanvas");
        if (t != null)
        {
            _baseCanvas = t.GetComponent<Canvas>();
            t.gameObject.SetActive(false);
        }
    }

    void SetExplanationVisible(bool on)
    {
        _explanationVisible = on;
        if (_introRig != null) _introRig.SetActive(on);
        if (_eyeRoot != null) _eyeRoot.SetActive(on);
    }

    // ----------------- INTRO PANEL -----------------

    void BuildIntroRig()
    {
        _introRig = new GameObject("NystagmusClinicalPanel");
        if (headAnchor != null)
        {
            _introRig.transform.SetParent(headAnchor, false);
            _introRig.transform.localPosition = new Vector3(0f, panelVerticalOffset, panelDistance);
            _introRig.transform.localRotation = Quaternion.identity;
        }

        GameObject canvasGo = new GameObject("PanelCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(_introRig.transform, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 60;
        canvasGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 14f;

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        root.sizeDelta = new Vector2(1600f, 1000f);
        root.localScale = Vector3.one * 0.0031f;

        Image bg = NewImage("Paper", root, paper);
        Stretch(bg.rectTransform);

        Text wm = NewText("Wordmark", root, "DISABILITY AR  /  MODULE 02", 20, FontStyle.Bold, ink, _sans, letterSpacing: 4);
        wm.alignment = TextAnchor.MiddleLeft;
        Anchor(wm.rectTransform, 0.04f, 0.91f, 0.60f, 0.96f);

        Text fileNo = NewText("FileNo", root, "NYSTAGMUS / AR PASSTHROUGH", 20, FontStyle.Normal, soft, _sans, letterSpacing: 3);
        fileNo.alignment = TextAnchor.MiddleRight;
        Anchor(fileNo.rectTransform, 0.55f, 0.91f, 0.96f, 0.96f);

        Image top = NewImage("Top", root, ink);
        Anchor(top.rectTransform, 0.04f, 0.885f, 0.96f, 0.895f);

        Image redDot = NewImage("RedDot", root, red);
        Anchor(redDot.rectTransform, 0.94f, 0.92f, 0.96f, 0.95f);
        redDot.sprite = MakeDiscSprite();

        // ===== LEFT COLUMN =====
        RectTransform left = CreateRect("LeftCol", root);
        Anchor(left, 0.04f, 0.16f, 0.56f, 0.87f);

        Text id = NewText("Id", left, "02", 220, FontStyle.Bold, ink, _sans, letterSpacing: -6);
        id.alignment = TextAnchor.UpperLeft;
        Anchor(id.rectTransform, 0f, 0.66f, 0.32f, 1f);

        Text eyebrow = NewText("Eyebrow", left, "PATHOLOGIE", 22, FontStyle.Bold, red, _sans, letterSpacing: 8);
        eyebrow.alignment = TextAnchor.UpperLeft;
        Anchor(eyebrow.rectTransform, 0.33f, 0.93f, 1f, 1f);

        Text title = NewText("Title", left, "NYSTAGMUS.", 98, FontStyle.Bold, ink, _sans);
        title.alignment = TextAnchor.UpperLeft;
        Anchor(title.rectTransform, 0.33f, 0.66f, 1f, 0.92f);

        Image div = NewImage("TitleDiv", left, ink);
        Anchor(div.rectTransform, 0f, 0.62f, 1f, 0.63f);

        BuildSection(left, "CAUSE.",
            "Mouvement oculaire involontaire. Type jerk : dérive lente puis saccade de retour.",
            0.41f, 0.60f);

        Image div2 = NewImage("Div2", left, ink);
        Anchor(div2.rectTransform, 0f, 0.40f, 1f, 0.405f);

        BuildSection(left, "SYMPTOMS.",
            "Oscillopsie : le monde semble vibrer ou glisser. Flou, fatigue, lecture difficile.",
            0.21f, 0.39f);

        Image div3 = NewImage("Div3", left, ink);
        Anchor(div3.rectTransform, 0f, 0.20f, 1f, 0.205f);

        BuildSection(left, "TREATMENT.",
            "Prismes, rééducation, traitements ciblés, chirurgie du point neutre selon origine.",
            0.01f, 0.19f);

        // ===== RIGHT COLUMN =====
        RectTransform right = CreateRect("RightCol", root);
        Anchor(right, 0.59f, 0.16f, 0.96f, 0.87f);

        Image rightFrame = NewImage("RightBorder", right, ink);
        Stretch(rightFrame.rectTransform);
        rightFrame.sprite = MakeFrameSprite(2);
        rightFrame.type = Image.Type.Sliced;

        Text plate = NewText("Plate", right, "PLATE II", 22, FontStyle.Bold, ink, _sans, letterSpacing: 6);
        plate.alignment = TextAnchor.UpperLeft;
        Anchor(plate.rectTransform, 0.05f, 0.90f, 0.95f, 0.96f);

        Text plateSub = NewText("PlateSub", right, "OSCILLATION OCULAIRE", 22, FontStyle.Bold, red, _sans, letterSpacing: 4);
        plateSub.alignment = TextAnchor.UpperLeft;
        Anchor(plateSub.rectTransform, 0.05f, 0.84f, 0.95f, 0.90f);

        Image rightDivider = NewImage("RightDiv", right, ink);
        Anchor(rightDivider.rectTransform, 0.05f, 0.83f, 0.95f, 0.835f);

        Text caption = NewText("Caption", right, "FIG. 02 — OSCILLOPSIE SIMULEE", 16, FontStyle.Bold, ink, _sans, letterSpacing: 4);
        caption.alignment = TextAnchor.MiddleCenter;
        Anchor(caption.rectTransform, 0.05f, 0.06f, 0.95f, 0.10f);

        // ===== FOOTER =====
        Image bottomBar = NewImage("BottomBar", root, ink);
        Anchor(bottomBar.rectTransform, 0.04f, 0.135f, 0.96f, 0.145f);

        Text hint = NewText("Hint", root, "GACHETTE  ->  LANCER LA SIMULATION       B  ->  RETOUR", 22, FontStyle.Bold, ink, _sans, letterSpacing: 4);
        hint.alignment = TextAnchor.MiddleCenter;
        Anchor(hint.rectTransform, 0.04f, 0.05f, 0.96f, 0.13f);
    }

    void BuildSection(RectTransform parent, string label, string body, float yMin, float yMax)
    {
        Text lab = NewText(label, parent, label, 22, FontStyle.Bold, red, _sans, letterSpacing: 6);
        lab.alignment = TextAnchor.UpperLeft;
        Anchor(lab.rectTransform, 0f, yMax - 0.05f, 0.40f, yMax);

        Text bd = NewText(label + "Body", parent, body, 22, FontStyle.Normal, ink, _sans);
        bd.alignment = TextAnchor.UpperLeft;
        Anchor(bd.rectTransform, 0f, yMin, 1f, yMax - 0.06f);
    }

    // ----------------- EYE ANATOMY (oscillating) -----------------

    void BuildEyeAnatomy()
    {
        _eyeRoot = new GameObject("NystagmusEye3D");
        if (headAnchor != null)
        {
            _eyeRoot.transform.SetParent(headAnchor, false);
            _eyeRoot.transform.localPosition = new Vector3(eyeOffset.x, panelVerticalOffset + eyeOffset.y, eyeDistance + eyeOffset.z);
            _eyeRoot.transform.localRotation = Quaternion.identity;
        }
        _eyeRoot.transform.localScale = Vector3.one * 0.16f;

        // Shake container — only this transform moves; the eye children stay relative
        GameObject shakeGo = new GameObject("Shake");
        shakeGo.transform.SetParent(_eyeRoot.transform, false);
        _eyeShake = shakeGo.transform;

        // Sclera
        MakePrimitive(_eyeShake, "Sclera", PrimitiveType.Sphere,
            Vector3.zero, Vector3.one, new Color(0.97f, 0.97f, 0.96f, 1f), false);

        // Iris
        MakePrimitive(_eyeShake, "Iris", PrimitiveType.Sphere,
            new Vector3(0f, 0f, -0.42f), new Vector3(0.50f, 0.50f, 0.10f), new Color(0.04f, 0.04f, 0.04f, 1f), false);

        // Pupil
        MakePrimitive(_eyeShake, "Pupil", PrimitiveType.Sphere,
            new Vector3(0f, 0f, -0.49f), new Vector3(0.22f, 0.22f, 0.06f), Color.black, false);

        // Motion arrows (two small red capsules left/right) as trail markers — stay static, outside shake
        Renderer arrowL = MakePrimitive(_eyeRoot.transform, "ArrowL", PrimitiveType.Capsule,
            new Vector3(-1.30f, 0f, -0.20f), new Vector3(0.10f, 0.18f, 0.10f), red, true);
        arrowL.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Renderer arrowR = MakePrimitive(_eyeRoot.transform, "ArrowR", PrimitiveType.Capsule,
            new Vector3(1.30f, 0f, -0.20f), new Vector3(0.10f, 0.18f, 0.10f), red, true);
        arrowR.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        _highlightMaterial = arrowL.sharedMaterial;
    }

    Renderer MakePrimitive(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color, bool emissive)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        Collider col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer r = go.GetComponent<Renderer>();
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(sh);
        m.color = color;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.20f);
        if (emissive)
        {
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 0.5f);
        }
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    void AnimateEye()
    {
        if (_eyeShake == null) return;

        // Real jerk-nystagmus waveform: slow drift one direction (~80% of cycle),
        // then fast saccadic return (~20%). Asymmetric. Foveation pause near end of slow phase.
        const float frequency = 3.2f;
        const float slowPhaseFraction = 0.80f;
        float period = 1f / frequency;
        float phase = (_time % period) / period;
        float waveform;
        if (phase < slowPhaseFraction)
        {
            float t = phase / slowPhaseFraction;
            float eased = 1f - Mathf.Pow(1f - t, 2.6f); // ease-out: slows down before saccade
            waveform = Mathf.Lerp(-1f, 1f, eased);
        }
        else
        {
            float t = (phase - slowPhaseFraction) / (1f - slowPhaseFraction);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            waveform = Mathf.Lerp(1f, -1f, eased);
        }

        // Pupil horizontal offset clamped within iris bounds
        float pupilShift = waveform * 0.18f;
        _eyeShake.localPosition = new Vector3(pupilShift, 0f, 0f);

        // Saccade detection: flash arrows during fast phase
        bool fastPhase = phase >= slowPhaseFraction;
        if (_highlightMaterial != null && _highlightMaterial.HasProperty("_EmissionColor"))
        {
            float pulse = fastPhase ? 1.4f : 0.25f;
            _highlightMaterial.SetColor("_EmissionColor", red * pulse);
        }
    }

    // ----------------- HUD SLIDER -----------------

    void BuildHudSlider()
    {
        GameObject hudGo = new GameObject("NystagmusHud",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        if (headAnchor != null)
        {
            hudGo.transform.SetParent(headAnchor, false);
            hudGo.transform.localPosition = new Vector3(0f, -0.42f, 1.45f);
            hudGo.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
        }
        _hudCanvas = hudGo.GetComponent<Canvas>();
        _hudCanvas.renderMode = RenderMode.WorldSpace;
        _hudCanvas.sortingOrder = 70;
        hudGo.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 14f;
        _hudGroup = hudGo.GetComponent<CanvasGroup>();
        _hudGroup.alpha = 0f;

        _hudRoot = hudGo.GetComponent<RectTransform>();
        _hudRoot.sizeDelta = new Vector2(1100f, 240f);
        _hudRoot.localScale = Vector3.one * 0.0014f;

        Image bg = NewImage("HudPaper", _hudRoot, paper);
        Stretch(bg.rectTransform);

        Image frame = NewImage("HudFrame", _hudRoot, ink);
        Stretch(frame.rectTransform);
        frame.sprite = MakeFrameSprite(2);
        frame.type = Image.Type.Sliced;

        _hudRedDot = NewImage("HudRedDot", _hudRoot, red);
        Anchor(_hudRedDot.rectTransform, 0.94f, 0.83f, 0.97f, 0.92f);
        _hudRedDot.sprite = MakeDiscSprite();

        _hudLabel = NewText("Label", _hudRoot, "INTENSITE", 24, FontStyle.Bold, ink, _sans, letterSpacing: 8);
        _hudLabel.alignment = TextAnchor.MiddleLeft;
        Anchor(_hudLabel.rectTransform, 0.05f, 0.78f, 0.55f, 0.92f);

        _hudPercent = NewText("Percent", _hudRoot, "30", 130, FontStyle.Bold, ink, _sans, letterSpacing: -4);
        _hudPercent.alignment = TextAnchor.UpperRight;
        Anchor(_hudPercent.rectTransform, 0.55f, 0.30f, 0.93f, 0.95f);

        Text pctSign = NewText("Pct", _hudRoot, "%", 36, FontStyle.Bold, ink, _sans);
        pctSign.alignment = TextAnchor.UpperRight;
        Anchor(pctSign.rectTransform, 0.93f, 0.65f, 0.965f, 0.93f);

        RectTransform track = CreateRect("Track", _hudRoot);
        Anchor(track, 0.05f, 0.34f, 0.95f, 0.40f);
        _hudBack = NewImage("Back", track, new Color(ink.r, ink.g, ink.b, 0.18f));
        Stretch(_hudBack.rectTransform);

        RectTransform fillRoot = CreateRect("Fill", track);
        fillRoot.anchorMin = new Vector2(0f, 0f);
        fillRoot.anchorMax = new Vector2(intensityStart, 1f);
        fillRoot.offsetMin = Vector2.zero;
        fillRoot.offsetMax = Vector2.zero;
        _hudFill = fillRoot.gameObject.AddComponent<Image>();
        _hudFill.color = red;
        _hudFill.raycastTarget = false;

        AddTick(track, 0f,   "0");
        AddTick(track, 0.25f, "25");
        AddTick(track, 0.50f, "50");
        AddTick(track, 0.75f, "75");
        AddTick(track, 1f,   "100");

        Text hint = NewText("Hint", _hudRoot, "JOYSTICK HAUT/BAS  ->  AJUSTER     B  ->  RETOUR", 18, FontStyle.Bold, ink, _sans, letterSpacing: 4);
        hint.alignment = TextAnchor.MiddleCenter;
        Anchor(hint.rectTransform, 0.05f, 0.06f, 0.95f, 0.18f);
    }

    void AddTick(RectTransform track, float at, string label)
    {
        Image tickLine = NewImage("Tick_" + label, track, ink);
        Anchor(tickLine.rectTransform, at - 0.0015f, -0.50f, at + 0.0015f, 0f);

        Text txt = NewText("TickLabel_" + label, track, label, 16, FontStyle.Bold, ink, _sans);
        txt.alignment = TextAnchor.UpperCenter;
        Anchor(txt.rectTransform, at - 0.05f, -1.60f, at + 0.05f, -0.55f);
    }

    void UpdateHudSlider()
    {
        if (simulation == null || _hudGroup == null) return;
        Vector2 axis = AxisSafe(intensityAxis);
        bool active = Mathf.Abs(axis.y) >= stickDeadZone;
        if (active) _lastInputTime = Time.unscaledTime;

        float since = Time.unscaledTime - _lastInputTime;
        float target = since < hudFadeDelay ? 1f : Mathf.Clamp01(1f - (since - hudFadeDelay) / hudFadeDuration);

        _hudVisibility = Mathf.Lerp(_hudVisibility, target, Time.deltaTime * 8f);
        _hudGroup.alpha = _hudVisibility;

        float v = Mathf.Clamp01(simulation.intensity);
        if (_hudFill != null)
        {
            RectTransform rt = _hudFill.rectTransform;
            Vector2 max = rt.anchorMax;
            max.x = Mathf.Lerp(max.x, v, Time.deltaTime * 12f);
            rt.anchorMax = max;
        }
        if (_hudPercent != null)
            _hudPercent.text = Mathf.RoundToInt(v * 100f).ToString();

        if (_hudRedDot != null)
        {
            float a = 0.75f + Mathf.Sin(_time * 3f) * 0.20f;
            Color c = red; c.a = a; _hudRedDot.color = c;
        }
    }

    // ----------------- HEAD + INPUT -----------------

    void ResolveHead()
    {
        if (headAnchor != null) return;
        OVRCameraRig rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null) headAnchor = rig.centerEyeAnchor;
        if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
    }

    static Vector2 AxisSafe(OVRInput.Axis2D axis)
    { try { return OVRInput.Get(axis); } catch { return Vector2.zero; } }

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
    static readonly System.Collections.Generic.Dictionary<int, Sprite> _frames = new System.Collections.Generic.Dictionary<int, Sprite>();

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
