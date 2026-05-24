using System.Collections.Generic;
using UnityEngine;

public class PhotophobieSim : HandicapSimulationBase
{
    [Header("Exposition passthrough AR (post-process Meta)")]
    [Tooltip("Boost de luminosité du passthrough Meta pendant la simulation (-1..1). Affecte la vraie vue AR.")]
    [Range(-1f, 1f)] public float passthroughBrightnessBoost = 0.45f;
    [Tooltip("Boost de luminosité quand la correction est active (lunettes teintées).")]
    [Range(-1f, 1f)] public float correctedPassthroughBoost = -0.10f;
    [Tooltip("Contraste appliqué au passthrough (-1..1).")]
    [Range(-1f, 1f)] public float passthroughContrast = 0.20f;
    [Tooltip("Saturation appliquée au passthrough (-1..1). Négatif = délavé.")]
    [Range(-1f, 1f)] public float passthroughSaturation = -0.25f;

    [Header("Voile lumineux ambiant additionnel")]
    [Tooltip("Voile blanc-chaud par-dessus la vue AR (ajoute à l'exposition du passthrough).")]
    [Range(0f, 0.5f)] public float baseBrightness = 0.10f;
    [Range(0f, 0.5f)] public float correctedBrightness = 0.02f;
    public Color brightTint = new Color(1.00f, 0.96f, 0.85f, 1f);
    public float veilDistance = 0.30f;
    public Vector2 veilScale = new Vector2(1.7f, 1.7f);

    [Header("Lampes virtuelles à éteindre")]
    [Range(1, 3)] public int sourceCount = 3;
    [Tooltip("Rayon visuel de l'ampoule (m).")]
    public float sourceRadius = 0.07f;
    public Color sourceColor = new Color(1f, 0.93f, 0.60f);
    [Range(0.5f, 4f)] public float sourceEmission = 2.6f;
    [Tooltip("Hauteur du plafond simulé (m) — utilisée pour suspendre lustre et appliques.")]
    public float ceilingHeight = 2.55f;

    [Header("Blocage avec la main (manette)")]
    [Tooltip("Manette qui sert d'écran (à lever entre la tête et l'ampoule).")]
    public OVRInput.Controller blockController = OVRInput.Controller.LTouch;
    [Tooltip("Rayon de l'ombre de la main : tolérance perpendiculaire au rayon casque→ampoule (m).")]
    [Range(0.03f, 0.50f)] public float blockShadowRadius = 0.16f;
    [Tooltip("Distance minimum main-casque le long du rayon pour valider (m).")]
    [Range(0.10f, 0.60f)] public float minHandDistance = 0.22f;
    [Tooltip("Durée de maintien pour éteindre définitivement (s).")]
    public float blockHoldSec = 2.0f;

    [Header("Glare (regard direct)")]
    [Range(5f, 90f)] public float glareConeDeg = 40f;
    [Range(0f, 1f)] public float glareMaxAlpha = 0.85f;
    public Color glareColor = new Color(1f, 0.95f, 0.80f, 1f);

    [Header("Jauge de douleur")]
    public float painFillRate = 0.40f;
    public float painDecayRate = 0.55f;
    [Range(0.5f, 1f)] public float painThreshold = 1.0f;

    // ───── Runtime ─────────────────────────────────────────────────────────
    GameObject _veilQuad;
    Material _veilMat;
    GameObject _glareQuad;
    Material _glareMat;
    GameObject _challengeRoot;
    List<LightSource> _sources = new List<LightSource>();
    int _remaining;
    float _pain;
    bool _correctionOn;
    float _basePainAlpha;
    float _lastDisplayedPain = -1f;
    int _lastDisplayedRem = -1;
    OVRPassthroughLayer _ptLayer;
    bool _ptApplied;

    class LightSource
    {
        public GameObject root;
        public List<Renderer> bulbRends;
        public Material activeMat;
        public Material deadMat;
        public float holdTime;
        public bool extinguished;
        public Vector3 worldPos;
    }

    // Anneau de progression unique attaché à la manette gauche.
    GameObject _handRingObj;
    Mesh _handRingMesh;
    Material _handRingMat;

