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

    [Tooltip("Use world axis for movement (otherwise use object's local orientation)")]
    public bool useWorldAxis = true;

    [Header("Optional Bounds")]
    [Tooltip("Clamp position within the specified range")]
    public bool clampPosition = false;

    public Vector3 positionMin = new Vector3(-10f, 0f, -10f);
    public Vector3 positionMax = new Vector3(10f, 0f, 10f);

    public enum MovementPlane { XZ, XY }

    void Start()
    {
        if (potInput == null)
            potInput = GetComponentInChildren<ArduinoPotInput>();
        if (potInput == null)
            Debug.LogError("[JoystickMovement] ArduinoPotInput not found. Attach it or assign potInput.");
    }

    void Update()
    {
        if (potInput == null) return;

        float joyX = potInput.JoyX;
        float joyY = potInput.JoyY;

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

        if (moveDir.sqrMagnitude > 0.01f)
        {
            moveDir.Normalize();
            Vector3 delta = moveDir * (moveSpeed * Time.deltaTime);
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
    }
}
