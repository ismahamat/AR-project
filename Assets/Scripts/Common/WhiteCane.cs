using UnityEngine;

[DisallowMultipleComponent]
public class WhiteCane : MonoBehaviour
{
    public enum Hand { Left, Right }

    [Header("Attachement")]
    public Hand hand = Hand.Left;
    public Transform overrideAnchor;
    public Vector3 localOffset = new Vector3(0f, -0.02f, 0.04f);
    public Vector3 localEuler = new Vector3(60f, 0f, 0f);

    [Header("Forme")]
    public float length = 1.20f;
    public float diameter = 0.020f;
    public Color shaftColor = Color.white;
    public Color tipColor   = new Color(0.85f, 0.10f, 0.10f);
    public float tipFraction = 0.10f;

    [Header("Détection")]
    public LayerMask collisionMask = ~0;
    [Tooltip("Délai mini entre deux vibrations (s).")]
    public float retriggerCooldown = 0.08f;

    [Header("Toggle")]
    [Tooltip("Bouton pour montrer/cacher la canne (Y / Four sur la manette gauche).")]
    public OVRInput.Button toggleButton = OVRInput.Button.Four;
    public bool startVisible = true;

    Transform _anchor;
    GameObject _shaft, _tip;
    SphereCollider _trigger;
    Rigidbody _rb;
    float _lastHitTime;
    bool _visible;

    void Awake()
    {
        Build();
        SetVisible(startVisible);
    }

    void Start()
    {
        ResolveAnchor();
        if (_anchor != null) AttachTo(_anchor);
    }

    void Update()
    {
        bool down = false;
        try { down = OVRInput.GetDown(toggleButton); } catch { }
        if (down) SetVisible(!_visible);
    }

    void ResolveAnchor()
    {
        if (overrideAnchor != null) { _anchor = overrideAnchor; return; }
        var rig = FindAnyObjectByType<OVRCameraRig>();
        if (rig != null) _anchor = (hand == Hand.Left) ? rig.leftHandAnchor : rig.rightHandAnchor;
    }

    public void AttachTo(Transform anchor)
    {
        _anchor = anchor;
        transform.SetParent(anchor, false);
        transform.localPosition = localOffset;
        transform.localRotation = Quaternion.Euler(localEuler);
        transform.localScale = Vector3.one;
    }

    public void SetVisible(bool v)
    {
        _visible = v;
        if (_shaft != null) _shaft.SetActive(v);
        if (_tip != null) _tip.SetActive(v);
        if (_trigger != null) _trigger.enabled = v;
    }

    void Build()
    {
        gameObject.layer = LayerMask.NameToLayer("Default");

        _shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        _shaft.name = "Shaft";
        DestroyImmediate(_shaft.GetComponent<Collider>());
        _shaft.transform.SetParent(transform, false);
        _shaft.transform.localPosition = new Vector3(0, 0, length * 0.5f);
        _shaft.transform.localRotation = Quaternion.Euler(90, 0, 0);
        _shaft.transform.localScale = new Vector3(diameter, length * 0.5f, diameter);
        var sm = _shaft.GetComponent<Renderer>();
        sm.sharedMaterial = MakeUnlitMaterial(shaftColor);
        sm.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sm.receiveShadows = false;

        _tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        _tip.name = "Tip";
        DestroyImmediate(_tip.GetComponent<Collider>());
        _tip.transform.SetParent(transform, false);
        _tip.transform.localPosition = new Vector3(0, 0, length);
        _tip.transform.localScale = Vector3.one * (diameter * 2.4f);
        var tr = _tip.GetComponent<Renderer>();
        tr.sharedMaterial = MakeUnlitMaterial(tipColor);
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        _trigger = gameObject.AddComponent<SphereCollider>();
        _trigger.isTrigger = true;
        _trigger.center = new Vector3(0, 0, length);
        _trigger.radius = diameter * 1.4f;

        _rb = gameObject.AddComponent<Rigidbody>();
        _rb.isKinematic = true;
        _rb.useGravity = false;
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void OnTriggerEnter(Collider other) => HandleHit(other);
    void OnTriggerStay(Collider other)  => HandleHit(other);

    void HandleHit(Collider other)
    {
        if (!_visible) return;
        if ((collisionMask.value & (1 << other.gameObject.layer)) == 0) return;
        if (Time.unscaledTime - _lastHitTime < retriggerCooldown) return;
        _lastHitTime = Time.unscaledTime;
        var ctrl = (hand == Hand.Left) ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        HapticFeedback.Bump(ctrl);
    }

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
}
