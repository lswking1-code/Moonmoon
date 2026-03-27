using UnityEngine;

/// <summary>
/// Moves using Arduino joystick (JoyX/JoyY from PotForUnity.ino) via ArduinoPotInput, plus optional WASD/arrows.
/// </summary>
public class JoystickMovement : MonoBehaviour
{
    [Header("Source")]
    public ArduinoPotInput potInput;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public MovementPlane movementPlane = MovementPlane.XZ;
    [Range(0f, 1f)]
    public float minInputMagnitude = 0f;
    public bool useWorldAxis = true;

    [Header("Rigidbody")]
    public bool useRigidbodyMovement = true;
    public bool fallbackToTransformWhenNoRigidbody = true;

    [Header("Keyboard")]
    public bool enableKeyboardInput = true;
    public float arduinoNoDataTimeout = 0.5f;

    [Header("Facing")]
    public bool faceMovementDirection = true;
    public float faceRotationSpeedDegPerSec = 540f;

    [Header("Bounds")]
    public bool clampPosition = false;
    public Vector3 positionMin = new Vector3(-10f, 0f, -10f);
    public Vector3 positionMax = new Vector3(10f, 0f, 10f);

    [Header("Animator")]
    public Animator animator;
    public string speedParameterName = "Speed";

    public enum MovementPlane { XZ, XY }

    Rigidbody _rb;
    Vector3 _pendingMoveDir;
    bool _hasMoveInput;
    int _speedParamHash;

    void Start()
    {
        potInput ??= GetComponentInChildren<ArduinoPotInput>();
        if (potInput == null)
            Debug.LogError("[JoystickMovement] ArduinoPotInput not found. Attach it or assign potInput.");

        if (useRigidbodyMovement)
        {
            _rb = GetComponent<Rigidbody>() ?? GetComponentInChildren<Rigidbody>();
            if (_rb == null)
            {
                Debug.LogError("[JoystickMovement] Rigidbody required when useRigidbodyMovement is on, or turn it off.");
                if (!fallbackToTransformWhenNoRigidbody) { enabled = false; return; }
            }
        }

        if (!string.IsNullOrEmpty(speedParameterName))
            _speedParamHash = Animator.StringToHash(speedParameterName);
        animator ??= GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
    }

    void Update()
    {
        bool live = potInput != null && potInput.IsConnected && potInput.HasReceivedRecently(arduinoNoDataTimeout);
        float ax = live ? potInput.JoyX : 0f;
        float ay = live ? potInput.JoyY : 0f;

        float kx = 0f, ky = 0f;
        if (enableKeyboardInput)
        {
            float Axis(KeyCode neg, KeyCode pos) => (Input.GetKey(pos) ? 1f : 0f) + (Input.GetKey(neg) ? -1f : 0f);
            kx = Axis(KeyCode.A, KeyCode.D) + Axis(KeyCode.LeftArrow, KeyCode.RightArrow);
            ky = Axis(KeyCode.S, KeyCode.W) + Axis(KeyCode.DownArrow, KeyCode.UpArrow);
        }

        float joyX = Mathf.Clamp(ax + kx, -1f, 1f);
        float joyY = Mathf.Clamp(ay + ky, -1f, 1f);

        Vector3 local = movementPlane == MovementPlane.XZ
            ? new Vector3(joyX, 0f, joyY)
            : new Vector3(joyX, joyY, 0f);
        Vector3 moveDir = useWorldAxis ? local : transform.TransformDirection(local);

        float deadSqr = minInputMagnitude > 0f
            ? Mathf.Clamp01(minInputMagnitude) * Mathf.Clamp01(minInputMagnitude)
            : 0.01f;
        _hasMoveInput = moveDir.sqrMagnitude > deadSqr;
        _pendingMoveDir = _hasMoveInput ? moveDir.normalized : Vector3.zero;

        if (!_hasMoveInput) return;
        if (useRigidbodyMovement && _rb != null) return;
        if (!fallbackToTransformWhenNoRigidbody) return;

        transform.position += _pendingMoveDir * (moveSpeed * Time.deltaTime);
        if (clampPosition)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(
                Mathf.Clamp(p.x, positionMin.x, positionMax.x),
                Mathf.Clamp(p.y, positionMin.y, positionMax.y),
                Mathf.Clamp(p.z, positionMin.z, positionMax.z));
        }
    }

    static void StopIfLeavingBounds(ref float vel, float next, float min, float max)
    {
        if ((next < min && vel < 0f) || (next > max && vel > 0f)) vel = 0f;
    }

    void FixedUpdate()
    {
        if (!useRigidbodyMovement || _rb == null) return;

        Vector3 vel = _rb.linearVelocity;
        if (movementPlane == MovementPlane.XZ)
        {
            vel.x = _pendingMoveDir.x * moveSpeed;
            vel.z = _pendingMoveDir.z * moveSpeed;
        }
        else
        {
            vel.x = _pendingMoveDir.x * moveSpeed;
            vel.y = _pendingMoveDir.y * moveSpeed;
            vel.z = 0f;
        }

        if (clampPosition)
        {
            Vector3 n = _rb.position + vel * Time.fixedDeltaTime;
            StopIfLeavingBounds(ref vel.x, n.x, positionMin.x, positionMax.x);
            StopIfLeavingBounds(ref vel.y, n.y, positionMin.y, positionMax.y);
            StopIfLeavingBounds(ref vel.z, n.z, positionMin.z, positionMax.z);
        }

        _rb.linearVelocity = vel;
    }

    void LateUpdate()
    {
        if (faceMovementDirection && _hasMoveInput)
        {
            Quaternion target = FacingFromWorldDir(_pendingMoveDir);
            transform.rotation = faceRotationSpeedDegPerSec <= 0f
                ? target
                : Quaternion.RotateTowards(transform.rotation, target, faceRotationSpeedDegPerSec * Time.deltaTime);
        }

        if (animator != null && !string.IsNullOrEmpty(speedParameterName))
            animator.SetFloat(_speedParamHash, PlanarSpeed());
    }

    Quaternion FacingFromWorldDir(Vector3 worldMoveDir)
    {
        if (movementPlane == MovementPlane.XZ)
        {
            Vector3 f = new Vector3(worldMoveDir.x, 0f, worldMoveDir.z);
            if (f.sqrMagnitude < 1e-8f) return transform.rotation;
            return Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        Vector3 f2 = new Vector3(worldMoveDir.x, worldMoveDir.y, 0f);
        if (f2.sqrMagnitude < 1e-8f) return transform.rotation;
        return Quaternion.LookRotation(f2.normalized, Vector3.forward);
    }

    float PlanarSpeed()
    {
        if (useRigidbodyMovement && _rb != null)
        {
            Vector3 v = _rb.linearVelocity;
            return movementPlane == MovementPlane.XZ
                ? new Vector3(v.x, 0f, v.z).magnitude
                : new Vector3(v.x, v.y, 0f).magnitude;
        }
        return (_pendingMoveDir * moveSpeed).magnitude;
    }
}
