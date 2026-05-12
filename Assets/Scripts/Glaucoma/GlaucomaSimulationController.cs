using UnityEngine;
using UnityEngine.Rendering;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class GlaucomaSimulationController : MonoBehaviour
{
    [Header("XR target")]
    [Tooltip("Head/camera transform that receives the glaucoma overlay. If empty, CenterEyeAnchor or Camera.main is used.")]
    public Transform headsetAnchor;

    [Tooltip("Camera used to size the overlay plane. If empty, the camera on the headset anchor is used.")]
    public Camera targetCamera;

    [Header("Glaucoma simulation")]
    [Range(0f, 1f)] public float severity = 0.85f;
    [Range(0.1f, 0.55f)] public float mildCentralVisionRadius = 0.42f;
    [Range(0.05f, 0.35f)] public float severeCentralVisionRadius = 0.18f;
    [Range(0.02f, 0.25f)] public float transitionFeather = 0.09f;
    [Range(0f, 1f)] public float peripheralDarkness = 0.96f;
    [Range(0f, 1f)] public float blindSpotStrength = 0.65f;
    [Range(0f, 0.35f)] public float hazeNoise = 0.12f;
    [Range(0f, 0.45f)] public float milkyVeil = 0.18f;
    public Color fogTint = new Color(0.015f, 0.013f, 0.01f, 1f);

    [Header("Overlay geometry")]
    [Min(0.11f)] public float overlayDistance = 0.18f;
    [Range(1f, 1.6f)] public float overlayScaleMargin = 1.18f;

    [Header("Runtime controls")]
    public bool simulationActive = true;
    public bool allowKeyboardTuning = true;

    private const string OverlayName = "Glaucoma_HeadLocked_Overlay";
    private Transform overlayTransform;
    private MeshRenderer overlayRenderer;
    private MeshFilter overlayFilter;
    private Material overlayMaterial;

    private static readonly int ClearRadiusId = Shader.PropertyToID("_ClearRadius");
    private static readonly int FeatherId = Shader.PropertyToID("_Feather");
    private static readonly int DarknessId = Shader.PropertyToID("_Darkness");
    private static readonly int SpotStrengthId = Shader.PropertyToID("_SpotStrength");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int VeilStrengthId = Shader.PropertyToID("_VeilStrength");
    private static readonly int FogTintId = Shader.PropertyToID("_FogTint");
    private static Mesh sharedQuad;

    private void OnEnable()
    {
        ResolveTarget();
        EnsureOverlay();
        ApplySimulation();
        ApplyVisibility();
    }

    private void OnDisable()
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = false;
        }

    }

    private void Update()
    {
        if (headsetAnchor == null || targetCamera == null)
        {
            ResolveTarget();
        }

        if (Application.isPlaying && allowKeyboardTuning)
        {
            HandleKeyboardTuning();
        }

        EnsureOverlay();
        ApplySimulation();
        ApplyVisibility();
    }

    private void OnValidate()
    {
        severity = Mathf.Clamp01(severity);
        mildCentralVisionRadius = Mathf.Max(mildCentralVisionRadius, severeCentralVisionRadius + 0.01f);
        ResolveTarget();
        EnsureOverlay();
        ApplySimulation();
        ApplyVisibility();
    }

    public void SetSimulationActive(bool active)
    {
        simulationActive = active;
        ResolveTarget();
        EnsureOverlay();
        ApplySimulation();
        ApplyVisibility();
    }

    private void ResolveTarget()
    {
        if (headsetAnchor == null && Camera.main != null)
        {
            headsetAnchor = Camera.main.transform;
        }

        if (targetCamera == null && headsetAnchor != null)
        {
            targetCamera = headsetAnchor.GetComponent<Camera>();
        }
    }

    private void EnsureOverlay()
    {
        if (headsetAnchor == null)
        {
            return;
        }

        if (overlayTransform == null)
        {
            Transform existing = headsetAnchor.Find(OverlayName);
            if (existing != null)
            {
                overlayTransform = existing;
            }
            else
            {
                GameObject overlay = new GameObject(OverlayName);
                overlayTransform = overlay.transform;
                overlayTransform.SetParent(headsetAnchor, false);
            }
        }

        overlayFilter = overlayTransform.GetComponent<MeshFilter>();
        if (overlayFilter == null)
        {
            overlayFilter = overlayTransform.gameObject.AddComponent<MeshFilter>();
        }

        overlayRenderer = overlayTransform.GetComponent<MeshRenderer>();
        if (overlayRenderer == null)
        {
            overlayRenderer = overlayTransform.gameObject.AddComponent<MeshRenderer>();
        }

        overlayFilter.sharedMesh = GetQuadMesh();
        overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
        overlayRenderer.receiveShadows = false;
        overlayRenderer.enabled = simulationActive;
        overlayRenderer.allowOcclusionWhenDynamic = false;
        overlayRenderer.sortingOrder = short.MaxValue;

        Shader shader = Shader.Find("ARProject/GlaucomaTunnelVision");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }

        if (overlayMaterial == null || overlayMaterial.shader != shader)
        {
            overlayMaterial = new Material(shader)
            {
                name = "Runtime_GlaucomaTunnelVision_Material",
                hideFlags = HideFlags.DontSave
            };
        }

        overlayRenderer.sharedMaterial = overlayMaterial;
        overlayTransform.localPosition = Vector3.forward * overlayDistance;
        overlayTransform.localRotation = Quaternion.identity;

        float fieldOfView = targetCamera != null ? targetCamera.fieldOfView : 90f;
        float aspect = targetCamera != null && targetCamera.aspect > 0.01f ? targetCamera.aspect : 1.777f;
        float height = 2f * overlayDistance * Mathf.Tan(fieldOfView * Mathf.Deg2Rad * 0.5f) * overlayScaleMargin;
        float width = height * aspect;
        overlayTransform.localScale = new Vector3(width, height, 1f);
    }

    private void ApplySimulation()
    {
        if (overlayMaterial == null)
        {
            return;
        }

        float radius = Mathf.Lerp(mildCentralVisionRadius, severeCentralVisionRadius, severity);
        float darkness = Mathf.Lerp(0.45f, peripheralDarkness, severity);
        float spots = Mathf.Lerp(0.1f, blindSpotStrength, severity);
        float noise = Mathf.Lerp(0.02f, hazeNoise, severity);
        float veil = Mathf.Lerp(0.04f, milkyVeil, severity);

        overlayMaterial.SetFloat(ClearRadiusId, radius);
        overlayMaterial.SetFloat(FeatherId, transitionFeather);
        overlayMaterial.SetFloat(DarknessId, darkness);
        overlayMaterial.SetFloat(SpotStrengthId, spots);
        overlayMaterial.SetFloat(NoiseStrengthId, noise);
        overlayMaterial.SetFloat(VeilStrengthId, veil);
        overlayMaterial.SetColor(FogTintId, fogTint);

        if (overlayMaterial.shader.name == "Unlit/Color")
        {
            overlayMaterial.color = new Color(0f, 0f, 0f, darkness);
        }

    }

    private void ApplyVisibility()
    {
        if (overlayRenderer != null)
        {
            overlayRenderer.enabled = simulationActive;
        }

    }

    private void HandleKeyboardTuning()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame) severity = 0.25f;
        if (keyboard.digit2Key.wasPressedThisFrame) severity = 0.5f;
        if (keyboard.digit3Key.wasPressedThisFrame) severity = 0.75f;
        if (keyboard.digit4Key.wasPressedThisFrame) severity = 1f;
        if (keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame) severity += 0.05f;
        if (keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame) severity -= 0.05f;
#else
        if (Input.GetKeyDown(KeyCode.Alpha1)) severity = 0.25f;
        if (Input.GetKeyDown(KeyCode.Alpha2)) severity = 0.5f;
        if (Input.GetKeyDown(KeyCode.Alpha3)) severity = 0.75f;
        if (Input.GetKeyDown(KeyCode.Alpha4)) severity = 1f;
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus)) severity += 0.05f;
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus)) severity -= 0.05f;
#endif

        severity = Mathf.Clamp01(severity);
    }

    private static Mesh GetQuadMesh()
    {
        if (sharedQuad != null)
        {
            return sharedQuad;
        }

        sharedQuad = new Mesh
        {
            name = "Glaucoma Overlay Quad",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };
        sharedQuad.RecalculateBounds();
        return sharedQuad;
    }
}
