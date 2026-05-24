using UnityEngine;

/// <summary>
/// Saisie longue-distance ("force grab") de l'œil 3D : tiens la gâchette latérale
/// (grip) et l'œil est attiré dans ta main, où qu'il soit (jusqu'à forceGrabRange).
/// Lâche → retour fluide à la position d'origine sur le panel.
/// </summary>
[DisallowMultipleComponent]
public class EyeGrabbable : MonoBehaviour
{
    [Header("Saisie")]
    [Tooltip("Distance maximale main → centre de l'œil pour déclencher la saisie à distance.")]
    public float forceGrabRange = 3.0f;
    [Tooltip("Position locale dans la main (offset paume) une fois la saisie terminée.")]
    public Vector3 grabPoseInHand = new Vector3(0f, -0.03f, 0.10f);
    [Tooltip("Vitesse à laquelle l'œil rejoint la pose de la main.")]
    public float pullSpeed = 8f;
    [Tooltip("Vitesse de retour à la pose d'origine après lâcher.")]
    public float returnSpeed = 4f;
    public OVRInput.Button leftGrabButton  = OVRInput.Button.SecondaryHandTrigger;
    public OVRInput.Button rightGrabButton = OVRInput.Button.PrimaryHandTrigger;

    OVRCameraRig _rig;
    Transform _homeParent;
    Vector3 _homeLocalPos;
    Quaternion _homeLocalRot;
    Vector3 _homeLocalScale;
    Transform _grabbingHand;
    OVRInput.Controller _grabbingController;
    bool _settling;        // l'œil glisse vers la pose comfortable de la main
    float _settleTime;
    bool _returning;       // après lâcher, retour vers l'origine
    EyeAnatomyView _eye;
    bool _wasAutoRotating;
    bool _warned;

    void Awake()
    {
        _homeParent     = transform.parent;
        _homeLocalPos   = transform.localPosition;
        _homeLocalRot   = transform.localRotation;
        _homeLocalScale = transform.localScale;
        _eye            = GetComponent<EyeAnatomyView>();
    }

    void Start()
    {
        _rig = FindAnyObjectByType<OVRCameraRig>();
    }

    void Update()
    {
        if (_rig == null)
        {
            _rig = FindAnyObjectByType<OVRCameraRig>();
            if (_rig == null)
            {
                if (!_warned) { Debug.LogWarning("[EyeGrabbable] OVRCameraRig introuvable — l'œil ne sera pas saisissable."); _warned = true; }
                return;
            }
        }

        if (_grabbingHand != null)
        {
            // Glissement progressif vers la pose de la main
            if (_settling)
            {
                transform.localPosition = Vector3.Lerp(transform.localPosition, grabPoseInHand, Time.deltaTime * pullSpeed);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, Quaternion.identity, Time.deltaTime * pullSpeed);
                _settleTime += Time.deltaTime;
                if (Vector3.Distance(transform.localPosition, grabPoseInHand) < 0.01f || _settleTime > 1.5f)
                {
                    transform.localPosition = grabPoseInHand;
                    transform.localRotation = Quaternion.identity;
                    _settling = false;
                }
            }

            // Vérifie le relâchement de la gâchette
            bool stillHolding = false;
            try
            {
                if (_grabbingController == OVRInput.Controller.LTouch)
                    stillHolding = OVRInput.Get(leftGrabButton, OVRInput.Controller.LTouch);
                else if (_grabbingController == OVRInput.Controller.RTouch)
                    stillHolding = OVRInput.Get(rightGrabButton, OVRInput.Controller.RTouch);
            }
            catch { }
            if (!stillHolding) ReleaseGrab();
            return;
        }

        // Force-grab : tant que la gâchette est tenue + main dans le rayon, on saisit
        bool leftHeld  = SafeHeld(leftGrabButton,  OVRInput.Controller.LTouch);
        bool rightHeld = SafeHeld(rightGrabButton, OVRInput.Controller.RTouch);
        if (leftHeld && _rig.leftHandAnchor != null)
            TryGrab(_rig.leftHandAnchor, OVRInput.Controller.LTouch);
        else if (rightHeld && _rig.rightHandAnchor != null)
            TryGrab(_rig.rightHandAnchor, OVRInput.Controller.RTouch);

        // Retour fluide à la position d'origine après lâcher
        if (_returning)
        {
            transform.localPosition = Vector3.Lerp(transform.localPosition, _homeLocalPos, Time.deltaTime * returnSpeed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, _homeLocalRot, Time.deltaTime * returnSpeed);
            transform.localScale    = Vector3.Lerp(transform.localScale, _homeLocalScale, Time.deltaTime * returnSpeed);
            if (Vector3.Distance(transform.localPosition, _homeLocalPos) < 0.005f)
            {
                transform.localPosition = _homeLocalPos;
                transform.localRotation = _homeLocalRot;
                transform.localScale    = _homeLocalScale;
                _returning = false;
            }
        }
    }

    void TryGrab(Transform hand, OVRInput.Controller c)
    {
        if (hand == null) return;
        float d = Vector3.Distance(hand.position, transform.position);
        if (d > forceGrabRange) return;

        transform.SetParent(hand, true);  // on préserve la pose monde au moment de la saisie
        _grabbingHand = hand;
        _grabbingController = c;
        _settling = true;
        _settleTime = 0f;
        _returning = false;
        if (_eye != null)
        {
            _wasAutoRotating = _eye.autoRotate;
            _eye.autoRotate = false;
        }
        HapticFeedback.Tap(c);
    }

    void ReleaseGrab()
    {
        transform.SetParent(_homeParent, true);
        _grabbingHand = null;
        _settling = false;
        _returning = true;
        if (_eye != null) _eye.autoRotate = _wasAutoRotating;
    }

    static bool SafeHeld(OVRInput.Button b, OVRInput.Controller c)
    {
        try { return OVRInput.Get(b, c); } catch { return false; }
    }
}
