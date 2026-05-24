using System.Collections.Generic;
using UnityEngine;

public class GlaucomeSim : HandicapSimulationBase
{
    [Header("Vision tubulaire (hardcore)")]
    [Tooltip("Rayon du noyau de vision la moins obscurcie (très petit = on devine à peine le centre).")]
    [Range(0.01f, 0.5f)] public float severeRadius = 0.04f;
    [Tooltip("Rayon équivalent quand la correction est active.")]
    [Range(0.10f, 0.7f)] public float correctedRadius = 0.42f;
    [Tooltip("Largeur du fondu tunnel→noir. Petit = tunnel très serré, opaque vite.")]
    [Range(0.04f, 0.60f)] public float feather = 0.10f;
    [Tooltip("Opacité maximale du voile au centre.")]
    [Range(0f, 1f)] public float hazeOpacity = 0.90f;
    [Tooltip("Opacité du voile quand la correction est active.")]
    [Range(0f, 1f)] public float correctedHazeOpacity = 0.08f;
    public float vignetteDistance = 0.30f;
    public Vector2 vignetteScale = new Vector2(1.7f, 1.7f);

    [Header("Défi : tracé du chemin")]
    [Tooltip("Liste des points (x = droite, y = avant) en mètres relatifs au point de départ. Le dernier waypoint = arrivée. Crée des virages en changeant x entre deux points.")]
    public List<Vector2> pathWaypoints = new List<Vector2>();
    [Tooltip("Distance d'arrivée pour valider (m).")]
    public float arrivalRadius = 0.45f;

    [Header("Défi : géométrie des dalles")]
    [Tooltip("Largeur de la bande (m).")]
    public float pathWidth = 0.32f;
    [Tooltip("Longueur d'une dalle dans le sens de la marche (m).")]
    public float tileDepth = 0.22f;
    [Tooltip("Espacement entre dalles consécutives, centre à centre (m).")]
    public float tileSpacing = 0.26f;
    [Tooltip("Nombre de barres surélevées (stries) par dalle.")]
    [Range(3, 8)] public int stripesPerTile = 5;
    [Tooltip("Largeur d'une strie (m).")]
    public float stripeWidth = 0.022f;
    [Tooltip("Hauteur d'une strie au-dessus de la base (m).")]
    public float stripeHeight = 0.012f;
    [Tooltip("Couleur de la bande.")]
    public Color pathColor = new Color(0.72f, 0.60f, 0.18f, 1f);
    [Tooltip("Couleur du repère d'arrivée.")]
    public Color goalColor = new Color(0.25f, 0.65f, 0.32f, 1f);
    [Tooltip("Émission de la base de la dalle (0 = pas d'auto-illumination).")]
    [Range(0f, 2f)] public float baseEmission = 0.0f;
    [Tooltip("Émission des stries (0 = neutre, >1 = brille fort).")]
    [Range(0f, 2f)] public float stripeEmission = 0.0f;
    [Tooltip("Émission du pilier d'arrivée (laissé un peu plus visible que le chemin).")]
    [Range(0f, 2f)] public float goalEmission = 0.50f;

    [Header("Canne d'aveugle (main droite)")]
    public OVRInput.Button caneButton = OVRInput.Button.PrimaryIndexTrigger;
    public OVRInput.Controller caneController = OVRInput.Controller.RTouch;
    public float caneLength = 1.20f;
    public float caneDiameter = 0.020f;
    [Tooltip("Intensité du retour haptique au contact d'une strie (0..1). Bas = réaliste.")]
    [Range(0f, 1f)] public float caneHapticAmplitude = 0.22f;
    [Tooltip("Durée d'une impulsion haptique (s).")]
    [Range(0.005f, 0.20f)] public float caneHapticDuration = 0.030f;
    [Tooltip("Fréquence (timbre) de la vibration. 0.3 = sourd, 0.8 = aigu.")]
    [Range(0f, 1f)] public float caneHapticFrequency = 0.45f;

    GameObject _vignette;
    Material _vignetteMat;
    Texture2D _vignetteTex;

    GameObject _challengeRoot;
    GameObject _caneRoot;
    Vector3 _pointBLocal;
    bool _caneVisible;
    float _lastCaneHitTime;
    float _activeHaze;

    static readonly Collider[] _overlapBuf = new Collider[16];

    Material _baseMat, _stripeMat, _goalMat;
    Material _caneShaftMat, _caneTipMat;

