using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Nystagmus simulation based on real medical waveforms.
// - Jerk type (default, ~80% of clinical cases): slow drift phase + fast corrective saccade.
//   Asymmetric sawtooth-like waveform. Brief foveation pause at end of slow phase.
// - Pendular type: smooth sinusoidal back-and-forth (toggle for educational comparison).
// - Predominantly horizontal (yaw). Vertical/torsion components are tiny and only appear
//   at high intensities, since pure vertical/torsion nystagmus indicates brain pathology.
// - Motion smear during fast phase suggests perceived oscillopsia.
public class NystagmusSim : HandicapSimulationBase
{
    public enum NystagmusType { Jerk, Pendular }
    public enum BeatDirection { RightBeating, LeftBeating }

    [Header("Contrôle")]
    public OVRInput.Axis2D intensityAxis = OVRInput.Axis2D.SecondaryThumbstick;
    [Range(0f, 1f)] public float intensity = 0.30f;
    [Range(0.1f, 2f)] public float intensitySpeed = 0.50f;
    [Range(0f, 0.40f)] public float stickDeadZone = 0.15f;

    [Header("Cibles visuelles (cubes + lettres)")]
    [Tooltip("Bouton qui cache/montre uniquement les cubes et la grille de lettres.")]
    public OVRInput.Button toggleTargetsButton = OVRInput.Button.SecondaryThumbstick;
    [Tooltip("Racine des cibles visuelles (cubes + Snellen). Auto-résolu via le nom 'VisualTargets' si vide.")]
    public GameObject visualTargetsRoot;

    [Header("Forme d'onde")]
    public NystagmusType type = NystagmusType.Jerk;
    public BeatDirection beatDirection = BeatDirection.RightBeating;
    [Range(1f, 7f)] public float frequencyHz = 4.0f;
    [Range(1f, 15f)] public float maxAmplitudeYawDeg = 8.0f;
    [Range(0.5f, 0.95f)] public float slowPhaseFraction = 0.80f;
    [Range(0f, 1f)] public float foveationPauseAmount = 0.55f;
    [Range(0f, 2f)] public float verticalContributionDeg = 0.6f;
    [Range(0f, 2f)] public float torsionalContributionDeg = 0.4f;
    [Range(0f, 2f)] public float naturalTremorDeg = 0.30f;
    public bool cameraTremorOnly = true;

    [Header("Perception (motion smear)")]
    public bool useMotionSmear = false;
    [Range(0.20f, 0.80f)] public float smearDistance = 0.34f;
    public Vector2 smearScale = new Vector2(1.85f, 1.12f);
    [Range(0f, 1f)] public float smearMaxAlpha = 0.24f;
    [Range(0f, 0.08f)] public float visibleFieldJitterMeters = 0.035f;

    Transform _trackingSpace;
    Quaternion _trackingBaseRotation = Quaternion.identity;
    bool _effectActive;
    float _phaseTime;
    float _lastYawAngle;
    float _smearAlpha;
    float _waveform;
    float _retinalSlip01;
    bool _fastPhase;
    float _lastHintIntensity = -1f;
    float _lastTargetsToggleTime = -10f;

    GameObject _smearRoot;
    Material _smearMaterial;

    void Reset() => ApplyProfile();

    protected override void Awake()
    {
        ApplyProfile();
        base.Awake();
    }

    protected override void OnSetup()
    {
        ResolveTrackingSpace();
        ResolveVisualTargets();
        BuildMotionSmear();
        SetSmearVisible(false);
        RefreshHint(force: true);
    }

    void ResolveVisualTargets()
    {
        if (visualTargetsRoot != null) return;
        visualTargetsRoot = GameObject.Find("VisualTargets");
    }

    protected override void OnSimulationStart()
    {
        _effectActive = true;
        _phaseTime = 0f;
        SetSmearVisible(false);
        RefreshHint(force: true);
    }

    protected override void OnSimulationStop()
    {
        _effectActive = false;
        SetSmearVisible(false);
        RestoreTrackingSpace();
    }

    protected override void Update()
    {
        base.Update();
        UpdateIntensityInput();
        UpdateTargetsToggleInput();
        UpdateOscillation();
        UpdateSmear();
    }

