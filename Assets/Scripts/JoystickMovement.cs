using UnityEngine;

/// <summary>
/// Reads Arduino joystick (joyX, joyY from PotForUnity.ino) to control object movement.
/// Works with ArduinoPotInput: attach ArduinoPotInput on the same GameObject or a child, or assign potInput manually.
/// </summary>
public class JoystickMovement : MonoBehaviour
{
    [Header("Joystick Data Source")]
    public ArduinoPotInput potInput;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;

    public MovementPlane movementPlane = MovementPlane.XZ;

    [Header("Jitter Filter")]
    [Tooltip("最小摇杆输入幅度，小于该值则忽略（0~1，0 表示不启用）。")]
    [Range(0f, 1f)]
    public float minInputMagnitude = 0f;

    [Tooltip("Use world axis for movement (otherwise use object's local orientation)")]
    public bool useWorldAxis = true;

    [Header("Rigidbody Movement")]
    [Tooltip("When present, movement will be done via Rigidbody.MovePosition in FixedUpdate.")]
    public bool useRigidbodyMovement = true;

    [Tooltip("If no Rigidbody found and this is enabled, will fall back to transform.position movement.")]
    public bool fallbackToTransformWhenNoRigidbody = true;

    [Header("Keyboard Input")]
    [Tooltip("启用键盘移动输入（WASD + 方向键）。会与 Arduino 摇杆输入叠加。")]
    public bool enableKeyboardInput = true;

    [Tooltip("多久内没有收到数据就认为“无 Arduino 数据”。")]
    public float arduinoNoDataTimeout = 0.5f;

    [Header("Optional Bounds")]
    [Tooltip("Clamp position within the specified range")]
    public bool clampPosition = false;

    public Vector3 positionMin = new Vector3(-10f, 0f, -10f);
    public Vector3 positionMax = new Vector3(10f, 0f, 10f);

    public enum MovementPlane { XZ, XY }

    Rigidbody _rb;
    Vector3 _pendingMoveDir = Vector3.zero;
    bool _hasMoveInput;

    void Start()
    {
        if (potInput == null)
            potInput = GetComponentInChildren<ArduinoPotInput>();
        if (potInput == null)
            Debug.LogError("[JoystickMovement] ArduinoPotInput not found. Attach it or assign potInput.");

        if (useRigidbodyMovement)
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = GetComponentInChildren<Rigidbody>();

            if (_rb == null)
            {
                Debug.LogError("[JoystickMovement] Rigidbody not found but `useRigidbodyMovement` is enabled. " +
                               "Add a Rigidbody to the moving GameObject (or child), or disable `useRigidbodyMovement`.");
                if (!fallbackToTransformWhenNoRigidbody)
                {
                    enabled = false;
                    return;
                }
            }
        }
    }

    void Update()
    {
        bool hasArduinoJoy = potInput != null && potInput.IsConnected && potInput.HasReceivedRecently(arduinoNoDataTimeout);

        float arduinoX = hasArduinoJoy ? potInput.JoyX : 0f;
        float arduinoY = hasArduinoJoy ? potInput.JoyY : 0f;

        float keyboardX = 0f;
        float keyboardY = 0f;
        if (enableKeyboardInput)
        {
            // WASD + Arrow 共同控制（相反方向可互相抵消）
            keyboardX =
                (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) +
                (Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) +
                (Input.GetKey(KeyCode.D) ? 1f : 0f) +
                (Input.GetKey(KeyCode.A) ? -1f : 0f);
            keyboardY =
                (Input.GetKey(KeyCode.UpArrow) ? 1f : 0f) +
                (Input.GetKey(KeyCode.DownArrow) ? -1f : 0f) +
                (Input.GetKey(KeyCode.W) ? 1f : 0f) +
                (Input.GetKey(KeyCode.S) ? -1f : 0f);
        }

        float joyX = Mathf.Clamp(arduinoX + keyboardX, -1f, 1f);
        float joyY = Mathf.Clamp(arduinoY + keyboardY, -1f, 1f);

        Vector3 moveDir;
        if (useWorldAxis)
        {
            if (movementPlane == MovementPlane.XZ)
                moveDir = new Vector3(joyX, 0f, joyY);
            else
                moveDir = new Vector3(joyX, joyY, 0f);
        }
        else
        {
            if (movementPlane == MovementPlane.XZ)
                moveDir = transform.TransformDirection(new Vector3(joyX, 0f, joyY));
            else
                moveDir = transform.TransformDirection(new Vector3(joyX, joyY, 0f));
        }

        float sqrMag = moveDir.sqrMagnitude;
        if (minInputMagnitude > 0f)
        {
            float threshold = Mathf.Clamp01(minInputMagnitude);
            float thresholdSqr = threshold * threshold;
            _hasMoveInput = sqrMag > thresholdSqr;
        }
        else
        {
            _hasMoveInput = sqrMag > 0.01f;
        }

        _pendingMoveDir = _hasMoveInput ? moveDir.normalized : Vector3.zero;

        // Fallback behavior: if Rigidbody movement isn't possible, keep original transform-based movement.
        if (!_hasMoveInput) return;
        if (useRigidbodyMovement && _rb != null) return;
        if (!fallbackToTransformWhenNoRigidbody) return;

        Vector3 delta = _pendingMoveDir * (moveSpeed * Time.deltaTime);
        transform.position += delta;

        if (clampPosition)
        {
            Vector3 p = transform.position;
            transform.position = new Vector3(
                Mathf.Clamp(p.x, positionMin.x, positionMax.x),
                Mathf.Clamp(p.y, positionMin.y, positionMax.y),
                Mathf.Clamp(p.z, positionMin.z, positionMax.z)
            );
        }
    }

    void FixedUpdate()
    {
        if (!useRigidbodyMovement || _rb == null) return;

        // 用速度控制水平移动，保留竖直速度，避免 MovePosition 把重力“清零”导致下沉/穿地
        Vector3 vel = _rb.linearVelocity;
        if (movementPlane == MovementPlane.XZ)
        {
            vel.x = _pendingMoveDir.x * moveSpeed;
            vel.z = _pendingMoveDir.z * moveSpeed;
            // vel.y 保持原样，交给重力和地面碰撞
        }
        else
        {
            vel.x = _pendingMoveDir.x * moveSpeed;
            vel.y = _pendingMoveDir.y * moveSpeed;
            vel.z = 0f;
        }

        if (clampPosition)
        {
            Vector3 nextPos = _rb.position + vel * Time.fixedDeltaTime;
            if (nextPos.x < positionMin.x && vel.x < 0f) vel.x = 0f;
            if (nextPos.x > positionMax.x && vel.x > 0f) vel.x = 0f;
            if (nextPos.y < positionMin.y && vel.y < 0f) vel.y = 0f;
            if (nextPos.y > positionMax.y && vel.y > 0f) vel.y = 0f;
            if (nextPos.z < positionMin.z && vel.z < 0f) vel.z = 0f;
            if (nextPos.z > positionMax.z && vel.z > 0f) vel.z = 0f;
        }

        _rb.linearVelocity = vel;
    }
}