    void Reset()
    {
        handicapTitle = "Glaucome";
        explanation =
            "Le <b>glaucome</b> attaque progressivement le <b>nerf optique</b>, qui transmet l'image au cerveau.\n\n" +
            "La vision périphérique se rétrécit en <b>vision tubulaire</b> : on ne voit plus que le centre, comme à travers un tube. " +
            "Sans traitement, la perte gagne le centre et peut conduire à la cécité.";
        correctionText =
            "Le glaucome ne se guérit pas, mais on peut <b>arrêter sa progression</b> :\n\n" +
            "<b>•</b> Collyres pour faire baisser la pression intra-oculaire (1ère ligne)\n" +
            "<b>•</b> Laser (trabéculoplastie) pour drainer l'œil\n" +
            "<b>•</b> Chirurgie quand les autres traitements ne suffisent plus\n\n" +
            "<i>Plus le diagnostic est précoce, mieux la vision restante est préservée.</i>";
        anatomyPart = "OpticNerve";
        accent = new Color(0.13f, 0.67f, 1.00f, 1f);
        challengeKey = "glaucome";
        eyeDiseasedScaleFactor = new Vector3(0.40f, 1f, 0.40f);

        pathWaypoints = DefaultZigzag();
    }

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(handicapTitle) || handicapTitle == "Handicap") Reset();
        if (pathWaypoints == null || pathWaypoints.Count == 0)
            pathWaypoints = DefaultZigzag();
        base.Awake();
    }

    static List<Vector2> DefaultZigzag()
    {
        return new List<Vector2>
        {
            new Vector2( 0.0f,  2.0f),
            new Vector2( 1.8f,  2.0f),
            new Vector2( 1.8f,  4.0f),
            new Vector2( 0.0f,  4.0f),
            new Vector2( 0.0f,  6.0f),
            new Vector2(-1.8f,  6.0f),
            new Vector2(-1.8f,  8.0f),
            new Vector2( 0.0f,  8.0f),
            new Vector2( 0.0f, 10.5f),
        };
    }

    protected override void OnSetup()
    {
        EnsureHeadAnchor();
        BuildVignette();
        _activeHaze = hazeOpacity;
        SetVignetteVisible(false);
    }

    protected override void OnSimulationStart()
    {
        if (_vignette == null) BuildVignette();
        _activeHaze = hazeOpacity;
        SetVignetteParams(severeRadius, _activeHaze);
        SetVignetteVisible(true);
    }

    protected override void OnSimulationStop()
    {
        SetVignetteVisible(false);
    }

    protected override void OnChallengeStart()
    {
        BuildPath();
        BuildCane();
        SetChallengeStatus(
            "Suivez la <b>bande podotactile</b> au sol jusqu'au repère vert (avec virages).\n" +
            "Avancez <b>physiquement</b> dans la pièce. <b>Maintenez la gâchette droite</b> pour sortir la canne — elle vibre au contact des stries.");
    }

    protected override void OnChallengeStop()
    {
        ClearChallengeObjects();
    }

    protected override void OnCorrectionToggle(bool on)
    {
        _activeHaze = on ? correctedHazeOpacity : hazeOpacity;
        SetVignetteParams(on ? correctedRadius : severeRadius, _activeHaze);
    }

    protected override void Update()
    {
        base.Update();
        if (CurrentPhase != Phase.Challenge) return;
        UpdateCaneVisibility();
        UpdateCaneCollision();
        CheckArrival();
    }

    // ───── Construction du chemin ────────────────────────────────────────────
    void BuildPath()
    {
        EnsureHeadAnchor();
        var rig = FindAnyObjectByType<OVRCameraRig>();
        Transform space = rig != null ? rig.trackingSpace : null;

        Vector3 head = SafeHeadPosition();
        Vector3 fwd = SafeHeadForward(); fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        _challengeRoot = new GameObject("GlaucomeChallenge");

        // Ancrage : sous tracking space → Y local 0 = sol de la pièce (FloorLevel),
        // et le chemin reste solidaire de la pièce même si le rig est recalé.
        if (space != null)
        {
            _challengeRoot.transform.SetParent(space, false);

            Vector3 headLocal = space.InverseTransformPoint(head);
            _challengeRoot.transform.localPosition = new Vector3(headLocal.x, 0f, headLocal.z);

            Vector3 fwdLocal = space.InverseTransformDirection(fwd);
            fwdLocal.y = 0f;
            if (fwdLocal.sqrMagnitude < 1e-4f) fwdLocal = Vector3.forward;
            fwdLocal.Normalize();
            _challengeRoot.transform.localRotation = Quaternion.LookRotation(fwdLocal, Vector3.up);
        }
        else
        {
            _challengeRoot.transform.position = new Vector3(head.x, 0f, head.z);
            _challengeRoot.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        }

        _baseMat   = MakeLitMaterial(new Color(0.42f, 0.36f, 0.12f), baseEmission);
        _stripeMat = MakeLitMaterial(pathColor, stripeEmission);
        _goalMat   = MakeLitMaterial(goalColor, goalEmission);

        // Tout est désormais exprimé dans le repère local du challenge root :
        // +Z local = avant (direction du regard projeté au sol), +X = droite.
        Vector3 prev = Vector3.zero;
        for (int w = 0; w < pathWaypoints.Count; w++)
        {
            Vector2 wp = pathWaypoints[w];
            Vector3 cur = new Vector3(wp.x, 0f, wp.y);
            BuildSegmentLocal(prev, cur);
            prev = cur;
        }
        _pointBLocal = prev;

        SpawnGoalLocal(_pointBLocal);
    }

    void BuildSegmentLocal(Vector3 a, Vector3 b)
    {
        Vector3 d = b - a; d.y = 0f;
        float length = d.magnitude;
        if (length < 1e-3f) return;
        Vector3 dir = d / length;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

        int n = Mathf.Max(1, Mathf.RoundToInt(length / tileSpacing));
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) / n;
            Vector3 pos = Vector3.Lerp(a, b, t);
            SpawnGuidanceTileLocal(pos, rot);
        }
    }

    void SpawnGuidanceTileLocal(Vector3 localPos, Quaternion localRot)
    {
        // Base plate posée au sol (Y local ≈ 0).
        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.name = "TileBase";
        Destroy(basePlate.GetComponent<Collider>());
        basePlate.transform.SetParent(_challengeRoot.transform, false);
        basePlate.transform.localPosition = localPos + Vector3.up * 0.006f;
        basePlate.transform.localRotation = localRot;
        basePlate.transform.localScale = new Vector3(pathWidth, 0.012f, tileDepth);
        var br = basePlate.GetComponent<Renderer>();
        br.sharedMaterial = _baseMat;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        br.receiveShadows = false;

        int n = Mathf.Max(2, stripesPerTile);
        float usable = pathWidth - stripeWidth;
        for (int i = 0; i < n; i++)
        {
            float t = n > 1 ? i / (float)(n - 1) : 0.5f;
            float xLocal = Mathf.Lerp(-usable * 0.5f, usable * 0.5f, t);

            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "Stripe";
            stripe.transform.SetParent(_challengeRoot.transform, false);
            // Position en repère local de la dalle : décalage X local, posée sur la base.
            stripe.transform.localPosition = localPos + localRot * new Vector3(xLocal, 0.012f + stripeHeight * 0.5f, 0f);
            stripe.transform.localRotation = localRot;
            stripe.transform.localScale = new Vector3(stripeWidth, stripeHeight, tileDepth * 0.94f);
            stripe.AddComponent<PathTileMarker>();

            var sr = stripe.GetComponent<Renderer>();
            sr.sharedMaterial = _stripeMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
        }
    }

    void SpawnGoalLocal(Vector3 localPos)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "Goal";
        var col = pillar.GetComponent<Collider>(); if (col != null) Destroy(col);
        pillar.transform.SetParent(_challengeRoot.transform, false);
        pillar.transform.localPosition = localPos + Vector3.up * 0.60f;
        pillar.transform.localScale = new Vector3(0.20f, 1.10f, 0.20f);
        var pr = pillar.GetComponent<Renderer>();
        pr.sharedMaterial = _goalMat;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;

        BuildWarningTileLocal(localPos);
    }

    void BuildWarningTileLocal(Vector3 localPos)
    {
        var basePlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        basePlate.name = "WarningBase";
        Destroy(basePlate.GetComponent<Collider>());
        basePlate.transform.SetParent(_challengeRoot.transform, false);
        basePlate.transform.localPosition = localPos + Vector3.up * 0.006f;
        basePlate.transform.localScale = new Vector3(pathWidth * 1.4f, 0.012f, pathWidth * 1.4f);
        var br = basePlate.GetComponent<Renderer>();
        br.sharedMaterial = _baseMat;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        br.receiveShadows = false;

        int dotsPerSide = 5;
        float side = pathWidth * 1.25f;
        float dotDiameter = 0.030f;
        for (int ix = 0; ix < dotsPerSide; ix++)
        for (int iz = 0; iz < dotsPerSide; iz++)
        {
            float tx = (dotsPerSide > 1) ? ix / (float)(dotsPerSide - 1) : 0.5f;
            float tz = (dotsPerSide > 1) ? iz / (float)(dotsPerSide - 1) : 0.5f;
            float xLocal = Mathf.Lerp(-side * 0.5f, side * 0.5f, tx);
            float zLocal = Mathf.Lerp(-side * 0.5f, side * 0.5f, tz);

            var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.name = "Dot";
            dot.transform.SetParent(_challengeRoot.transform, false);
            dot.transform.localPosition = localPos + new Vector3(xLocal, 0.012f + dotDiameter * 0.25f, zLocal);
            dot.transform.localScale = new Vector3(dotDiameter, dotDiameter * 0.6f, dotDiameter);
            dot.AddComponent<PathTileMarker>();
            var dr = dot.GetComponent<Renderer>();
            dr.sharedMaterial = _stripeMat;
            dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dr.receiveShadows = false;
        }
    }

    // ───── Canne ─────────────────────────────────────────────────────────────
    void BuildCane()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null) return;
        Transform handAnchor = caneController == OVRInput.Controller.RTouch
            ? rig.rightHandAnchor : rig.leftHandAnchor;
        if (handAnchor == null) return;

        _caneRoot = new GameObject("VirtualCane");
        _caneRoot.transform.SetParent(handAnchor, false);
        // Origine collée à la manette, orientation strictement alignée sur
        // son axe local +Z : la canne suit exactement la direction pointée.
        _caneRoot.transform.localPosition = new Vector3(0f, -0.005f, 0.02f);
        _caneRoot.transform.localRotation = Quaternion.identity;

        _caneShaftMat = MakeUnlitMaterial(Color.white);
        _caneTipMat = MakeUnlitMaterial(new Color(0.85f, 0.10f, 0.10f));

        var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shaft.name = "Shaft";
        DestroyImmediate(shaft.GetComponent<Collider>());
        shaft.transform.SetParent(_caneRoot.transform, false);
        shaft.transform.localPosition = new Vector3(0, 0, caneLength * 0.5f);
        shaft.transform.localRotation = Quaternion.Euler(90, 0, 0);
        shaft.transform.localScale = new Vector3(caneDiameter, caneLength * 0.5f, caneDiameter);
        var sr = shaft.GetComponent<Renderer>();
        sr.sharedMaterial = _caneShaftMat;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;

        var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tip.name = "Tip";
        DestroyImmediate(tip.GetComponent<Collider>());
        tip.transform.SetParent(_caneRoot.transform, false);
        tip.transform.localPosition = new Vector3(0, 0, caneLength);
        tip.transform.localScale = Vector3.one * (caneDiameter * 2.5f);
        var tr = tip.GetComponent<Renderer>();
        tr.sharedMaterial = _caneTipMat;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        // Aucun Rigidbody / Collider : ils faisaient interpoler la canne entre
        // FixedUpdate, ce qui désynchronisait visuellement la canne de la
        // manette. La détection des stries se fait via Physics.OverlapSphere
        // dans Update (UpdateCaneCollision), donc la canne suit la pose de la
        // manette chaque frame, sans lag.
        _caneRoot.SetActive(false);
        _caneVisible = false;
    }

    void UpdateCaneCollision()
    {
        if (_caneRoot == null || !_caneVisible) return;
        Vector3 tipWorld = _caneRoot.transform.TransformPoint(new Vector3(0f, 0f, caneLength));
        float r = caneDiameter * 2.5f;
        int n = Physics.OverlapSphereNonAlloc(tipWorld, r, _overlapBuf, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < n; i++)
        {
            if (_overlapBuf[i] != null && _overlapBuf[i].GetComponent<PathTileMarker>() != null)
            {
                OnCaneTouchedPath();
                return;
            }
        }
    }

    void UpdateCaneVisibility()
    {
        if (_caneRoot == null) return;
        bool pressed = false;
        try { pressed = OVRInput.Get(caneButton, caneController); } catch { }
        if (pressed != _caneVisible)
        {
            _caneVisible = pressed;
            _caneRoot.SetActive(pressed);
        }
    }

    void OnCaneTouchedPath()
    {
        if (Time.unscaledTime - _lastCaneHitTime < 0.06f) return;
        _lastCaneHitTime = Time.unscaledTime;
        HapticFeedback.Pulse(caneController, caneHapticFrequency, caneHapticAmplitude, caneHapticDuration);
    }

    // ───── Arrivée ───────────────────────────────────────────────────────────
    void CheckArrival()
    {
        if (_challengeRoot == null) return;
        // Position physique du casque (centerEyeAnchor) ramenée dans le repère du chemin :
        // seul l'avancée réelle dans la pièce compte, pas une translation du rig.
        Vector3 head = SafeHeadPosition();
        Vector3 headLocal = _challengeRoot.transform.InverseTransformPoint(head);
        Vector2 a = new Vector2(headLocal.x, headLocal.z);
        Vector2 b = new Vector2(_pointBLocal.x, _pointBLocal.z);
        if (Vector2.Distance(a, b) <= arrivalRadius) EndChallenge();
    }

    void ClearChallengeObjects()
    {
        if (_caneRoot != null) { Destroy(_caneRoot); _caneRoot = null; }
        if (_challengeRoot != null) { Destroy(_challengeRoot); _challengeRoot = null; }
        if (_baseMat != null) { Destroy(_baseMat); _baseMat = null; }
        if (_stripeMat != null) { Destroy(_stripeMat); _stripeMat = null; }
        if (_goalMat != null) { Destroy(_goalMat); _goalMat = null; }
        if (_caneShaftMat != null) { Destroy(_caneShaftMat); _caneShaftMat = null; }
        if (_caneTipMat != null) { Destroy(_caneTipMat); _caneTipMat = null; }
        _caneVisible = false;
    }

    // ───── Vignette ──────────────────────────────────────────────────────────
    void BuildVignette()
    {
        EnsureHeadAnchor();
        var anchor = headAnchor != null ? headAnchor : transform;

        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "TubularVignette";
        var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
        q.transform.SetParent(anchor, false);
        q.transform.localPosition = new Vector3(0, 0, vignetteDistance);
        q.transform.localRotation = Quaternion.identity;
        q.transform.localScale = new Vector3(vignetteScale.x, vignetteScale.y, 1f);
        _vignette = q;

        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        if (sh == null) sh = Shader.Find("Sprites/Default");

        var m = new Material(sh);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        // Queue 2950 (juste avant Transparent = 3000) : la vignette est rendue
        // AVANT le canvas du menu (shaders UI ≈ 3000), donc l'UI passe par
        // dessus et n'est jamais obscurcie par le noir périphérique.
        m.renderQueue = 2950;
        m.color = Color.white;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        _vignetteMat = m;
    }

    void SetVignetteParams(float coreR, float hazeAlpha)
    {
        if (_vignetteMat == null) return;
        var tex = MakeVignetteTex(512, coreR, feather, hazeAlpha);
        if (_vignetteTex != null && _vignetteTex != tex) Destroy(_vignetteTex);
        _vignetteTex = tex;
        _vignetteMat.mainTexture = tex;
        if (_vignetteMat.HasProperty("_BaseMap")) _vignetteMat.SetTexture("_BaseMap", tex);
    }

    void SetVignetteVisible(bool v)
    {
        if (_vignette != null) _vignette.SetActive(v);
    }

    /// <summary>
    /// Vignette glaucome : sigmoïde radiale (vrai fondu, pas de palier), centre
    /// brouillé par un bruit trois octaves type verre dépoli pour rendre les
    /// détails non distinguables même dans la zone la moins sombre.
    /// </summary>
    static Texture2D MakeVignetteTex(int size, float coreR, float fadeWidth, float hazeAlpha)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[size * size];
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float maxDist = size * 0.5f;

        // Bruit de valeur, trois octaves : taches lentes + grains + texture fine.
        const int g1 = 10, g2 = 30, g3 = 84;
        var rng = new System.Random(1337);
        var n1 = new float[g1 + 1, g1 + 1];
        var n2 = new float[g2 + 1, g2 + 1];
        var n3 = new float[g3 + 1, g3 + 1];
        for (int j = 0; j <= g1; j++) for (int i = 0; i <= g1; i++) n1[i, j] = (float)rng.NextDouble();
        for (int j = 0; j <= g2; j++) for (int i = 0; i <= g2; i++) n2[i, j] = (float)rng.NextDouble();
        for (int j = 0; j <= g3; j++) for (int i = 0; i <= g3; i++) n3[i, j] = (float)rng.NextDouble();

        // Sigmoïde radiale (logistique) : tunnel ≈ 0 au centre, ≈ 1 au bord,
        // sans aucun palier. fadeWidth pilote la pente.
        float midR = Mathf.Clamp(coreR + Mathf.Max(0.10f, fadeWidth) * 0.9f, 0.10f, 0.80f);
        float steepness = Mathf.Lerp(14f, 4.5f, Mathf.InverseLerp(0.10f, 0.60f, fadeWidth));

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float u = (x + 0.5f) / size;
            float v = (y + 0.5f) / size;
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / maxDist;

            // Fondu continu via sigmoïde, forcé à 1.0 dès la fin de la zone
            // de fondu pour garantir une vignette totalement opaque sur les côtés.
            float tunnel = 1f / (1f + Mathf.Exp(-steepness * (d - midR)));
            float opaqueStart = midR + fadeWidth * 0.5f;
            float opaqueEnd   = midR + fadeWidth;
            tunnel = Mathf.Max(tunnel, Mathf.SmoothStep(opaqueStart, opaqueEnd, d));

            // Trois octaves de bruit pour un aspect verre dépoli + grain.
            float n = 0.50f * SampleNoise(n1, g1, u, v)
                    + 0.32f * SampleNoise(n2, g2, u, v)
                    + 0.18f * SampleNoise(n3, g3, u, v);
            n = Mathf.SmoothStep(0.15f, 0.90f, n);

            // Voile centré : pic d'opacité ≈ hazeAlpha au centre, décroissance
            // douce vers la périphérie où la vignette noire prend le relais.
            // Modulation par le bruit autour de 1.0 pour conserver le plafond.
            float radial = Mathf.SmoothStep(0f, 1f, d);
            float haze = hazeAlpha * Mathf.Lerp(1.0f, 0.55f, radial);
            haze *= 0.70f + 0.60f * n;
            haze = Mathf.Min(haze, hazeAlpha);
            haze = Mathf.Clamp01(haze);

            // Composition : voile gris d'abord, tunnel noir par-dessus.
            float finalA = 1f - (1f - haze) * (1f - tunnel);
            float rC = 0f, gC = 0f, bC = 0f;
            if (finalA > 1e-4f)
            {
                // Voile gris-bleu très sombre, légèrement modulé pour donner
                // un peu de variation chromatique (cataracte/scotome).
                float warm = 0.5f + 0.5f * SampleNoise(n1, g1, 1f - u, v);
                float hr = Mathf.Lerp(0.05f, 0.09f, warm);
                float hg = Mathf.Lerp(0.07f, 0.10f, warm);
                float hb = Mathf.Lerp(0.11f, 0.14f, 1f - warm);
                float w = haze * (1f - tunnel);
                rC = hr * w / finalA;
                gC = hg * w / finalA;
                bC = hb * w / finalA;
            }

            px[y * size + x] = new Color(rC, gC, bC, finalA);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static float SampleNoise(float[,] grid, int gridN, float u, float v)
    {
        float fx = u * gridN, fy = v * gridN;
        int ix = Mathf.Clamp(Mathf.FloorToInt(fx), 0, gridN - 1);
        int iy = Mathf.Clamp(Mathf.FloorToInt(fy), 0, gridN - 1);
        float tx = fx - ix, ty = fy - iy;
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);
        return Mathf.Lerp(Mathf.Lerp(grid[ix, iy], grid[ix + 1, iy], tx),
                          Mathf.Lerp(grid[ix, iy + 1], grid[ix + 1, iy + 1], tx), ty);
    }

    // ───── Helpers ───────────────────────────────────────────────────────────
    static Material MakeUnlitMaterial(Color c)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    static Material MakeLitMaterial(Color c, float emissionBoost)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_EmissionColor"))
        {
            m.SetColor("_EmissionColor", c * emissionBoost);
            if (emissionBoost > 0f) m.EnableKeyword("_EMISSION");
        }
        return m;
    }

    void OnDestroy()
    {
        if (_vignetteTex != null) Destroy(_vignetteTex);
        if (_vignetteMat != null) Destroy(_vignetteMat);
        ClearChallengeObjects();
    }
}

class PathTileMarker : MonoBehaviour { }