    void Reset()
    {
        handicapTitle = "Photophobie";
        explanation =
            "La <b>photophobie</b> est une intolérance anormale à la lumière. La <b>pupille</b> (en rouge sur l'œil) peine à se rétrécir face à la luminosité : trop de lumière atteint la rétine, et les signaux de douleur transmis par le nerf trijumeau sont anormalement amplifiés.\n\n" +
            "Toute source — soleil, écran, éclairage — devient douloureuse, force à plisser les yeux et déclenche larmoiement et migraines.\n\n" +
            "Causes fréquentes : <b>migraine</b>, <b>uvéite</b>, <b>sécheresse oculaire sévère</b>, traumatisme crânien.";
        correctionText =
            "Pas de traitement universel : on agit sur la cause sous-jacente. En attendant :\n\n" +
            "<b>•</b> <b>Verres FL-41</b> (teinte rosée) ou lunettes ambrées\n" +
            "<b>•</b> Réduction des sources lumineuses, écran tamisé\n" +
            "<b>•</b> Traitement de la migraine ou de l'inflammation oculaire\n\n" +
            "<i>Attention aux verres très sombres en permanence : ils peuvent aggraver la sensibilité.</i>";
        anatomyPart = "Pupil";
        accent = new Color(0.96f, 0.80f, 0.20f, 1f);
        challengeKey = "photophobie";
        // La pupille échoue à se constricter face à la lumière : elle reste
        // anormalement dilatée → on l'anime en croissance cyclique.
        eyeDiseasedScaleFactor = new Vector3(1.70f, 1f, 1.70f);
    }

    protected override void Awake()
    {
        if (string.IsNullOrEmpty(handicapTitle) || handicapTitle == "Handicap") Reset();
        base.Awake();
    }

    protected override void OnSetup()
    {
        EnsureHeadAnchor();
        BuildVeil();
        BuildGlare();
        SetVeilVisible(false);
        SetGlareAlpha(0f);
    }

    protected override void OnSimulationStart()
    {
        if (_veilQuad == null) BuildVeil();
        if (_glareQuad == null) BuildGlare();
        _correctionOn = false;
        _basePainAlpha = baseBrightness;
        UpdateVeilColor();
        SetVeilVisible(true);
        ApplyPassthroughExposure(passthroughBrightnessBoost);
    }

    protected override void OnSimulationStop()
    {
        SetVeilVisible(false);
        SetGlareAlpha(0f);
        ApplyPassthroughExposure(0f);   // remet passthrough neutre
    }

    protected override void OnChallengeStart()
    {
        BuildSources();
        BuildHandRing();
        _pain = 0f;
        _lastDisplayedPain = -1f;
        _lastDisplayedRem = -1;
        // Message initial détaillé : explique le geste avant que le HUD ne se mette à jour.
        SetChallengeStatus(
            "<b>3 lampes</b> sont allumées dans la pièce : un lampadaire, un lustre, une lampe de chevet.\n" +
            "Tournez-vous vers une lampe, puis levez la <b>manette gauche</b> entre vos yeux et son ampoule " +
            "(<i>geste : se protéger du soleil avec la main</i>).\n" +
            "Un <b>anneau vert</b> se remplit autour de l'ampoule. Tenez 2 s → la lampe s'éteint."
        );
    }

    protected override void OnChallengeStop()
    {
        ClearChallengeObjects();
    }

    protected override void OnCorrectionToggle(bool on)
    {
        _correctionOn = on;
        _basePainAlpha = on ? correctedBrightness : baseBrightness;
        UpdateVeilColor();
        ApplyPassthroughExposure(on ? correctedPassthroughBoost : passthroughBrightnessBoost);
    }

    protected override void Update()
    {
        base.Update();
        if (CurrentPhase != Phase.Simulation && CurrentPhase != Phase.Challenge && CurrentPhase != Phase.Correction)
            return;

        UpdateGlare();
        if (CurrentPhase == Phase.Challenge)
        {
            UpdateBlocking();
            UpdatePain();
        }
    }