    void UpdateTargetsToggleInput()
    {
        if (visualTargetsRoot == null) ResolveVisualTargets();
        if (visualTargetsRoot == null) return;

        bool down = false;
        try { down = OVRInput.GetDown(toggleTargetsButton); } catch { }
#if ENABLE_INPUT_SYSTEM
        if (!down && Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame) down = true;
#endif
        if (!down) return;
        if (Time.unscaledTime - _lastTargetsToggleTime < 0.30f) return;
        _lastTargetsToggleTime = Time.unscaledTime;

        visualTargetsRoot.SetActive(!visualTargetsRoot.activeSelf);
        HapticFeedback.Tap(OVRInput.Controller.RTouch);
    }

    void OnDisable()
    {
        SetSmearVisible(false);
        RestoreTrackingSpace();
    }

    // ---------------- Tracking Space ----------------

    void ResolveTrackingSpace()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null)
        {
            _trackingSpace = rig.trackingSpace;
            if (_trackingSpace == null) _trackingSpace = rig.transform.Find("TrackingSpace");
        }
        if (_trackingSpace == null && Camera.main != null) _trackingSpace = Camera.main.transform;
        if (_trackingSpace != null) _trackingBaseRotation = _trackingSpace.localRotation;
        else Debug.LogWarning("[NystagmusSim] TrackingSpace introuvable — oscillation caméra inactive.");
    }

    void RestoreTrackingSpace()
    {
        if (_trackingSpace != null) _trackingSpace.localRotation = _trackingBaseRotation;
    }

    // ---------------- Input ----------------

    void UpdateIntensityInput()
    {
        if (CurrentPhase != Phase.Simulation && CurrentPhase != Phase.Challenge) return;
        Vector2 axis = AxisSafe(intensityAxis);
        float ay = axis.y;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.isPressed) ay += 1f;
            if (Keyboard.current.gKey.isPressed) ay -= 1f;
        }
