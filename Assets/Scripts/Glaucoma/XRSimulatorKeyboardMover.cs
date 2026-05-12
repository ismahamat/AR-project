using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class XRSimulatorKeyboardMover : MonoBehaviour
{
    [Header("References")]
    public Transform headsetAnchor;

    [Header("Keyboard movement")]
    public bool enableKeyboardMovement = true;
    public float moveSpeed = 1.35f;
    public float fastMoveMultiplier = 2.5f;
    public float verticalMoveSpeed = 0.75f;

    private void LateUpdate()
    {
        if (!enableKeyboardMovement)
        {
            return;
        }

        Vector3 input = ReadMoveInput();
        float vertical = ReadVerticalInput();
        if (input.sqrMagnitude <= 0.0001f && Mathf.Abs(vertical) <= 0.0001f)
        {
            return;
        }

        Transform yawReference = headsetAnchor != null ? headsetAnchor : transform;
        Vector3 forward = Vector3.ProjectOnPlane(yawReference.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(yawReference.right, Vector3.up).normalized;

        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = transform.forward;
        }

        if (right.sqrMagnitude <= 0.0001f)
        {
            right = transform.right;
        }

        float speed = moveSpeed * ReadSpeedMultiplier();
        Vector3 delta = (forward * input.z + right * input.x) * speed;
        delta += Vector3.up * vertical * verticalMoveSpeed;
        transform.position += delta * Time.deltaTime;
    }

    private static Vector3 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector3.zero;
        }

        float horizontal = 0f;
        float forward = 0f;
        if (keyboard.aKey.isPressed || keyboard.qKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
        if (keyboard.wKey.isPressed || keyboard.zKey.isPressed || keyboard.upArrowKey.isPressed) forward += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) forward -= 1f;
#else
        float horizontal = 0f;
        float forward = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow)) forward += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) forward -= 1f;
#endif
        return Vector3.ClampMagnitude(new Vector3(horizontal, 0f, forward), 1f);
    }

    private static float ReadVerticalInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return 0f;
        }

        float vertical = 0f;
        if (keyboard.eKey.isPressed || keyboard.pageUpKey.isPressed) vertical += 1f;
        if (keyboard.cKey.isPressed || keyboard.pageDownKey.isPressed) vertical -= 1f;
        return vertical;
#else
        float vertical = 0f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.PageUp)) vertical += 1f;
        if (Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.PageDown)) vertical -= 1f;
        return vertical;
#endif
    }

    private float ReadSpeedMultiplier()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? fastMoveMultiplier : 1f;
#else
        return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? fastMoveMultiplier : 1f;
#endif
    }
}