    // ───── Passthrough exposure (post-process Meta) ──────────────────────────
    void ApplyPassthroughExposure(float brightness)
    {
        if (_ptLayer == null) _ptLayer = FindAnyObjectByType<OVRPassthroughLayer>();
        if (_ptLayer == null) return;
        bool reset = Mathf.Abs(brightness) < 1e-4f && Mathf.Abs(passthroughContrast) < 1e-4f && Mathf.Abs(passthroughSaturation) < 1e-4f;
        try
        {
            if (reset)
            {
                _ptLayer.SetBrightnessContrastSaturation(0f, 0f, 0f);
                _ptApplied = false;
            }
            else
            {
                _ptLayer.SetBrightnessContrastSaturation(brightness, passthroughContrast, passthroughSaturation);
                _ptApplied = true;
            }
        }
        catch
        {
            // Fallback : ancienne API par champs
            try
            {
                _ptLayer.colorMapEditorBrightness = brightness;
                _ptLayer.colorMapEditorContrast = passthroughContrast;
                _ptLayer.colorMapEditorSaturation = passthroughSaturation;
            }
            catch { }
        }
    }

    // ───── Voile + glare ────────────────────────────────────────────────────
    void BuildVeil()
    {
        EnsureHeadAnchor();
        var anchor = headAnchor != null ? headAnchor : transform;
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "PhotophobieVeil";
        var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
        q.transform.SetParent(anchor, false);
        q.transform.localPosition = new Vector3(0f, 0f, veilDistance);
        q.transform.localRotation = Quaternion.identity;
        q.transform.localScale = new Vector3(veilScale.x, veilScale.y, 1f);
        _veilQuad = q;
        _veilMat = MakeTransparentOverlay(2950);
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = _veilMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        UpdateVeilColor();
    }

    void UpdateVeilColor()
    {
        if (_veilMat == null) return;
        Color c = brightTint; c.a = _basePainAlpha;
        _veilMat.color = c;
        if (_veilMat.HasProperty("_BaseColor")) _veilMat.SetColor("_BaseColor", c);
    }

    void SetVeilVisible(bool v)
    {
        if (_veilQuad != null) _veilQuad.SetActive(v);
    }

    void BuildGlare()
    {
        EnsureHeadAnchor();
        var anchor = headAnchor != null ? headAnchor : transform;
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "PhotophobieGlare";
        var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
        q.transform.SetParent(anchor, false);
        q.transform.localPosition = new Vector3(0f, 0f, veilDistance - 0.005f);
        q.transform.localRotation = Quaternion.identity;
        q.transform.localScale = new Vector3(veilScale.x, veilScale.y, 1f);
        _glareQuad = q;
        _glareMat = MakeTransparentOverlay(2951);
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = _glareMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        SetGlareAlpha(0f);
    }

    void SetGlareAlpha(float a)
    {
        if (_glareMat == null) return;
        Color c = glareColor; c.a = Mathf.Clamp01(a);
        _glareMat.color = c;
        if (_glareMat.HasProperty("_BaseColor")) _glareMat.SetColor("_BaseColor", c);
    }

    void UpdateGlare()
    {
        if (_sources == null || _sources.Count == 0 || headAnchor == null) { SetGlareAlpha(0f); return; }
        Vector3 head = SafeHeadPosition();
        Vector3 fwd = SafeHeadForward();
        float maxA = 0f;
        for (int i = 0; i < _sources.Count; i++)
        {
            var ls = _sources[i];
            if (ls == null || ls.extinguished) continue;
            Vector3 toS = ls.worldPos - head;
            if (toS.sqrMagnitude < 1e-4f) continue;
            toS.Normalize();
            float dot = Vector3.Dot(fwd, toS);
            float angleDeg = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            float t = 1f - Mathf.Clamp01(angleDeg / Mathf.Max(1f, glareConeDeg));
            maxA = Mathf.Max(maxA, glareMaxAlpha * t * t);
        }
        if (_correctionOn) maxA *= 0.30f;
        SetGlareAlpha(maxA);
    }

    // ───── Construction des lampes ──────────────────────────────────────────
    enum LampStyle { Floor, Chandelier, Bedside }

