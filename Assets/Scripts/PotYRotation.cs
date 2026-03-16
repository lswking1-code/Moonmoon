using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls object rotation around Y axis using Arduino potentiometer value.
/// Can read directly from ArduinoPotInput or via Input System Potentiometer Action.
/// </summary>
[RequireComponent(typeof(Transform))]
public class PotYRotation : MonoBehaviour
{
    [Header("Rotation Target")]
    public GameObject rotationTarget;

    [Header("Potentiometer Source")]
    public bool useInputSystem;

    public InputActionReference potentiometerAction;

    public ArduinoPotInput potInput;

    [Header("Y Axis Rotation Range (degrees)")]
    [Tooltip("Y axis angle when potentiometer is 0")]
    public float minAngleY = 0f;

    [Tooltip("Y axis angle when potentiometer is 1")]
    public float maxAngleY = 360f;

    [Tooltip("When checked, invert rotation direction (angle decreases as potentiometer increases)")]
    public bool invertDirection = false;

    [Header("Optional: Smoothing")]
    [Tooltip("When > 0, smooth interpolation is applied to rotation")]
    [Range(0f, 20f)]
    public float smoothSpeed = 0f;

    [Header("Debug")]
    public bool showRotationDebug = true;

    float _currentAngleY;
    Transform _targetTransform;
    bool _startOk;
    float _initialLocalX, _initialLocalZ;

    void Start()
    {
        _targetTransform = rotationTarget != null ? rotationTarget.transform : transform;
        if (_targetTransform == null) { Debug.LogError("[PotYRotation] Rotation target is invalid (rotationTarget destroyed?)."); return; }

        var euler = _targetTransform.localEulerAngles;
        _initialLocalX = euler.x;
        _initialLocalZ = euler.z;

        if (potInput == null) potInput = GetComponentInChildren<ArduinoPotInput>() ?? FindObjectOfType<ArduinoPotInput>();

        _startOk = (useInputSystem && potentiometerAction != null) || potInput != null;
        if (!_startOk) { Debug.LogError("[PotYRotation] No potentiometer data source found. Uncheck Use Input System and ensure ArduinoPotInput is in the scene, or assign Pot Input manually."); return; }

        if (useInputSystem && potentiometerAction != null) potentiometerAction.action.Enable();
        _currentAngleY = minAngleY;
        ApplyRotation(minAngleY);
    }

    float GetNormalizedValue()
    {
        if (useInputSystem && potentiometerAction != null)
        {
            float v = Mathf.Clamp01(potentiometerAction.action.ReadValue<float>());
            if (v > 0f || potInput == null) return v;
        }
        return potInput != null ? Mathf.Clamp01(potInput.GetNormalized()) : 0f;
    }

    void Update()
    {
        if (!_startOk || _targetTransform == null) return;
        float t = GetNormalizedValue();
        if (invertDirection) t = 1f - t;
        float targetAngleY = Mathf.Lerp(minAngleY, maxAngleY, t);
        _currentAngleY = smoothSpeed > 0f ? Mathf.LerpAngle(_currentAngleY, targetAngleY, smoothSpeed * Time.deltaTime) : targetAngleY;
        ApplyRotation(_currentAngleY);
    }

    void OnDestroy()
    {
        if (useInputSystem && potentiometerAction != null && potentiometerAction.action != null)
            potentiometerAction.action.Disable();
    }

    void ApplyRotation(float angleY)
    {
        if (_targetTransform == null) return;
        float y = Mathf.Clamp(angleY, Mathf.Min(minAngleY, maxAngleY), Mathf.Max(minAngleY, maxAngleY));
        _targetTransform.localEulerAngles = new Vector3(_initialLocalX, y, _initialLocalZ);
    }

    string GetSourceLabel()
    {
        if (!useInputSystem || potentiometerAction == null) return potInput != null ? "ArduinoPot" : "None";
        float fromAction = Mathf.Clamp01(potentiometerAction.action.ReadValue<float>());
        return fromAction > 0f ? "InputSystem" : (potInput != null ? "ArduinoPot(fallback)" : "InputSystem=0");
    }

    void OnGUI()
    {
        if (!showRotationDebug || !_startOk) return;
        float t = GetNormalizedValue();
        if (invertDirection) t = 1f - t;
        float angleY = Mathf.Lerp(minAngleY, maxAngleY, t);
        string targetName = _targetTransform != null ? _targetTransform.name : "?";
        GUI.Label(new Rect(10, 120, 320, 60), $"[PotYRotation]\nSource: {GetSourceLabel()}  Normalized: {t:F3}  Target angle Y: {angleY:F1}°\nRotating: {targetName}");
    }
}
