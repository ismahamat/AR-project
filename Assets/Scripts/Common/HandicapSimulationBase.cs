using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public abstract class HandicapSimulationBase : MonoBehaviour
{
    [Header("Identité")]
    public string handicapTitle = "Handicap";
    [TextArea(3, 8)] public string explanation = "Description du trouble.";
    [TextArea(3, 8)] public string correctionText = "Comment c'est corrigé.";
    public string anatomyPart = "";
    public Color accent = new Color(0.20f, 0.65f, 0.95f, 1f);

    [Header("Défi")]
    public string challengeKey = "";
    [TextArea(2, 5)] public string challengeInstructions = "Réalisez la tâche le plus vite possible.";

    [Header("Navigation")]
    public string menuSceneName = "SampleScene";

    [Header("Suivi tête")]
    public Transform headAnchor;
    public float distance = 1.5f;
    public float verticalOffset = -0.10f;
    [Range(0f, 20f)] public float followSmoothing = 6f;

    [Header("Anatomie 3D")]
    [Tooltip("Si défini, ce prefab est instancié à la place de l'œil procédural (ex. modèle Sketchfab importé en .glb).")]
    public GameObject eyeModelPrefab;

    [Header("Cacher le panel")]
    [Tooltip("Bouton qui cache/montre TOUTE l'UI (X par défaut).")]
    public OVRInput.Button hideButton = OVRInput.Button.Three;

    [Header("Inverser l'effet du handicap")]
    [Tooltip("Bouton qui active/désactive l'effet visuel du handicap pendant les phases Simulation/Défi (Y par défaut).")]
    public OVRInput.Button toggleHandicapButton = OVRInput.Button.Four;

    [Header("Animation maladie sur l'œil 3D")]
    [Tooltip("Facteur de scale appliqué à 'anatomyPart' au pic du cycle. (1,1,1) = pas d'animation.")]
    public Vector3 eyeDiseasedScaleFactor = Vector3.one;

    public enum Phase { Explanation, Simulation, Challenge, Correction, Done }
    public Phase CurrentPhase { get; private set; }
    public ChallengeTimer Timer { get; private set; }

    Canvas _canvas;
    RectTransform _canvasRect;
    GameObject _explanationPanel, _simulationPanel, _challengePanel, _correctionPanel;
    TextMeshProUGUI _explTitle, _explBody, _simHint, _challengeTimerText, _challengeInstr;
    TextMeshProUGUI _corrTitle, _corrBody, _corrToggleLabel, _corrHint;
    Image _corrToggleBg;
    EyeAnatomyView _anatomy;
    Sprite _spriteBg, _spriteTile, _spriteButton, _spriteShadow, _spriteCircle;

    bool _correctionOn;
    bool _hidden;
    bool _handicapOn;

    protected virtual void Awake()
    {
        Timer = GetComponent<ChallengeTimer>();
        if (Timer == null) Timer = gameObject.AddComponent<ChallengeTimer>();
        if (!string.IsNullOrEmpty(challengeKey)) Timer.challengeKey = challengeKey;

        BuildSprites();
        BuildUI();
        BuildAnatomy();
        OnSetup();
        EnterPhase(Phase.Explanation);
    }

    protected virtual void Start()
    {
        ResolveHeadAnchor();
        AttachToHead();
    }

    protected virtual void Update()
    {
        HandleInput();
        if (CurrentPhase == Phase.Challenge && _challengeTimerText != null && Timer != null && Timer.Running)
            _challengeTimerText.text = ChallengeTimer.Format(Timer.Elapsed);
    }

    // ───── Subclass hooks ─────────────────────────────────────────────────────
    protected virtual void OnSetup() { }
    protected virtual void OnSimulationStart() { }
    protected virtual void OnSimulationStop() { }
    protected virtual void OnChallengeStart() { }
    protected virtual void OnChallengeStop() { }
    protected virtual void OnCorrectionToggle(bool on) { }

    /// <summary>Subclasses call this when the player has completed the mini-game.</summary>
    protected void EndChallenge()
    {
        if (Timer != null && Timer.Running) Timer.End();
        OnChallengeStop();
        EnterPhase(Phase.Correction);
    }

    /// <summary>Update the challenge HUD instructions (e.g. with progress count).</summary>
    protected void SetChallengeStatus(string text)
    {
        if (_challengeInstr != null) _challengeInstr.text = text;
    }

    /// <summary>Update the simulation reveal screen body text.</summary>
    protected void SetSimulationHint(string text)
    {
        if (_simHint != null) _simHint.text = text;
    }

    protected void EnsureHeadAnchor()
    {
        if (headAnchor == null) ResolveHeadAnchor();
    }

    // ───── Phase machine ─────────────────────────────────────────────────────
    void EnterPhase(Phase p)
    {
        CurrentPhase = p;
        _hidden = false;
        RefreshVisibility();

        switch (p)
        {
            case Phase.Simulation:
                _handicapOn = true;
                SetHandicapActive(true);
                break;
            case Phase.Challenge:
                _handicapOn = true;
                SetHandicapActive(true);
                if (Timer != null) Timer.Begin();
                OnChallengeStart();
                break;
            case Phase.Correction:
                _handicapOn = true;
                SetHandicapActive(true);
                _correctionOn = false;
                ApplyCorrectionToggle(false);
                if (_corrBody != null) _corrBody.text = correctionText;
                if (_corrTitle != null)
                {
                    _corrTitle.text = "Comment c'est corrigé ?";
                    if (Timer != null && Timer.HasResult)
                        _corrTitle.text += $"   <size=70%><color=#7A8090>(votre temps : {ChallengeTimer.Format(Timer.Elapsed)})</color></size>";
                }
                break;
        }
    }

    void RefreshVisibility()
    {
        Phase p = CurrentPhase;

        // Désactive TOUT le canvas (fond beige + tous les panels) quand caché.
        if (_canvas != null) _canvas.gameObject.SetActive(!_hidden);

        if (!_hidden)
        {
            if (_explanationPanel != null) _explanationPanel.SetActive(p == Phase.Explanation);
            if (_simulationPanel  != null) _simulationPanel.SetActive(p == Phase.Simulation);
            if (_challengePanel   != null) _challengePanel.SetActive(p == Phase.Challenge);
            if (_correctionPanel  != null) _correctionPanel.SetActive(p == Phase.Correction);
        }
        if (_anatomy != null) _anatomy.gameObject.SetActive(!_hidden && p == Phase.Explanation);
    }

    void ToggleHidden()
    {
        _hidden = !_hidden;
        RefreshVisibility();
        HapticFeedback.Tap(OVRInput.Controller.RTouch);
    }

    /// <summary>
    /// Active/désactive globalement l'effet du handicap (vignette, distorsion, filtre couleur, …).
    /// L'implémentation par défaut appelle OnSimulationStart/OnSimulationStop ; les sous-classes
    /// peuvent surcharger pour un comportement plus fin.
    /// </summary>
    protected virtual void SetHandicapActive(bool on)
    {
        if (on) OnSimulationStart();
        else OnSimulationStop();
    }

    void ToggleHandicap()
    {
        _handicapOn = !_handicapOn;
        SetHandicapActive(_handicapOn);
        HapticFeedback.Tap(OVRInput.Controller.LTouch);
    }

    void HandleInput()
    {
        if (Down(hideButton) || KeyDown(Key.M)) { ToggleHidden(); return; }
        if ((Down(toggleHandicapButton) || KeyDown(Key.N)) &&
            (CurrentPhase == Phase.Simulation || CurrentPhase == Phase.Challenge))
        {
            ToggleHandicap();
            return;
        }

        bool confirm = Down(OVRInput.Button.One) ||
                       Down(OVRInput.Button.PrimaryIndexTrigger) || Down(OVRInput.Button.SecondaryIndexTrigger) ||
                       KeyDown(Key.Enter) || KeyDown(Key.Space);
        bool back    = Down(OVRInput.Button.Two) || KeyDown(Key.Backspace) || KeyDown(Key.Escape);

        switch (CurrentPhase)
        {
            case Phase.Explanation:
                if (confirm) EnterPhase(Phase.Simulation);
                else if (back) ReturnToMenu();
                break;
            case Phase.Simulation:
                if (confirm) EnterPhase(Phase.Challenge);
                else if (back) { OnSimulationStop(); EnterPhase(Phase.Explanation); }
                break;
            case Phase.Challenge:
                if (back) { if (Timer != null) Timer.Reset(); OnChallengeStop(); EnterPhase(Phase.Simulation); }
                break;
            case Phase.Correction:
                if (confirm) ApplyCorrectionToggle(!_correctionOn);
                else if (back) ReturnToMenu();
                break;
        }
    }

    void ApplyCorrectionToggle(bool on)
    {
        _correctionOn = on;
        if (_corrToggleBg != null) _corrToggleBg.color = on ? MRUI.BUTTON_GREEN : MRUI.BUTTON_BLUE;
        if (_corrToggleLabel != null) _corrToggleLabel.text = on ? "✓  Correction ACTIVE" : ">  Activer la correction";
        OnCorrectionToggle(on);
    }

    void ReturnToMenu()
    {
        OnSimulationStop();
        OnChallengeStop();
        OnCorrectionToggle(false);
        if (!string.IsNullOrEmpty(menuSceneName))
            SceneManager.LoadScene(menuSceneName);
    }

    static bool Down(OVRInput.Button b)
    {
        try { return OVRInput.GetDown(b); } catch { return false; }
    }

    static bool KeyDown(Key k)
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current[k].wasPressedThisFrame;
#else
        return false;
#endif
    }

    // ───── UI building ───────────────────────────────────────────────────────
    void BuildSprites()
    {
        _spriteBg     = MRUI.RoundedRect(512, 512, 48, Color.white);
        _spriteTile   = MRUI.RoundedRect(256, 256, 36, Color.white);
        _spriteButton = MRUI.RoundedRect(256, 96, 40, Color.white);
        _spriteShadow = MRUI.SoftShadow(256, 256, 36, 18, new Color(0, 0, 0, 0.35f));
        _spriteCircle = MRUI.RoundedRect(64, 64, 32, Color.white);
    }

    void BuildUI()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var canvasGO = new GameObject("MenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGO.transform.SetParent(transform, false);
        _canvas = canvasGO.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 4f;
        _canvasRect = canvasGO.GetComponent<RectTransform>();
        _canvasRect.sizeDelta = new Vector2(1400, 900);
        _canvasRect.localScale = Vector3.one * 0.001f;
        _canvasRect.localPosition = Vector3.zero;

        var bg = MRUI.NewImage("Background", _canvasRect, MRUI.BG_CREAM, _spriteBg);
        MRUI.Stretch(bg.rectTransform);

        var top = MRUI.NewImage("TopFade", _canvasRect, MRUI.BG_TOP_FADE, _spriteTile);
        var trt = top.rectTransform;
        trt.anchorMin = new Vector2(0, 1); trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -20);
        trt.sizeDelta = new Vector2(-80, 130);

        BuildExplanationPanel();
        BuildSimulationPanel();
        BuildChallengePanel();
        BuildCorrectionPanel();
    }

    void BuildExplanationPanel()
    {
        _explanationPanel = MRUI.Panel("ExplanationPanel", _canvasRect);
        MRUI.Stretch(_explanationPanel.GetComponent<RectTransform>());

        _explTitle = MRUI.NewText("Title", _explanationPanel.transform, handicapTitle, 70, MRUI.TEXT_DARK, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        MRUI.Anchor(_explTitle.rectTransform, new Vector2(0.40f, 0.78f), new Vector2(0.95f, 0.92f));
        _explTitle.color = MRUI.Darken(accent, 0.40f);

        var underline = MRUI.NewImage("Underline", _explanationPanel.transform, accent, _spriteTile);
        MRUI.Anchor(underline.rectTransform, new Vector2(0.40f, 0.76f), new Vector2(0.50f, 0.77f));

        _explBody = MRUI.NewText("Body", _explanationPanel.transform, explanation, 30, MRUI.TEXT_DARK, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        MRUI.Anchor(_explBody.rectTransform, new Vector2(0.40f, 0.20f), new Vector2(0.94f, 0.74f));
        _explBody.textWrappingMode = TextWrappingModes.Normal;
        _explBody.lineSpacing = 14;

        var hint = MRUI.NewText("Hint", _explanationPanel.transform,
            "<b>A</b> / <b>gâchette</b>  démarrer  >    ·    <b>B</b>  retour    ·    <b>Grip</b>  saisir l'œil",
            24, MRUI.TEXT_GRAY, FontStyles.Normal, TextAlignmentOptions.Center);
        MRUI.Anchor(hint.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.10f));
    }

    void BuildSimulationPanel()
    {
        _simulationPanel = MRUI.Panel("SimulationPanel", _canvasRect);
        MRUI.Stretch(_simulationPanel.GetComponent<RectTransform>());

        var card = MRUI.NewImage("Card", _simulationPanel.transform, new Color(1f, 1f, 1f, 0.92f), _spriteTile);
        MRUI.Anchor(card.rectTransform, new Vector2(0.20f, 0.30f), new Vector2(0.80f, 0.70f));

        var title = MRUI.NewText("Title", card.transform, "Simulation active", 56, MRUI.TEXT_DARK, FontStyles.Bold, TextAlignmentOptions.Center);
        MRUI.Anchor(title.rectTransform, new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.90f));
        title.color = MRUI.Darken(accent, 0.40f);

        _simHint = MRUI.NewText("Body", card.transform,
            "Regardez autour de vous pour <b>ressentir l'effet</b>.\n\nQuand vous êtes prêt, appuyez sur <b>A</b> pour lancer le défi.",
            30, MRUI.TEXT_DARK, FontStyles.Normal, TextAlignmentOptions.Center);
        MRUI.Anchor(_simHint.rectTransform, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.62f));
        _simHint.textWrappingMode = TextWrappingModes.Normal;
        _simHint.lineSpacing = 12;

        var hint = MRUI.NewText("Hint", _simulationPanel.transform,
            "<b>A</b>  défi  >    ·    <b>B</b>  retour    ·    <b>Y</b>  inverser handicap    ·    <b>X</b>  cacher",
            22, MRUI.TEXT_GRAY, FontStyles.Normal, TextAlignmentOptions.Center);
        MRUI.Anchor(hint.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.10f));
    }

    void BuildChallengePanel()
    {
        _challengePanel = MRUI.Panel("ChallengePanel", _canvasRect);
        MRUI.Stretch(_challengePanel.GetComponent<RectTransform>());

        var hudBg = MRUI.NewImage("HUDBg", _challengePanel.transform, new Color(0f, 0f, 0f, 0.55f), _spriteButton);
        MRUI.Anchor(hudBg.rectTransform, new Vector2(0.72f, 0.88f), new Vector2(0.97f, 0.965f));

        _challengeTimerText = MRUI.NewText("TimerText", hudBg.transform, "0.00s", 38, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        MRUI.Stretch(_challengeTimerText.rectTransform);

        var instrBg = MRUI.NewImage("InstructionsBg", _challengePanel.transform, new Color(1f, 1f, 1f, 0.85f), _spriteTile);
        MRUI.Anchor(instrBg.rectTransform, new Vector2(0.06f, 0.04f), new Vector2(0.94f, 0.18f));

        _challengeInstr = MRUI.NewText("Instructions", instrBg.transform, challengeInstructions, 28, MRUI.TEXT_DARK, FontStyles.Normal, TextAlignmentOptions.Center);
        MRUI.Anchor(_challengeInstr.rectTransform, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.90f));
        _challengeInstr.textWrappingMode = TextWrappingModes.Normal;

        var hideHint = MRUI.NewText("HideHint", _challengePanel.transform,
            "<b>Y</b>  inverser handicap    ·    <b>X</b>  cacher / montrer",
            22, new Color(1f, 1f, 1f, 0.85f), FontStyles.Normal, TextAlignmentOptions.Center);
        MRUI.Anchor(hideHint.rectTransform, new Vector2(0.06f, 0.20f), new Vector2(0.94f, 0.24f));
    }

    void BuildCorrectionPanel()
    {
        _correctionPanel = MRUI.Panel("CorrectionPanel", _canvasRect);
        MRUI.Stretch(_correctionPanel.GetComponent<RectTransform>());

        _corrTitle = MRUI.NewText("Title", _correctionPanel.transform, "Comment c'est corrigé ?", 56, MRUI.TEXT_DARK, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        MRUI.Anchor(_corrTitle.rectTransform, new Vector2(0.06f, 0.83f), new Vector2(0.95f, 0.94f));
        _corrTitle.color = MRUI.Darken(accent, 0.40f);

        var underline = MRUI.NewImage("Underline", _correctionPanel.transform, accent, _spriteTile);
        MRUI.Anchor(underline.rectTransform, new Vector2(0.06f, 0.815f), new Vector2(0.18f, 0.825f));

        var bodyBg = MRUI.NewImage("BodyBg", _correctionPanel.transform, new Color(1f, 1f, 1f, 0.85f), _spriteTile);
        MRUI.Anchor(bodyBg.rectTransform, new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.78f));

        _corrBody = MRUI.NewText("Body", bodyBg.transform, correctionText, 30, MRUI.TEXT_DARK, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        MRUI.Anchor(_corrBody.rectTransform, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
        _corrBody.textWrappingMode = TextWrappingModes.Normal;
        _corrBody.lineSpacing = 12;

        var btnGO = new GameObject("ToggleButton", typeof(RectTransform));
        btnGO.transform.SetParent(_correctionPanel.transform, false);
        var brt = btnGO.GetComponent<RectTransform>();
        MRUI.Anchor(brt, new Vector2(0.27f, 0.13f), new Vector2(0.73f, 0.24f));

        var btnShadow = MRUI.NewImage("Shadow", brt, new Color(0, 0, 0, 0.25f), _spriteShadow);
        var bsr = btnShadow.rectTransform;
        bsr.anchorMin = Vector2.zero; bsr.anchorMax = Vector2.one;
        bsr.offsetMin = new Vector2(-8, -16); bsr.offsetMax = new Vector2(8, 4);

        _corrToggleBg = MRUI.NewImage("Bg", brt, MRUI.BUTTON_BLUE, _spriteButton);
        MRUI.Stretch(_corrToggleBg.rectTransform);

        _corrToggleLabel = MRUI.NewText("Label", brt, ">  Activer la correction", 34, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
        MRUI.Stretch(_corrToggleLabel.rectTransform);

        _corrHint = MRUI.NewText("Hint", _correctionPanel.transform,
            "<b>A</b> / <b>gâchette</b>  activer la correction    ·    <b>B</b>  retour    ·    <b>X</b>  cacher",
            22, MRUI.TEXT_GRAY, FontStyles.Normal, TextAlignmentOptions.Center);
        MRUI.Anchor(_corrHint.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.10f));
    }

    void BuildAnatomy()
    {
        var go = new GameObject("EyeAnatomy");
        go.SetActive(false);
        go.transform.SetParent(transform, false);
        // Position d'origine : sur le panel, à gauche, légèrement en avant.
        // Le force-grab permet de l'attirer dans la main même de loin.
        go.transform.localPosition = new Vector3(-0.42f, 0.05f, -0.15f);
        go.transform.localRotation = Quaternion.Euler(0, -20f, 0);
        go.transform.localScale = Vector3.one * 0.10f;

        _anatomy = go.AddComponent<EyeAnatomyView>();
        _anatomy.anatomyModelPrefab = eyeModelPrefab;
        _anatomy.diseasedScaleFactor = eyeDiseasedScaleFactor;
        if (System.Enum.TryParse<EyeAnatomyView.Part>(anatomyPart, true, out var diseased))
            _anatomy.diseasedPart = diseased;

        // Permet de saisir l'œil avec la gâchette latérale d'une manette
        go.AddComponent<EyeGrabbable>();

        go.SetActive(true);
        _anatomy.Highlight(anatomyPart);
    }

    // ───── Head follow (same logic as the menu) ──────────────────────────────
    protected void ResolveHeadAnchor()
    {
        if (headAnchor != null) return;
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null) headAnchor = rig.centerEyeAnchor;
        if (headAnchor == null && Camera.main != null) headAnchor = Camera.main.transform;
    }

    /// <summary>
    /// Returns headAnchor.position, sanitised. When OVR has no active session
    /// (editor without a connected headset) the anchor can report absurd
    /// values; in that case we fall back to a sane head-height position.
    /// </summary>
    protected Vector3 SafeHeadPosition()
    {
        if (headAnchor == null) return new Vector3(0, 1.6f, 0);
        var p = headAnchor.position;
        if (float.IsNaN(p.x) || float.IsInfinity(p.x) || Mathf.Abs(p.x) > 100f) p.x = 0f;
        if (float.IsNaN(p.y) || float.IsInfinity(p.y) || Mathf.Abs(p.y) > 100f) p.y = 1.6f;
        if (float.IsNaN(p.z) || float.IsInfinity(p.z) || Mathf.Abs(p.z) > 100f) p.z = 0f;
        return p;
    }

    protected Vector3 SafeHeadForward()
    {
        if (headAnchor == null) return Vector3.forward;
        var f = headAnchor.forward;
        if (float.IsNaN(f.x) || float.IsInfinity(f.x)) return Vector3.forward;
        if (f.sqrMagnitude < 1e-4f) return Vector3.forward;
        return f;
    }

    /// <summary>
    /// Parent the simulation root to the head anchor with a fixed local offset.
    /// The panel becomes head-locked: it stays in front of the user no matter
    /// what OVR reports for head position. Avoids drift / chase issues entirely.
    /// </summary>
    void AttachToHead()
    {
        if (headAnchor == null) return;
        transform.SetParent(headAnchor, false);
        transform.localPosition = new Vector3(0f, verticalOffset, distance);
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
    }
}