    struct LampSpawn
    {
        public LampStyle style;
        public float azimDeg;
        public float distance;
        public float bulbHeight;
    }

    static readonly LampSpawn[] _lampPlan = {
        new LampSpawn { style = LampStyle.Floor,      azimDeg = -42f, distance = 2.30f, bulbHeight = 1.55f },
        new LampSpawn { style = LampStyle.Chandelier, azimDeg =  35f, distance = 2.60f, bulbHeight = 2.15f },
        new LampSpawn { style = LampStyle.Bedside,    azimDeg = 140f, distance = 1.70f, bulbHeight = 0.78f },
    };

    void BuildSources()
    {
        EnsureHeadAnchor();
        var rig = FindAnyObjectByType<OVRCameraRig>();
        Transform space = rig != null ? rig.trackingSpace : null;

        Vector3 head = SafeHeadPosition();
        Vector3 fwd = SafeHeadForward(); fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
        fwd.Normalize();

        _challengeRoot = new GameObject("PhotophobieSources");
        if (space != null) _challengeRoot.transform.SetParent(space, false);

        _sources.Clear();
        int n = Mathf.Clamp(sourceCount, 1, _lampPlan.Length);
        for (int i = 0; i < n; i++)
        {
            var p = _lampPlan[i];
            Vector3 dir = Quaternion.AngleAxis(p.azimDeg, Vector3.up) * fwd;
            Vector3 bulb = new Vector3(head.x + dir.x * p.distance, p.bulbHeight, head.z + dir.z * p.distance);
            _sources.Add(SpawnLamp(bulb, p.style));
        }
        _remaining = _sources.Count;
    }

    LightSource SpawnLamp(Vector3 bulbWorld, LampStyle style)
    {
        var root = new GameObject("Lamp_" + style);
        root.transform.SetParent(_challengeRoot.transform, false);
        root.transform.position = bulbWorld;
        // Le repère local du root est aligné monde (rotation neutre) sauf pour
        // l'applique où on oriente vers le "mur".
        root.transform.rotation = Quaternion.identity;

        // Queue 2960 : rendu APRÈS le voile (2950) et le glare (2951) →
        // les lampes restent opaques visuellement, jamais lavées par l'effet photophobie.
        const int kLampQueue = 2960;
        var shadeMat = MakeLitMaterial(new Color(0.92f, 0.85f, 0.68f), 0.18f, kLampQueue);
        var metalMat = MakeLitMaterial(new Color(0.22f, 0.22f, 0.24f), 0f, kLampQueue);
        var activeBulbMat = MakeLitMaterial(sourceColor, sourceEmission, kLampQueue);
        var deadBulbMat = MakeLitMaterial(new Color(0.25f, 0.22f, 0.14f), 0f, kLampQueue);

        var bulbRends = new List<Renderer>();

        switch (style)
        {
            case LampStyle.Floor:
            {
                float floorDelta = -bulbWorld.y;
                BuildCylinder(root.transform, new Vector3(0, -0.04f, 0), new Vector3(0, floorDelta + 0.02f, 0), 0.022f, metalMat, "Pole");
                BuildBase(root.transform, new Vector3(0, floorDelta + 0.02f, 0), 0.18f, 0.04f, metalMat, "Base");
                BuildShade(root.transform, new Vector3(0, sourceRadius * 1.20f, 0), 0.18f, 0.18f, shadeMat);
                bulbRends.Add(BuildBulb(root.transform, Vector3.zero, sourceRadius, activeBulbMat));
                break;
            }
            case LampStyle.Chandelier:
            {
                float ceilDelta = ceilingHeight - bulbWorld.y;
                BuildCylinder(root.transform, new Vector3(0, 0.10f, 0), new Vector3(0, ceilDelta, 0), 0.010f, metalMat, "Chain");
                BuildBase(root.transform, new Vector3(0, ceilDelta, 0), 0.12f, 0.02f, metalMat, "CeilingPlate");
                BuildCapsule(root.transform, Vector3.zero, new Vector3(0.10f, 0.13f, 0.10f), shadeMat, "Body");
                for (int k = 0; k < 3; k++)
                {
                    float a = k * 120f;
                    Quaternion rot = Quaternion.AngleAxis(a, Vector3.up);
                    Vector3 armEnd = rot * new Vector3(0.18f, -0.04f, 0f);
                    BuildCylinder(root.transform, Vector3.zero, armEnd, 0.012f, metalMat, $"Arm_{k}");
                    bulbRends.Add(BuildBulb(root.transform, armEnd + Vector3.down * 0.02f, sourceRadius * 0.7f, activeBulbMat, $"SubBulb_{k}"));
                }
                bulbRends.Add(BuildBulb(root.transform, Vector3.zero, sourceRadius, activeBulbMat));
                break;
            }
            case LampStyle.Bedside:
            {
                BuildBase(root.transform, new Vector3(0, -0.30f, 0), 0.07f, 0.02f, metalMat, "Base");
                BuildCylinder(root.transform, new Vector3(0, -0.30f, 0), new Vector3(0, -0.05f, 0), 0.018f, metalMat, "Pole");
                BuildShade(root.transform, new Vector3(0, sourceRadius * 0.95f, 0), 0.13f, 0.14f, shadeMat);
                bulbRends.Add(BuildBulb(root.transform, Vector3.zero, sourceRadius, activeBulbMat));
                BuildBase(root.transform, new Vector3(0, -0.35f, 0), 0.30f, 0.04f, MakeLitMaterial(new Color(0.35f, 0.25f, 0.18f), 0f, kLampQueue), "TableTop");
                break;
            }
        }

        return new LightSource
        {
            root = root,
            bulbRends = bulbRends,
            activeMat = activeBulbMat,
            deadMat = deadBulbMat,
            holdTime = 0f,
            extinguished = false,
            worldPos = bulbWorld
        };
    }

