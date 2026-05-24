using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EyeAnatomyView : MonoBehaviour
{
    [Header("Style — couleurs")]
    public Color scleraColor    = new Color(0.97f, 0.95f, 0.92f);
    public Color veinColor      = new Color(0.78f, 0.18f, 0.18f);
    public Color corneaColor    = new Color(0.95f, 0.97f, 1.00f, 0.30f);
    public Color irisColor      = new Color(0.32f, 0.55f, 0.70f);
    public Color irisDarkColor  = new Color(0.10f, 0.20f, 0.32f);
    public Color pupilColor     = new Color(0.02f, 0.02f, 0.02f);
    public Color lensColor      = new Color(0.85f, 0.92f, 1.00f, 0.40f);
    public Color nerveColor     = new Color(0.96f, 0.86f, 0.78f);
    public Color nerveDarkColor = new Color(0.78f, 0.55f, 0.50f);
    public Color sheathColor    = new Color(0.95f, 0.85f, 0.78f, 0.32f);
    public Color macColor       = new Color(0.50f, 0.30f, 0.22f);
    public Color muscleColor    = new Color(0.78f, 0.32f, 0.32f);
    public Color tendonColor    = new Color(0.96f, 0.92f, 0.86f);
    public Color fatColor       = new Color(0.96f, 0.85f, 0.55f, 0.16f);
    public Color highlightColor = new Color(1.00f, 0.30f, 0.20f);

    [Header("Animation")]
    public bool autoRotate = true;
    [Range(0f, 60f)] public float rotateSpeed = 8f;

    [Header("Animation maladie (boucle sain ↔ atteint)")]
    public Part diseasedPart = Part.None;
    public Vector3 diseasedScaleFactor = Vector3.one;
    [Range(0.5f, 6f)] public float diseaseLoopDuration = 3.5f;

    [Header("Modèle externe (optionnel)")]
    public GameObject anatomyModelPrefab;
    public Vector3 modelLocalEuler = Vector3.zero;
    public Vector3 modelLocalScale = Vector3.one;
    public Vector3 modelLocalOffset = Vector3.zero;

    public enum Part { None, Sclera, Cornea, Iris, Pupil, Lens, OpticNerve, Retina, Macula, Muscles, Brain, VisualCortex }

    Part _current = Part.None;
    Renderer _eye, _cornea, _iris, _pupil, _lens, _nerve, _macula;
    Renderer[] _muscles;
    Renderer[] _nerveBranches;
    Renderer[] _vessels;
    GameObject _muscleGroup, _brainOverlay, _vesselsGroup, _nerveGroup;
    readonly Dictionary<Renderer, Color>   _baseColors = new Dictionary<Renderer, Color>();
    readonly Dictionary<Renderer, Vector3> _baseScales = new Dictionary<Renderer, Vector3>();
    readonly Dictionary<Part, List<Renderer>> _diseaseTargets = new Dictionary<Part, List<Renderer>>();
    bool _usingPrefab;
    readonly Dictionary<Part, List<Renderer>> _prefabPartMap = new Dictionary<Part, List<Renderer>>();

    void Awake()
    {
        if (anatomyModelPrefab != null) BuildFromPrefab();
        else Build();
        Highlight(Part.None);
    }

    void Update()
    {
        if (autoRotate)
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.Self);

        AnimateDisease();
    }

    void AnimateDisease()
    {
        if (diseasedPart == Part.None) return;
        if (diseasedScaleFactor == Vector3.one) return;
        if (diseaseLoopDuration < 0.01f) return;

        // Cycle phasé (transformation visible + temps d'observation pour chaque état) :
        //   0 – 15 %  : tient l'état SAIN
        //  15 – 50 %  : transformation sain → atteint (smoothstep)
        //  50 – 85 %  : tient l'état ATTEINT
        //  85 –100 %  : retour atteint → sain (smoothstep)
        float t = (Time.time % diseaseLoopDuration) / diseaseLoopDuration;
        float morph;
        if (t < 0.15f)      morph = 0f;
        else if (t < 0.50f) morph = Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.35f);
        else if (t < 0.85f) morph = 1f;
        else                morph = Mathf.SmoothStep(1f, 0f, (t - 0.85f) / 0.15f);

        IEnumerable<Renderer> targets;
        if (_diseaseTargets.TryGetValue(diseasedPart, out var explicitList))
            targets = explicitList;
        else
            targets = TargetsFor(diseasedPart);

        foreach (var r in targets)
        {
            if (r == null) continue;
            if (!_baseScales.TryGetValue(r, out var baseScale)) continue;
            Vector3 deformed = Vector3.Scale(baseScale, diseasedScaleFactor);
            r.transform.localScale = Vector3.Lerp(baseScale, deformed, morph);
        }
    }

    public void Highlight(string partName)
    {
        if (string.IsNullOrEmpty(partName)) { Highlight(Part.None); return; }
        if (System.Enum.TryParse<Part>(partName, true, out var p)) Highlight(p);
        else Highlight(Part.None);
    }

    public void Highlight(Part p)
    {
        ResetColors();
        _current = p;
        if (_brainOverlay != null) _brainOverlay.SetActive(p == Part.Brain || p == Part.VisualCortex);

        // Couleur statique sur la partie atteinte (pas de clignotement).
        if (p == Part.None) return;
        foreach (var r in TargetsFor(p))
        {
            if (r == null) continue;
            r.material.color = highlightColor;
            if (r.material.HasProperty("_BaseColor"))
                r.material.SetColor("_BaseColor", highlightColor);
        }
    }

    IEnumerable<Renderer> TargetsFor(Part p)
    {
        if (_usingPrefab)
        {
            if (_prefabPartMap.TryGetValue(p, out var list))
                foreach (var r in list) yield return r;
            yield break;
        }

        switch (p)
        {
            case Part.Sclera:     yield return _eye; break;
            case Part.Cornea:     yield return _cornea; break;
            case Part.Iris:       yield return _iris; break;
            case Part.Pupil:      yield return _pupil; break;
            case Part.Lens:       yield return _lens; break;
            case Part.OpticNerve:
                yield return _nerve;
                if (_nerveBranches != null) foreach (var n in _nerveBranches) yield return n;
                break;
            case Part.Retina:     yield return _eye; break;
            case Part.Macula:     yield return _macula; break;
            case Part.Muscles:
                if (_muscles != null) foreach (var m in _muscles) yield return m;
                break;
        }
    }

    void ResetColors()
    {
        foreach (var kv in _baseColors)
        {
            if (kv.Key == null) continue;
            kv.Key.material.color = kv.Value;
            if (kv.Key.material.HasProperty("_BaseColor"))
                kv.Key.material.SetColor("_BaseColor", kv.Value);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // Build (procédural) — sclère de rayon 0.5 (Sphere primitive scale=1)
    // ────────────────────────────────────────────────────────────────────────
    void Build()
    {
        // ── Graisse orbitaire : remplit les vides à l'arrière de l'œil ──
        var fat = MakePrim("OrbitalFat", PrimitiveType.Sphere,
            new Vector3(0, 0, 0.45f), Quaternion.identity, new Vector3(1.30f, 1.30f, 1.10f),
            fatColor, transform);
        MakeTransparent(fat.material, fatColor);
        fat.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ── Sclère ──
        _eye = MakePrim("Sclera", PrimitiveType.Sphere,
            Vector3.zero, Quaternion.identity, Vector3.one * 1.0f, Color.white);
        var scleraTex = MakeScleraTexture(512);
        ApplyTexture(_eye.sharedMaterial, scleraTex);
        _eye.sharedMaterial.color = Color.white;
        _baseColors[_eye] = Color.white;

        // ── Cornée transparente bombée ──
        _cornea = MakePrim("Cornea", PrimitiveType.Sphere,
            new Vector3(0, 0, -0.40f), Quaternion.identity,
            new Vector3(0.66f, 0.66f, 0.34f), corneaColor);
        MakeTransparent(_cornea.material, corneaColor);

        // ── Iris ──
        _iris = MakePrim("Iris", PrimitiveType.Cylinder,
            new Vector3(0, 0, -0.50f), Quaternion.Euler(90, 0, 0),
            new Vector3(0.50f, 0.005f, 0.50f), Color.white);
        var irisTex = MakeIrisTexture(512, irisColor, irisDarkColor);
        ApplyTexture(_iris.sharedMaterial, irisTex);
        _iris.sharedMaterial.color = Color.white;
        _baseColors[_iris] = Color.white;

        // ── Pupille ──
        _pupil = MakePrim("Pupil", PrimitiveType.Cylinder,
            new Vector3(0, 0, -0.508f), Quaternion.Euler(90, 0, 0),
            new Vector3(0.20f, 0.005f, 0.20f), pupilColor);

        // ── Cristallin ──
        _lens = MakePrim("Lens", PrimitiveType.Sphere,
            new Vector3(0, 0, -0.32f), Quaternion.identity,
            new Vector3(0.36f, 0.36f, 0.18f), lensColor);
        MakeTransparent(_lens.material, lensColor);

        // ── Nerf optique : disque + tronc capsule + gaine + chiasma + tractus ──
        _nerveGroup = new GameObject("OpticNerveGroup");
        _nerveGroup.transform.SetParent(transform, false);

        Vector3 discPos    = new Vector3(0.10f, -0.05f, 0.49f);  // sur la sclère, légèrement nasal
        Vector3 chiasmaPos = new Vector3(0.04f, 0.00f, 0.92f);
        Vector3 dirNerve   = (chiasmaPos - discPos).normalized;
        Quaternion alignNerve = Quaternion.FromToRotation(Vector3.up, dirNerve);

        // Petit disque sombre encastré dans la sclère (la "papille")
        var disc = MakePrim("OpticDisc", PrimitiveType.Cylinder,
            discPos, alignNerve, new Vector3(0.20f, 0.012f, 0.20f),
            nerveDarkColor, _nerveGroup.transform);

        // Tronc principal — capsule (formes arrondies au lieu de cylindre droit)
        _nerve = MakeCapsuleBetween("OpticNerveTrunk", discPos, chiasmaPos,
            0.18f, nerveColor, _nerveGroup.transform);

        // Gaine durale transparente plus épaisse
        var sheath = MakeCapsuleBetween("DuralSheath", discPos, chiasmaPos,
            0.24f, sheathColor, _nerveGroup.transform);
        MakeTransparent(sheath.sharedMaterial, sheathColor);

        // Chiasma optique (jonction sphérique)
        var chiasm = MakePrim("OpticChiasm", PrimitiveType.Sphere,
            chiasmaPos, Quaternion.identity, Vector3.one * 0.22f,
            nerveColor, _nerveGroup.transform);

        // Tractus optique vers le cortex
        Vector3 tractEnd = new Vector3(-0.22f, 0.22f, 1.10f);
        var tract = MakeCapsuleBetween("OpticTract", chiasmaPos, tractEnd,
            0.12f, nerveDarkColor, _nerveGroup.transform);

        _nerveBranches = new Renderer[] { disc, sheath, chiasm, tract };
        _diseaseTargets[Part.OpticNerve] = new List<Renderer> { _nerve, sheath };

        // ── Macula (point sombre temporal au fond de l'œil) ──
        _macula = MakePrim("Macula", PrimitiveType.Cylinder,
            new Vector3(0.08f, 0, 0.49f), Quaternion.Euler(90, 0, 0),
            new Vector3(0.18f, 0.005f, 0.18f), macColor);

        // ── Apex orbitaire (annulus de Zinn) — sphère où convergent les muscles ──
        Vector3 apexPos = new Vector3(0, 0, 0.85f);
        MakePrim("OrbitApex", PrimitiveType.Sphere,
            apexPos, Quaternion.identity, Vector3.one * 0.20f,
            new Color(0.86f, 0.65f, 0.55f), transform);

        // ── 6 muscles oculomoteurs (capsules tangentes à la sclère) ──
        _muscleGroup = new GameObject("Muscles");
        _muscleGroup.transform.SetParent(transform, false);
        var muscleList = new List<Renderer>();

        Vector3[] rectusDirs = { Vector3.up, Vector3.down, Vector3.right, Vector3.left };
        string[] rectusNames = { "RectusSup", "RectusInf", "RectusLat", "RectusMed" };
        for (int i = 0; i < 4; i++)
        {
            Vector3 dir = rectusDirs[i];

            // Insertion : sur la sclère antérieure (entre équateur et limbe), projetée sur surface r=0.5
            Vector3 insertion = new Vector3(dir.x * 0.46f, dir.y * 0.46f, -0.20f).normalized * 0.50f;
            // Coude : juste à l'extérieur de l'équateur
            Vector3 elbow = dir * 0.55f + Vector3.forward * 0.05f;
            // Apex : convergence postérieure avec léger spread radial
            Vector3 apex = apexPos + dir * 0.05f;

            // Sphère d'insertion (jonction tendineuse arrondie sur la sclère)
            muscleList.Add(MakePrim(rectusNames[i] + "_InsertionJoint", PrimitiveType.Sphere,
                insertion, Quaternion.identity, Vector3.one * 0.07f,
                tendonColor, _muscleGroup.transform));

            // Tendon : capsule fine de l'insertion à l'elbow
            muscleList.Add(MakeCapsuleBetween(rectusNames[i] + "_Tendon",
                insertion, elbow, 0.07f, tendonColor, _muscleGroup.transform));

            // Belly : capsule épaisse de l'elbow vers l'apex
            muscleList.Add(MakeCapsuleBetween(rectusNames[i] + "_Belly",
                elbow, apex, 0.13f, muscleColor, _muscleGroup.transform));
        }

        // Oblique supérieur avec trochlée (poulie au coin haut-nasal-avant)
        Vector3 soInsertion = new Vector3(0.20f, 0.20f, 0.10f).normalized * 0.50f;
        Vector3 soTrochlea  = new Vector3(-0.22f, 0.30f, -0.30f);
        Vector3 soOrigin    = new Vector3(-0.05f, 0.18f, 0.78f);
        muscleList.Add(MakePrim("SO_InsertionJoint", PrimitiveType.Sphere,
            soInsertion, Quaternion.identity, Vector3.one * 0.06f, tendonColor, _muscleGroup.transform));
        muscleList.Add(MakeCapsuleBetween("SO_Tendon", soInsertion, soTrochlea, 0.05f, tendonColor, _muscleGroup.transform));
        muscleList.Add(MakePrim("SO_TrochleaJoint", PrimitiveType.Sphere,
            soTrochlea, Quaternion.identity, Vector3.one * 0.07f, tendonColor, _muscleGroup.transform));
        muscleList.Add(MakeCapsuleBetween("SO_Belly", soTrochlea, soOrigin, 0.10f, muscleColor, _muscleGroup.transform));

        // Oblique inférieur (court, pas de poulie)
        Vector3 ioInsertion = new Vector3(0.20f, -0.20f, 0.10f).normalized * 0.50f;
        Vector3 ioOrigin    = new Vector3(-0.30f, -0.32f, -0.10f);
        muscleList.Add(MakePrim("IO_InsertionJoint", PrimitiveType.Sphere,
            ioInsertion, Quaternion.identity, Vector3.one * 0.06f, tendonColor, _muscleGroup.transform));
        muscleList.Add(MakeCapsuleBetween("IO", ioInsertion, ioOrigin, 0.09f, muscleColor, _muscleGroup.transform));

        _muscles = muscleList.ToArray();

        // ── Vaisseaux ciliaires postérieurs (autour du nerf optique, sur la sclère) ──
        _vesselsGroup = new GameObject("Vessels");
        _vesselsGroup.transform.SetParent(transform, false);
        var vesselList = new List<Renderer>();
        for (int i = 0; i < 8; i++)
        {
            float a = (i / 8f) * Mathf.PI * 2f;
            Vector3 start = new Vector3(0.06f + Mathf.Cos(a) * 0.13f, -0.04f + Mathf.Sin(a) * 0.13f, 0.43f).normalized * 0.502f;
            Vector3 end   = new Vector3(start.x + Mathf.Cos(a) * 0.06f, start.y + Mathf.Sin(a) * 0.06f, start.z + 0.02f).normalized * 0.508f;
            vesselList.Add(MakeCapsuleBetween("CiliaryArtery_" + i, start, end, 0.020f, veinColor, _vesselsGroup.transform));
        }
        _vessels = vesselList.ToArray();

        // ── Aperçu cerveau (Brain / VisualCortex) ──
        _brainOverlay = MakePrim("BrainHint", PrimitiveType.Sphere,
            new Vector3(0, 0.70f, 0.30f), Quaternion.identity, new Vector3(0.45f, 0.32f, 0.38f),
            new Color(0.96f, 0.78f, 0.78f), transform).gameObject;
        _brainOverlay.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Helpers géométriques
    // ────────────────────────────────────────────────────────────────────────
    Renderer MakePrim(string n, PrimitiveType t, Vector3 pos, Quaternion rot, Vector3 scl, Color col, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = n;
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scl;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = new Material(FindShader());
        r.sharedMaterial.color = col;
        if (r.sharedMaterial.HasProperty("_BaseColor"))
            r.sharedMaterial.SetColor("_BaseColor", col);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        _baseColors[r] = col;
        _baseScales[r] = scl;
        return r;
    }

    /// <summary>Capsule organique (extrémités arrondies) entre deux points locaux.</summary>
    Renderer MakeCapsuleBetween(string name, Vector3 a, Vector3 b, float thickness, Color col, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        go.transform.SetParent(parent != null ? parent : transform, false);
        Vector3 mid = (a + b) * 0.5f;
        Vector3 dir = b - a;
        float length = dir.magnitude;
        go.transform.localPosition = mid;
        go.transform.localRotation = length > 1e-4f
            ? Quaternion.FromToRotation(Vector3.up, dir.normalized)
            : Quaternion.identity;
        // Capsule mesh : hauteur 2 unités par défaut → scale.y = length/2 pour longueur totale = length.
        // scale.x/z = thickness → diamètre = thickness.
        Vector3 scl = new Vector3(thickness, length * 0.5f, thickness);
        go.transform.localScale = scl;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = new Material(FindShader());
        r.sharedMaterial.color = col;
        if (r.sharedMaterial.HasProperty("_BaseColor"))
            r.sharedMaterial.SetColor("_BaseColor", col);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        _baseColors[r] = col;
        _baseScales[r] = scl;
        return r;
    }

    static void ApplyTexture(Material m, Texture tex)
    {
        m.mainTexture = tex;
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Textures procédurales
    // ────────────────────────────────────────────────────────────────────────
    Texture2D MakeScleraTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[size * size];
        for (int i = 0; i < px.Length; i++) px[i] = scleraColor;

        var rng = new System.Random(42);
        int veinCount = 22;
        for (int v = 0; v < veinCount; v++)
        {
            int x = rng.Next(size);
            int y = rng.Next(size);
            float dx = (float)(rng.NextDouble() - 0.5);
            float dy = (float)(rng.NextDouble() - 0.5);
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            if (len > 0.001f) { dx /= len; dy /= len; }
            int steps = 80 + rng.Next(140);
            float thickness = 0.7f + (float)rng.NextDouble() * 1.3f;
            float opacityMul = 0.45f + (float)rng.NextDouble() * 0.35f;

            for (int s = 0; s < steps; s++)
            {
                dx += (float)(rng.NextDouble() - 0.5) * 0.18f;
                dy += (float)(rng.NextDouble() - 0.5) * 0.18f;
                float l = Mathf.Sqrt(dx * dx + dy * dy);
                if (l > 0.001f) { dx /= l; dy /= l; }
                x += (int)Mathf.Round(dx);
                y += (int)Mathf.Round(dy);
                if (x < 0 || x >= size || y < 0 || y >= size) break;

                int it = Mathf.CeilToInt(thickness);
                for (int dyy = -it; dyy <= it; dyy++)
                for (int dxx = -it; dxx <= it; dxx++)
                {
                    int xx = x + dxx; int yy = y + dyy;
                    if (xx < 0 || xx >= size || yy < 0 || yy >= size) continue;
                    float dist = Mathf.Sqrt(dxx * dxx + dyy * dyy);
                    float fall = Mathf.Clamp01(1f - dist / thickness);
                    if (fall < 0.01f) continue;
                    Color cur = px[yy * size + xx];
                    px[yy * size + xx] = Color.Lerp(cur, veinColor, fall * opacityMul);
                }
            }
        }

        for (int i = 0; i < 25; i++)
        {
            int cx = rng.Next(size);
            int cy = rng.Next(size);
            int rad = 6 + rng.Next(10);
            float strength = 0.05f + (float)rng.NextDouble() * 0.10f;
            for (int yy = cy - rad; yy <= cy + rad; yy++)
            for (int xx = cx - rad; xx <= cx + rad; xx++)
            {
                if (xx < 0 || xx >= size || yy < 0 || yy >= size) continue;
                float d = Vector2.Distance(new Vector2(xx, yy), new Vector2(cx, cy));
                if (d > rad) continue;
                float fall = 1f - d / rad;
                Color cur = px[yy * size + xx];
                px[yy * size + xx] = Color.Lerp(cur, veinColor, fall * strength);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    Texture2D MakeIrisTexture(int size, Color baseColor, Color darkColor)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        var px = new Color[size * size];
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float maxR = size * 0.5f;
        var rng = new System.Random(7);

        const int angSteps = 720;
        float[] noise = new float[angSteps];
        for (int i = 0; i < angSteps; i++) noise[i] = (float)rng.NextDouble();
        float[] smooth = new float[angSteps];
        for (int i = 0; i < angSteps; i++)
        {
            float s = 0; int w = 4;
            for (int k = -w; k <= w; k++) s += noise[(i + k + angSteps) % angSteps];
            smooth[i] = s / (2 * w + 1);
        }

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - c.x;
            float dy = y - c.y;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / maxR;
            int idx = y * size + x;

            if (r > 1f) { px[idx] = new Color(0, 0, 0, 0); continue; }

            float angle = Mathf.Atan2(dy, dx);
            float angDeg = (angle + Mathf.PI) / (2f * Mathf.PI) * angSteps;
            int ai = ((int)angDeg) % angSteps;

            float radial = 0.5f + 0.5f * Mathf.Sin(angle * 90f);
            float n = Mathf.Lerp(smooth[ai], radial, 0.4f);

            float radialBand;
            if (r < 0.55f)       radialBand = Mathf.InverseLerp(0.42f, 0.55f, r);
            else if (r < 0.85f)  radialBand = 1f - (r - 0.55f) / (0.85f - 0.55f) * 0.30f;
            else                 radialBand = Mathf.Lerp(0.70f, 0.10f, (r - 0.85f) / 0.15f);

            float intensity = Mathf.Clamp01(0.35f + n * 0.65f) * radialBand;
            Color col = Color.Lerp(darkColor, baseColor, intensity);

            if (r >= 0.42f && r <= 0.50f)
            {
                float t = Mathf.Abs(r - 0.46f) / 0.04f;
                col = Color.Lerp(darkColor, col, Mathf.Clamp01(t));
            }

            if (r < 0.42f) col = new Color(0, 0, 0, 1f);

            px[idx] = col;
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Modèle externe (prefab Sketchfab .glb importé)
    // ────────────────────────────────────────────────────────────────────────
    void BuildFromPrefab()
    {
        _usingPrefab = true;
        var inst = Instantiate(anatomyModelPrefab, transform);
        inst.name = "AnatomyModel";
        inst.transform.localPosition = modelLocalOffset;
        inst.transform.localEulerAngles = modelLocalEuler;
        inst.transform.localScale = modelLocalScale;

        var allRenderers = inst.GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            var mats = r.sharedMaterials;
            if (mats != null && mats.Length > 0)
                _baseColors[r] = mats[0] != null ? mats[0].color : Color.white;
            _baseScales[r] = r.transform.localScale;
        }

        AddPrefabMapping(allRenderers, Part.OpticNerve,    "nerve", "optic", "nerf");
        AddPrefabMapping(allRenderers, Part.Iris,          "iris");
        AddPrefabMapping(allRenderers, Part.Pupil,         "pupil", "pupille");
        AddPrefabMapping(allRenderers, Part.Lens,          "lens", "cristallin");
        AddPrefabMapping(allRenderers, Part.Retina,        "retina", "rétine", "retine");
        AddPrefabMapping(allRenderers, Part.Macula,        "macula", "fovea");
        AddPrefabMapping(allRenderers, Part.Muscles,       "muscle");
        AddPrefabMapping(allRenderers, Part.Sclera,        "sclera", "sclere");
        AddPrefabMapping(allRenderers, Part.Cornea,        "cornea", "cornée", "cornee");
        AddPrefabMapping(allRenderers, Part.Brain,         "brain", "cerveau", "cortex");
        AddPrefabMapping(allRenderers, Part.VisualCortex,  "cortex", "visual");
    }

    void AddPrefabMapping(Renderer[] renderers, Part p, params string[] keywords)
    {
        var list = new List<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null) continue;
            string name = r.gameObject.name.ToLowerInvariant();
            string parentName = r.transform.parent != null ? r.transform.parent.name.ToLowerInvariant() : "";
            foreach (var k in keywords)
            {
                if (name.Contains(k) || parentName.Contains(k)) { list.Add(r); break; }
            }
        }
        if (list.Count > 0) _prefabPartMap[p] = list;
    }

    // ────────────────────────────────────────────────────────────────────────
    // Shader & matériaux
    // ────────────────────────────────────────────────────────────────────────
    static Shader _cachedShader;
    static Shader FindShader()
    {
        if (_cachedShader != null) return _cachedShader;
        _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
        if (_cachedShader == null) _cachedShader = Shader.Find("Standard");
        if (_cachedShader == null) _cachedShader = Shader.Find("Sprites/Default");
        return _cachedShader;
    }

    static void MakeTransparent(Material m, Color c)
    {
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
        if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
        m.renderQueue = 3000;
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
    }
}
