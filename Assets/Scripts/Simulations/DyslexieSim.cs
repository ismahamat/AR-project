using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class DyslexieSim : HandicapSimulationBase
{
    [Header("Texte de lecture")]
    [TextArea(8, 16)] public string readingText =
        "Lorsque le soir descendait sur la ville, les facades semblaient conserver, dans leurs pierres tiedes, " +
        "le murmure des journees anciennes. Un voyageur, dont la memoire se fragmentait au rythme des pas, " +
        "traversait les rues et croyait reconnaitre chaque fenetre, chaque enseigne, chaque reflet dans les vitres obscures. " +
        "Pourtant, plus il avancait, plus les phrases lues sur les murs perdaient leur ordre, comme si les mots hesitaient " +
        "entre plusieurs sens possibles. Il lui fallait revenir au debut d'une ligne, puis repartir, ralentir, deviner, " +
        "corriger une syllabe, confondre une autre, et maintenir malgre tout le fil fragile de ce qu'il tentait de comprendre. " +
        "Dans cette fatigue silencieuse, la lecture cessait d'etre un geste automatique et devenait une marche difficile, " +
        "pleine de detours, d'efforts invisibles et de petites victoires arrachees a la confusion.";
    [Range(0f, 1f)] public float scrambleIntensity = 0.82f;
    [Range(0.05f, 1.0f)] public float scrambleInterval = 0.35f;

    [Header("Contrôles")]
    public OVRInput.Button toggleButton = OVRInput.Button.PrimaryIndexTrigger;
    public OVRInput.Button validateButton = OVRInput.Button.One;

    GameObject _readingRoot;
    RectTransform _readingCanvasRect;
    TextMeshProUGUI _text;
    TextMeshProUGUI _buttonLabel;
    TextMeshProUGUI _finishLabel;
    Image _buttonBg;
    Image _finishBg;
    Sprite _panelSprite;
    Sprite _buttonSprite;
    bool _effectOn;
    bool _challengeComplete;
    float _nextScrambleAt;
    float _startTime;
    int _stableFrames;

    void Reset()
    {
        handicapTitle = "Dyslexie";
        explanation =
            "La <b>dyslexie</b> est un trouble durable de l'apprentissage de la lecture. " +
            "Elle ne vient pas d'un probleme de vue : l'oeil peut etre sain, mais le <b>cerveau</b> traite plus difficilement les sons, les lettres et leur ordre.\n\n" +
            "Dans cette simulation, le texte devient instable pour faire ressentir une lecture plus lente, plus couteuse et plus fatigante.";
        correctionText =
            "On ne corrige pas la dyslexie avec des lunettes classiques. L'aide repose surtout sur :\n\n" +
            "<b>•</b> Orthophonie et entrainement phonologique\n" +
            "<b>•</b> Exercices de decodage, conscience des sons et automatisation de la lecture\n" +
            "<b>•</b> Mise en page aeree, interlignage, police lisible, consignes courtes\n" +
            "<b>•</b> Temps supplementaire, lecture audio, synthese vocale, correcteur et outils numeriques\n" +
            "<b>•</b> Adaptations scolaires : tiers-temps, evaluation orale, supports prepares\n\n" +
            "<i>La simulation est pedagogique : elle ne reproduit pas exactement toutes les formes de dyslexie.</i>";
        anatomyPart = "VisualCortex";
        accent = new Color(0.58f, 0.45f, 0.82f, 1f);
        challengeKey = "dyslexie";
        challengeInstructions = "Activez l'effet, lisez le texte, puis desactivez-le pour comparer la vitesse et l'effort de lecture.";
        eyeDiseasedScaleFactor = new Vector3(1.10f, 1.10f, 1.10f);
    }

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(handicapTitle) || handicapTitle == "Handicap") Reset();
        base.Awake();
    }

    protected override void OnSetup()
    {
        EnsureHeadAnchor();
        BuildReadingPanel();
        SetReadingVisible(false);
    }

    protected override void OnSimulationStart()
    {
        BuildReadingPanel();
        _effectOn = true;
        _challengeComplete = false;
        _startTime = Time.unscaledTime;
        _nextScrambleAt = 0f;
        SetReadingVisible(true);
        RefreshButton();
        SetSimulationHint(
            "Un texte de lecture est affiche devant vous.\n\n" +
            "<b>Gachette / A / Espace</b> : activer ou desactiver l'effet.\n" +
            "<b>U</b> : fin de simulation et traitements.");
    }

    protected override void OnSimulationStop()
    {
        SetReadingVisible(false);
        _effectOn = false;
    }

    protected override void OnChallengeStart()
    {
        BuildReadingPanel();
        _effectOn = true;
        _challengeComplete = false;
        _stableFrames = 0;
        _startTime = Time.unscaledTime;
        _nextScrambleAt = 0f;
        SetReadingVisible(true);
        RefreshButton();
        SetChallengeStatus("Lisez le texte. <b>Gachette / A / Espace</b> active ou desactive le melange. <b>U</b> termine.");
    }

    protected override void OnChallengeStop()
    {
        SetReadingVisible(false);
        _effectOn = false;
    }

    protected override void OnCorrectionToggle(bool on)
    {
        _effectOn = !on;
        RefreshButton();
        UpdateDisplayedText(force: true);
    }

    protected override void Update()
    {
        if ((CurrentPhase == Phase.Simulation || CurrentPhase == Phase.Challenge) && HandleFinishInput())
            return;

        base.Update();
        if (CurrentPhase != Phase.Simulation && CurrentPhase != Phase.Challenge && CurrentPhase != Phase.Correction)
            return;

        HandleToggleInput();
        UpdateDisplayedText(force: false);

        if (CurrentPhase == Phase.Challenge && !_challengeComplete && !_effectOn)
        {
            _stableFrames++;
            if (_stableFrames > 45 && Time.unscaledTime - _startTime > 5f)
            {
                _challengeComplete = true;
                EndChallenge();
            }
        }
        else _stableFrames = 0;
    }

    void BuildReadingPanel()
    {
        if (_readingRoot != null) return;
        EnsureHeadAnchor();

        _panelSprite = MRUI.RoundedRect(512, 512, 36, Color.white);
        _buttonSprite = MRUI.RoundedRect(256, 96, 34, Color.white);

        _readingRoot = new GameObject("DyslexieReadingPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Transform anchor = headAnchor != null ? headAnchor : transform;
        _readingRoot.transform.SetParent(anchor, false);
        _readingRoot.transform.localPosition = new Vector3(0f, -0.08f, 1.45f);
        _readingRoot.transform.localRotation = Quaternion.identity;

        Canvas canvas = _readingRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 80;
        _readingRoot.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 8f;

        _readingCanvasRect = _readingRoot.GetComponent<RectTransform>();
        _readingCanvasRect.sizeDelta = new Vector2(1550f, 900f);
        _readingCanvasRect.localScale = Vector3.one * 0.0012f;

        Image bg = MRUI.NewImage("Paper", _readingCanvasRect, MRUI.BG_CREAM, _panelSprite);
        MRUI.Stretch(bg.rectTransform);

        TextMeshProUGUI title = MRUI.NewText("Title", _readingCanvasRect, "EPREUVE DE LECTURE", 56, MRUI.Darken(accent, 0.35f), FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        MRUI.Anchor(title.rectTransform, new Vector2(0.07f, 0.80f), new Vector2(0.93f, 0.92f));

        Image rule = MRUI.NewImage("Rule", _readingCanvasRect, accent, _buttonSprite);
        MRUI.Anchor(rule.rectTransform, new Vector2(0.07f, 0.765f), new Vector2(0.93f, 0.778f));

        _text = MRUI.NewText("ReadingText", _readingCanvasRect, readingText, 28, MRUI.TEXT_DARK, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        _text.enableAutoSizing = false;
        _text.fontSizeMin = 28f;
        _text.fontSizeMax = 28f;
        _text.overflowMode = TextOverflowModes.Truncate;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.lineSpacing = 12f;
        _text.characterSpacing = 2.5f;
        MRUI.Anchor(_text.rectTransform, new Vector2(0.08f, 0.30f), new Vector2(0.92f, 0.72f));

        _buttonBg = MRUI.NewImage("ToggleButton", _readingCanvasRect, accent, _buttonSprite);
        MRUI.Anchor(_buttonBg.rectTransform, new Vector2(0.08f, 0.16f), new Vector2(0.47f, 0.27f));
        Button toggleUiButton = _buttonBg.gameObject.AddComponent<Button>();
        toggleUiButton.targetGraphic = _buttonBg;
        toggleUiButton.onClick.AddListener(ToggleDyslexiaEffect);

        _buttonLabel = MRUI.NewText("ToggleLabel", _buttonBg.rectTransform, "", 30, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        MRUI.Stretch(_buttonLabel.rectTransform);

        _finishBg = MRUI.NewImage("FinishButton", _readingCanvasRect, MRUI.TEXT_DARK, _buttonSprite);
        MRUI.Anchor(_finishBg.rectTransform, new Vector2(0.53f, 0.16f), new Vector2(0.92f, 0.27f));

        _finishLabel = MRUI.NewText("FinishLabel", _finishBg.rectTransform, "U  FIN SIMULATION", 30, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        MRUI.Stretch(_finishLabel.rectTransform);

        TextMeshProUGUI hint = MRUI.NewText("Hint", _readingCanvasRect, "GACHETTE / A / ESPACE  ->  EFFET     U  ->  FIN", 22, MRUI.TEXT_GRAY, FontStyles.Bold, TextAlignmentOptions.Center);
        MRUI.Anchor(hint.rectTransform, new Vector2(0.06f, 0.055f), new Vector2(0.94f, 0.10f));
    }

    void SetReadingVisible(bool visible)
    {
        if (_readingRoot != null) _readingRoot.SetActive(visible);
    }

    void HandleToggleInput()
    {
        bool down = false;
        try
        {
            down = OVRInput.GetDown(toggleButton, OVRInput.Controller.All) ||
                   OVRInput.GetDown(validateButton, OVRInput.Controller.All);
        }
        catch { }
#if ENABLE_INPUT_SYSTEM
        if (!down && Keyboard.current != null)
            down = Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame;
#endif
        if (!down) return;

        ToggleDyslexiaEffect();
        HapticFeedback.Tap(OVRInput.Controller.RTouch);
    }

    bool HandleFinishInput()
    {
        bool down = false;
#if ENABLE_INPUT_SYSTEM
        down = Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame;
#endif
        if (!down) return false;

        FinishSimulation();
        HapticFeedback.Tap(OVRInput.Controller.LTouch);
        return true;
    }

    void ToggleDyslexiaEffect()
    {
        _effectOn = !_effectOn;
        _nextScrambleAt = 0f;
        RefreshButton();
        UpdateDisplayedText(force: true);
    }

    void FinishSimulation()
    {
        if (CurrentPhase == Phase.Correction) return;
        _challengeComplete = true;
        SetReadingVisible(false);
        _effectOn = false;
        EndChallenge();
    }

    void RefreshButton()
    {
        if (_buttonBg != null) _buttonBg.color = _effectOn ? accent : MRUI.BUTTON_GREEN;
        if (_buttonLabel != null) _buttonLabel.text = _effectOn ? "DYSLEXIE ACTIVE" : "LECTURE STABLE";
    }

    void UpdateDisplayedText(bool force)
    {
        if (_text == null) return;
        if (!force && Time.unscaledTime < _nextScrambleAt) return;
        _nextScrambleAt = Time.unscaledTime + scrambleInterval;
        _text.text = _effectOn ? ScrambleText(readingText) : readingText;
    }

    string ScrambleText(string source)
    {
        if (string.IsNullOrEmpty(source)) return "";

        string[] words = source.Split(' ');
        StringBuilder sb = new StringBuilder(source.Length + 16);
        for (int i = 0; i < words.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(ScrambleWord(words[i]));
        }
        return sb.ToString();
    }

    string ScrambleWord(string word)
    {
        if (word.Length < 4 || Random.value > scrambleIntensity) return word;

        int start = 0;
        int end = word.Length - 1;
        while (start < word.Length && !char.IsLetter(word[start])) start++;
        while (end >= 0 && !char.IsLetter(word[end])) end--;
        if (end - start < 3) return word;

        char[] chars = word.ToCharArray();
        int swaps = Mathf.Clamp(Mathf.RoundToInt(scrambleIntensity * 3f), 1, 3);
        for (int s = 0; s < swaps; s++)
        {
            int a = Random.Range(start + 1, end);
            int b = Random.Range(start + 1, end);
            if (a == b) b = Mathf.Clamp(b + 1, start + 1, end - 1);
            char tmp = chars[a];
            chars[a] = chars[b];
            chars[b] = tmp;
        }

        for (int i = start + 1; i < end; i++)
        {
            if (Random.value < scrambleIntensity * 0.28f)
                chars[i] = SwapLookalike(chars[i]);
            else if (Random.value < scrambleIntensity * 0.10f && char.IsLetter(chars[i]))
                chars[i] = Random.value < 0.5f ? '~' : chars[i];
        }
        return new string(chars);
    }

    char SwapLookalike(char c)
    {
        switch (c)
        {
            case 'b': return 'd';
            case 'd': return 'b';
            case 'p': return 'q';
            case 'q': return 'p';
            case 'm': return 'n';
            case 'n': return 'm';
            case 'B': return 'D';
            case 'D': return 'B';
            case 'P': return 'Q';
            case 'Q': return 'P';
            case 'M': return 'N';
            case 'N': return 'M';
            default: return c;
        }
    }
}