    // ───── Builders primitifs ──────────────────────────────────────────────
    Renderer BuildBulb(Transform parent, Vector3 localPos, float radius, Material mat, string name = "Bulb")
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = name;
        var col = s.GetComponent<Collider>(); if (col != null) Destroy(col);
        s.transform.SetParent(parent, false);
        s.transform.localPosition = localPos;
        s.transform.localScale = Vector3.one * radius * 2f;
        var r = s.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return r;
    }

    void BuildShade(Transform parent, Vector3 localPos, float radius, float height, Material mat)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = "Shade";
        var col = c.GetComponent<Collider>(); if (col != null) Destroy(col);
        c.transform.SetParent(parent, false);
        c.transform.localPosition = localPos;
        c.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        var r = c.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void BuildBase(Transform parent, Vector3 localPos, float radius, float height, Material mat, string name = "Base")
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        var col = c.GetComponent<Collider>(); if (col != null) Destroy(col);
        c.transform.SetParent(parent, false);
        c.transform.localPosition = localPos;
        c.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        var r = c.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void BuildCapsule(Transform parent, Vector3 localPos, Vector3 scale, Material mat, string name)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        c.name = name;
        var col = c.GetComponent<Collider>(); if (col != null) Destroy(col);
        c.transform.SetParent(parent, false);
        c.transform.localPosition = localPos;
        c.transform.localScale = scale;
        var r = c.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void BuildCylinder(Transform parent, Vector3 localStart, Vector3 localEnd, float thickness, Material mat, string name)
    {
        Vector3 diff = localEnd - localStart;
        float length = diff.magnitude;
        if (length < 1e-4f) return;
        var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        c.name = name;
        var col = c.GetComponent<Collider>(); if (col != null) Destroy(col);
        c.transform.SetParent(parent, false);
        c.transform.localPosition = (localStart + localEnd) * 0.5f;
        c.transform.localRotation = Quaternion.FromToRotation(Vector3.up, diff.normalized);
        c.transform.localScale = new Vector3(thickness, length * 0.5f, thickness);
        var r = c.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    GameObject BuildBillboardOverlay(Transform parent, Vector3 localPos, float scale, Color color, float alpha, int queue)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = "Overlay";
        var col = q.GetComponent<Collider>(); if (col != null) Destroy(col);
        q.transform.SetParent(parent, false);
        q.transform.localPosition = localPos;
        q.transform.localScale = Vector3.one * scale;
        var mat = MakeTransparentOverlay(queue);
        Color c = color; c.a = alpha;
        mat.color = c;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        var r = q.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        q.AddComponent<Billboard>();
        return q;
    }

    // ───── Blocage main ─────────────────────────────────────────────────────
    void UpdateBlocking()
    {
        if (_sources.Count == 0) return;
        Vector3 head = SafeHeadPosition();
        Vector3 hand = GetBlockHandPosition();
        bool handValid = hand != Vector3.zero && (hand - head).sqrMagnitude >= minHandDistance * minHandDistance;

        float dt = Time.unscaledDeltaTime;
        bool changed = false;
        bool anyBlockedThisFrame = false;
        float bestProgress = 0f;

        for (int i = 0; i < _sources.Count; i++)
        {
            var ls = _sources[i];
            if (ls == null || ls.extinguished) continue;

            bool blocking = handValid && IsHandBlocking(head, hand, ls.worldPos);
            if (blocking)
            {
                ls.holdTime = Mathf.Min(blockHoldSec, ls.holdTime + dt);
                anyBlockedThisFrame = true;
                if (ls.holdTime >= blockHoldSec)
                {
                    ExtinguishSource(ls);
                    changed = true;
                    continue;
                }
            }
            else
            {
                ls.holdTime = Mathf.Max(0f, ls.holdTime - dt * 1.4f);
            }
            if (ls.holdTime > bestProgress) bestProgress = ls.holdTime;
        }

        UpdateHandRing(bestProgress / blockHoldSec);

        if (anyBlockedThisFrame)
        {
            HapticFeedback.Pulse(blockController, 0.40f, 0.18f, 0.025f);
        }
        if (changed) RefreshChallengeHud();
    }

    // ───── Anneau attaché à la main ────────────────────────────────────────
    void BuildHandRing()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null) return;
        Transform handAnchor = blockController == OVRInput.Controller.RTouch ? rig.rightHandAnchor : rig.leftHandAnchor;
        if (handAnchor == null) return;
        if (_handRingObj != null) return;

        var go = new GameObject("HandProgressRing");
        go.transform.SetParent(handAnchor, false);
        go.transform.localPosition = new Vector3(0f, 0.08f, 0.02f);  // au-dessus du poing
        go.transform.localRotation = Quaternion.identity;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        // Queue 2999 (juste avant l'UI à 3000) + ZTest Always → l'anneau est
        // dessiné par-dessus l'effet photophobie, par-dessus la manette, et
        // par-dessus n'importe quelle géométrie opaque dans le champ de vision.
        _handRingMat = MakeTransparentOverlay(2999);
        if (_handRingMat.HasProperty("_ZTest"))
            _handRingMat.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        if (_handRingMat.HasProperty("_Cull"))
            _handRingMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);  // 2 faces visibles
        Color c0 = new Color(0.20f, 0.95f, 0.40f, 1f);   // alpha 1 = opaque
        _handRingMat.color = c0;
        if (_handRingMat.HasProperty("_BaseColor")) _handRingMat.SetColor("_BaseColor", c0);
        mr.sharedMaterial = _handRingMat;

        _handRingMesh = new Mesh { name = "HandRingMesh" };
        mf.sharedMesh = _handRingMesh;

        go.AddComponent<Billboard>();
        go.SetActive(false);
        _handRingObj = go;
    }

    void UpdateHandRing(float progress)
    {
        if (_handRingObj == null) return;
        if (progress <= 0.001f)
        {
            _handRingObj.SetActive(false);
            return;
        }
        _handRingObj.SetActive(true);
        // Anneau bien voyant : extérieur 17 cm, épaisseur 4 cm.
        RebuildRingMesh(_handRingMesh, progress, 48, 0.050f, 0.085f);
        Color a = new Color(0.10f, 0.85f, 0.30f, 1f);
        Color b = new Color(0.35f, 1.00f, 0.45f, 1f);
        Color c = Color.Lerp(a, b, progress);
        _handRingMat.color = c;
        if (_handRingMat.HasProperty("_BaseColor")) _handRingMat.SetColor("_BaseColor", c);
    }

    // Vraie ombre : projette la main sur le rayon casque→ampoule, vérifie que
    // (1) la main est en avant du casque (au moins minHandDistance),
    // (2) la main est AVANT l'ampoule,
    // (3) la distance perpendiculaire au rayon est < blockShadowRadius.
    bool IsHandBlocking(Vector3 head, Vector3 hand, Vector3 source)
    {
        Vector3 toSource = source - head;
        float distSource = toSource.magnitude;
        if (distSource < 0.20f) return false;
        Vector3 nDirSource = toSource / distSource;

        Vector3 toHand = hand - head;
        float t = Vector3.Dot(toHand, nDirSource);   // distance projetée le long du rayon
        if (t < minHandDistance) return false;
        if (t > distSource - 0.02f) return false;

        Vector3 closest = head + nDirSource * t;
        float perpDist = Vector3.Distance(hand, closest);
        return perpDist <= blockShadowRadius;
    }

    Vector3 GetBlockHandPosition()
    {
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig == null) return Vector3.zero;
        Transform handAnchor = blockController == OVRInput.Controller.RTouch
            ? rig.rightHandAnchor : rig.leftHandAnchor;
        if (handAnchor == null) return Vector3.zero;
        return handAnchor.position;
    }

    /// <summary>
    /// Reconstruit un anneau procédural rempli de 0 à `progress` (sens horaire,
    /// départ à 12h). Tessellé en `segments` quads (2 triangles chacun).
    /// </summary>
    static void RebuildRingMesh(Mesh mesh, float progress, int segments, float innerR, float outerR)
    {
        int filled = Mathf.Max(0, Mathf.RoundToInt(segments * Mathf.Clamp01(progress)));
        if (filled == 0)
        {
            mesh.Clear();
            return;
        }
        int vCount = (filled + 1) * 2;
        var verts = new Vector3[vCount];
        var tris  = new int[filled * 6];

        for (int i = 0; i <= filled; i++)
        {
            float frac = (float)i / segments;
            // Départ en haut (12 h), progression dans le sens horaire :
            float ang = Mathf.PI * 0.5f - frac * Mathf.PI * 2f;
            float cos = Mathf.Cos(ang);
            float sin = Mathf.Sin(ang);
            verts[i * 2]     = new Vector3(cos * innerR, sin * innerR, 0f);
            verts[i * 2 + 1] = new Vector3(cos * outerR, sin * outerR, 0f);
        }
        for (int i = 0, ti = 0; i < filled; i++)
        {
            int a = i * 2;
            int b = i * 2 + 1;
            int c = (i + 1) * 2;
            int d = (i + 1) * 2 + 1;
            // Winding CCW vu de +Z (face caméra après billboard) :
            tris[ti++] = a; tris[ti++] = c; tris[ti++] = b;
            tris[ti++] = b; tris[ti++] = c; tris[ti++] = d;
        }
        mesh.Clear();
        mesh.vertices  = verts;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
    }

    void ExtinguishSource(LightSource ls)
    {
        ls.extinguished = true;
        // Éteindre TOUS les bulbes (cas du lustre avec sub-bulbes) → coupe émission + lumière.
        if (ls.bulbRends != null)
        {
            for (int i = 0; i < ls.bulbRends.Count; i++)
                if (ls.bulbRends[i] != null) ls.bulbRends[i].sharedMaterial = ls.deadMat;
        }
        _remaining--;
        HapticFeedback.Pulse(blockController, 0.55f, 0.55f, 0.18f);
        if (_remaining <= 0) EndChallenge();
    }

    // ───── Douleur ──────────────────────────────────────────────────────────
    void UpdatePain()
    {
        if (_sources.Count == 0) return;
        Vector3 head = SafeHeadPosition();
        Vector3 fwd = SafeHeadForward();
        float total = 0f;
        for (int i = 0; i < _sources.Count; i++)
        {
            var ls = _sources[i];
            if (ls == null || ls.extinguished) continue;
            Vector3 toS = ls.worldPos - head;
            if (toS.sqrMagnitude < 1e-4f) continue;
            toS.Normalize();
            float dot = Vector3.Dot(fwd, toS);
            float angleDeg = Mathf.Acos(Mathf.Clamp(dot, -1f, 1f)) * Mathf.Rad2Deg;
            if (angleDeg < glareConeDeg)
            {
                float t = 1f - angleDeg / glareConeDeg;
                total += t * t;
            }
        }
        if (_correctionOn) total *= 0.20f;

        float dt = Time.unscaledDeltaTime;
        if (total > 0.01f)
            _pain = Mathf.Clamp01(_pain + total * painFillRate * dt);
        else
            _pain = Mathf.Max(0f, _pain - painDecayRate * dt);

        if (_pain >= painThreshold)
        {
            _pain = 0f;
            HapticFeedback.Pulse(OVRInput.Controller.RTouch, 0.55f, 0.6f, 0.18f);
            HapticFeedback.Pulse(OVRInput.Controller.LTouch, 0.55f, 0.6f, 0.18f);
            SetGlareAlpha(1f);
        }
        RefreshChallengeHud();
    }

    void RefreshChallengeHud()
    {
        if (CurrentPhase != Phase.Challenge) return;
        if (Mathf.Abs(_pain - _lastDisplayedPain) < 0.015f && _remaining == _lastDisplayedRem) return;
        _lastDisplayedPain = _pain;
        _lastDisplayedRem = _remaining;

        int filled = Mathf.Clamp(Mathf.RoundToInt(_pain * 12f), 0, 12);
        string bar = "";
        for (int i = 0; i < 12; i++) bar += i < filled ? "▰" : "▱";
        string painLine = _pain >= 0.70f ? $"<color=#FF5530>{bar}</color>" : bar;

        SetChallengeStatus(
            $"Lampes à éteindre : <b>{_remaining} / {_sources.Count}</b>   ·   Douleur : {painLine}\n" +
            $"<size=85%>Manette gauche <b>EN ÉCRAN</b> entre vos yeux et l'ampoule → tenir 2 s (anneau vert).</size>"
        );
    }

    // ───── Cleanup ──────────────────────────────────────────────────────────
    void ClearChallengeObjects()
    {
        for (int i = 0; i < _sources.Count; i++)
        {
            var ls = _sources[i];
            if (ls == null) continue;
            if (ls.activeMat != null) Destroy(ls.activeMat);
            if (ls.deadMat != null) Destroy(ls.deadMat);
            if (ls.root != null) Destroy(ls.root);
        }
        _sources.Clear();
        if (_challengeRoot != null) { Destroy(_challengeRoot); _challengeRoot = null; }
        if (_handRingObj != null) { Destroy(_handRingObj); _handRingObj = null; }
        if (_handRingMesh != null) { Destroy(_handRingMesh); _handRingMesh = null; }
        if (_handRingMat != null) { Destroy(_handRingMat); _handRingMat = null; }
        _remaining = 0;
        _pain = 0f;
    }

    // ───── Helpers ──────────────────────────────────────────────────────────
    static Material MakeTransparentOverlay(int queue)
    {
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Transparent");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = queue;
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.color = Color.white;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        return m;
    }

    static Material MakeLitMaterial(Color c, float emissionBoost, int renderQueueOverride = -1)
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
        if (renderQueueOverride >= 0) m.renderQueue = renderQueueOverride;
        return m;
    }

    void OnDestroy()
    {
        if (_veilMat != null) Destroy(_veilMat);
        if (_glareMat != null) Destroy(_glareMat);
        ClearChallengeObjects();
        ApplyPassthroughExposure(0f);
    }
}

// Helper : rend le quad face caméra à chaque frame (pour halos et anneaux).
class Billboard : MonoBehaviour
{
    Transform _cam;
    void LateUpdate()
    {
        if (_cam == null)
        {
            var rig = FindAnyObjectByType<OVRCameraRig>();
            _cam = rig != null ? rig.centerEyeAnchor : (Camera.main != null ? Camera.main.transform : null);
            if (_cam == null) return;
        }
        Vector3 dir = transform.position - _cam.position;
        if (dir.sqrMagnitude < 1e-4f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
    }
}