#endif
        if (Mathf.Abs(ay) < stickDeadZone) return;
        float prev = intensity;
        intensity = Mathf.Clamp01(intensity + ay * intensitySpeed * Time.deltaTime);
        if (Mathf.Abs(intensity - prev) > 0.001f) RefreshHint(force: false);
    }

    // ---------------- Oscillation (core medical accuracy) ----------------

    void UpdateOscillation()
    {
        if (_trackingSpace == null) return;
        Quaternion offset = Quaternion.identity;

        if (_effectActive)
        {
            _phaseTime += Time.deltaTime;
            float period = 1f / Mathf.Max(0.1f, frequencyHz);
            float phase = (_phaseTime % period) / period;          // 0..1
            _fastPhase = type == NystagmusType.Jerk && phase >= Mathf.Clamp(slowPhaseFraction, 0.50f, 0.95f);
            float ampNorm = NystagmusWaveform(phase);              // -1..1, asymmetric for jerk
            _waveform = ampNorm;
            float yawDeg = ampNorm * maxAmplitudeYawDeg * intensity;
            float pitchDeg;
            float rollDeg;

            if (cameraTremorOnly)
            {
                float w = _phaseTime * Mathf.PI * 2f * frequencyHz;
                float horizontalShake = Mathf.Sin(w) * 0.72f + Mathf.Sin(w * 2.25f + 0.8f) * 0.28f;
                float verticalShake = Mathf.Sin(w * 1.35f + 1.7f);
                yawDeg = horizontalShake * maxAmplitudeYawDeg * intensity;
                pitchDeg = verticalShake * verticalContributionDeg * intensity;
                rollDeg = Mathf.Sin(w * 0.75f + 0.3f) * torsionalContributionDeg * intensity;
                _waveform = horizontalShake;
                _fastPhase = Mathf.Abs(horizontalShake) > 0.72f;
            }
            else
            {
                // Subtle vertical: a small in-phase sine (only visible at high intensity)
                float vertPhase = Mathf.Sin(_phaseTime * Mathf.PI * 2f * frequencyHz + 0.7f);
                pitchDeg = vertPhase * verticalContributionDeg * Mathf.Pow(intensity, 1.3f);

                // Subtle torsion (small low-freq): even smaller, lower frequency
                float torPhase = Mathf.Sin(_phaseTime * Mathf.PI * 2f * frequencyHz * 0.55f + 1.3f);
                rollDeg = torPhase * torsionalContributionDeg * Mathf.Pow(intensity, 1.3f);
            }

            if (beatDirection == BeatDirection.LeftBeating) yawDeg = -yawDeg;

            // Natural ocular tremor: high-frequency low-amplitude noise (~30-80 Hz physiologic),
            // here approximated with bandlimited multi-octave sine.
            float tremor = Mathf.Sin(_phaseTime * 53f) * 0.6f
                         + Mathf.Sin(_phaseTime * 91f + 0.7f) * 0.4f;
            yawDeg += tremor * naturalTremorDeg * intensity * 0.5f;

            // Retinal slip estimate from angular velocity. No grey overlay is shown by default.
            float yawDelta = Mathf.Abs(yawDeg - _lastYawAngle);
            _lastYawAngle = yawDeg;
            _retinalSlip01 = Mathf.Clamp01(yawDelta * frequencyHz / Mathf.Max(0.001f, maxAmplitudeYawDeg * 0.55f));
            float saccadeIntensity = Mathf.Clamp01(_retinalSlip01 + (_fastPhase ? 0.35f : 0f));
            _smearAlpha = Mathf.Lerp(_smearAlpha, saccadeIntensity, Time.deltaTime * 22f);

            offset = Quaternion.Euler(pitchDeg, yawDeg, rollDeg);
        }
        else
        {
            _smearAlpha = Mathf.Lerp(_smearAlpha, 0f, Time.deltaTime * 10f);
            _retinalSlip01 = Mathf.Lerp(_retinalSlip01, 0f, Time.deltaTime * 8f);
        }

        // Apply directly (no smoothing) — nystagmus is fast, smoothing kills the saccade
        _trackingSpace.localRotation = _trackingBaseRotation * offset;
    }

    // Returns -1..1. For Jerk: asymmetric (slow drift then fast jump back, with foveation pause).
    // For Pendular: pure sine.
    float NystagmusWaveform(float phase)
    {
        if (type == NystagmusType.Pendular)
            return Mathf.Sin(phase * Mathf.PI * 2f);

        // Jerk: drift slowly from -1 to +1 during slow phase, then snap back during fast phase.
        // Slow phase uses an ease-out curve so velocity decreases approaching the foveation
        // pause near the end (brief stable window before the saccade).
        float sp = Mathf.Clamp(slowPhaseFraction, 0.50f, 0.95f);
        if (phase < sp)
        {
            float t = phase / sp;                       // 0..1
            // Quadratic ease-out to slow down before the saccade (foveation pause)
            float eased = 1f - Mathf.Pow(1f - t, 1f + foveationPauseAmount * 3f);
            return Mathf.Lerp(-1f, 1f, eased);
        }
        else
        {
            float t = (phase - sp) / (1f - sp);         // 0..1
            // Smooth-step the fast phase to avoid infinite acceleration (clipping)
            float eased = Mathf.SmoothStep(0f, 1f, t);
            return Mathf.Lerp(1f, -1f, eased);
        }
    }

    // ---------------- Motion Smear Overlay ----------------

    void BuildMotionSmear()
    {
        if (!useMotionSmear || _smearRoot != null) return;
        EnsureHeadAnchor();
        Transform anchor = headAnchor != null ? headAnchor : transform;

        _smearRoot = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _smearRoot.name = "NystagmusMotionSmear";
        var col = _smearRoot.GetComponent<Collider>();
        if (col != null) Destroy(col);
        _smearRoot.transform.SetParent(anchor, false);
        _smearRoot.transform.localPosition = new Vector3(0f, 0f, smearDistance);
        _smearRoot.transform.localScale = new Vector3(smearScale.x, smearScale.y, 1f);

        Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        _smearMaterial = new Material(sh);
        _smearMaterial.name = "NystagmusSmearMaterial";
        _smearMaterial.renderQueue = 4500;
        _smearMaterial.SetOverrideTag("RenderType", "Transparent");
        _smearMaterial.mainTexture = MakeSmearTexture(384);
        if (_smearMaterial.HasProperty("_BaseMap")) _smearMaterial.SetTexture("_BaseMap", _smearMaterial.mainTexture);
        if (_smearMaterial.HasProperty("_BaseColor")) _smearMaterial.SetColor("_BaseColor", new Color(1, 1, 1, 0));
        if (_smearMaterial.HasProperty("_Surface")) _smearMaterial.SetFloat("_Surface", 1f);
        if (_smearMaterial.HasProperty("_ZWrite")) _smearMaterial.SetFloat("_ZWrite", 0f);
        if (_smearMaterial.HasProperty("_SrcBlend")) _smearMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (_smearMaterial.HasProperty("_DstBlend")) _smearMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _smearMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        Renderer rend = _smearRoot.GetComponent<Renderer>();
        rend.sharedMaterial = _smearMaterial;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
    }

    void SetSmearVisible(bool visible)
    {
        if (_smearRoot != null) _smearRoot.SetActive(visible && useMotionSmear);
    }

    void UpdateSmear()
    {
        if (_smearMaterial == null) return;
        float alpha = Mathf.Clamp01((_smearAlpha * 0.75f + _retinalSlip01 * 0.25f) * smearMaxAlpha * Mathf.Pow(intensity, 0.75f));
        Color c = new Color(0.80f, 0.88f, 0.90f, alpha);
        _smearMaterial.color = c;
        if (_smearMaterial.HasProperty("_BaseColor")) _smearMaterial.SetColor("_BaseColor", c);
        if (_smearRoot != null)
        {
            float x = _waveform * visibleFieldJitterMeters * intensity;
            float y = Mathf.Sin(_phaseTime * Mathf.PI * 2f * frequencyHz + 0.6f) * visibleFieldJitterMeters * 0.18f * intensity;
            _smearRoot.transform.localPosition = new Vector3(x, y, smearDistance);
            _smearRoot.transform.localRotation = Quaternion.Euler(0f, 0f, _waveform * 1.4f * intensity);
        }
    }

    Texture2D MakeSmearTexture(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f - center.x) / center.x;
                float ny = (y + 0.5f - center.y) / center.y;
                float vignette = Mathf.SmoothStep(1f, 0f, Mathf.Sqrt(nx * nx + ny * ny) * 0.88f);
                float horizontalSmear = Mathf.SmoothStep(1f, 0f, Mathf.Abs(ny) * 1.55f);
                float verticalEdges = 0.55f + 0.45f * Mathf.Sin((nx + 1.0f) * 34f);
                float fineEdges = 0.50f + 0.50f * Mathf.Sin((nx + ny * 0.12f) * 91f);
                float edgeField = Mathf.Pow(Mathf.Clamp01(verticalEdges * 0.65f + fineEdges * 0.35f), 2.2f);
                float a = horizontalSmear * vignette * (0.22f + edgeField * 0.55f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    // ---------------- Profile + Hint ----------------

    void RefreshHint(bool force)
    {
        if (!force && Mathf.Abs(_lastHintIntensity - intensity) < 0.02f) return;
        _lastHintIntensity = intensity;
        int pct = Mathf.RoundToInt(intensity * 100f);
        SetSimulationHint(
            $"<b>Intensité : {pct}%</b>\n\n" +
            "Stick droit ↕ (ou H / G)  ajuster\n" +
            "Clic stick droit / I  cacher cibles");
    }

    void ApplyProfile()
    {
        intensity = Mathf.Clamp(intensity, 0.30f, 0.42f);
        frequencyHz = 4.0f;
        maxAmplitudeYawDeg = 8.0f;
        slowPhaseFraction = 0.80f;
        foveationPauseAmount = 0.55f;
        cameraTremorOnly = true;
        useMotionSmear = false;
        smearMaxAlpha = 0.24f;
        visibleFieldJitterMeters = 0.035f;
        handicapTitle = "Nystagmus";
        explanation =
            "Mouvements oculaires involontaires et rythmiques. " +
            "La simulation combine amplitude et fréquence : plus elles montent, plus la vision perd en stabilité et en acuité.";
        correctionText =
            "Prise en charge :\n\n" +
            "- correction optique (prismes pour rapprocher le point neutre)\n" +
            "- rééducation orthoptique\n" +
            "- toxine botulique des muscles oculomoteurs (cas sélectionnés)\n" +
            "- chirurgie de Anderson-Kestenbaum pour déplacer le point neutre\n\n" +
            "Diagnostic : examen ophtalmo, vidéo-oculographie, bilan neurologique selon l'origine.";
        anatomyPart = "OcularMuscles";
        accent = new Color(0.280f, 0.620f, 0.720f, 1f);
        menuSceneName = "SampleScene";
    }

    static Vector2 AxisSafe(OVRInput.Axis2D axis)
    {
        try
        {
            Vector2 primary = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.All);
            Vector2 secondary = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick, OVRInput.Controller.All);
            Vector2 current = OVRInput.Get(axis, OVRInput.Controller.All);
            Vector2 best = secondary.sqrMagnitude >= primary.sqrMagnitude ? secondary : primary;
            return best.sqrMagnitude >= current.sqrMagnitude ? best : current;
        }
        catch { return Vector2.zero; }
    }
}
