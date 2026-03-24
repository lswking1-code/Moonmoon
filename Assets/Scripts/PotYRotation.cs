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

    [Header("Jitter Filter")]
    [Tooltip("最小一次变化角度，小于该值则忽略（度）。0 表示不启用。")]
    public float minAngleStep = 0f;

    [Header("Optional: Smoothing")]
    [Tooltip("When > 0, smooth interpolation is applied to rotation")]
    [Range(0f, 20f)]
    public float smoothSpeed = 0f;

    [Header("MouseWheel Fallback (No Arduino)")]
    [Tooltip("串口未连接或一段时间内没有收到 Arduino 数据时，用鼠标滚轮控制 Y 旋转。")]
    public bool enableMouseWheelFallback = true;

    [Tooltip("每个滚轮单位对应的 Y 角度增量（度）。")]
    public float wheelAngleStep = 30f;

    [Tooltip("多久内没有收到数据就认为“无 Arduino 数据”。")]
    public float arduinoNoDataTimeout = 0.5f;

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

        if (enableMouseWheelFallback && ShouldUseMouseWheelFallback())
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                _currentAngleY += scroll * wheelAngleStep; // up -> increase, down -> decrease

                float low = Mathf.Min(minAngleY, maxAngleY);
                float high = Mathf.Max(minAngleY, maxAngleY);
                _currentAngleY = Mathf.Clamp(_currentAngleY, low, high);

                ApplyRotation(_currentAngleY);
            }
            return;
        }

        float t = GetNormalizedValue();
        if (invertDirection) t = 1f - t;
        float targetAngleY = Mathf.Lerp(minAngleY, maxAngleY, t);
        if (minAngleStep > 0f)
        {
            float deltaToTarget = Mathf.DeltaAngle(_currentAngleY, targetAngleY);
            if (Mathf.Abs(deltaToTarget) < minAngleStep)
                return;
        }
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
        if (enableMouseWheelFallback && ShouldUseMouseWheelFallback())
            return "MouseWheel";

        if (!useInputSystem || potentiometerAction == null) return potInput != null ? "ArduinoPot" : "None";
        float fromAction = Mathf.Clamp01(potentiometerAction.action.ReadValue<float>());
        return fromAction > 0f ? "InputSystem" : (potInput != null ? "ArduinoPot(fallback)" : "InputSystem=0");
    }

    bool ShouldUseMouseWheelFallback()
    {
        return potInput == null || !potInput.IsConnected || !potInput.HasReceivedRecently(arduinoNoDataTimeout);
    }

    void OnGUI()
    {
        if (!showRotationDebug || !_startOk || _targetTransform == null) return;

        bool usingWheel = enableMouseWheelFallback && ShouldUseMouseWheelFallback();
        float t;
        float angleY;

        if (usingWheel)
        {
            angleY = _currentAngleY;
            float low = Mathf.Min(minAngleY, maxAngleY);
            float high = Mathf.Max(minAngleY, maxAngleY);
            t = high > low ? Mathf.InverseLerp(low, high, angleY) : 0f;
        }
        else
        {
            t = GetNormalizedValue();
            if (invertDirection) t = 1f - t;
            angleY = Mathf.Lerp(minAngleY, maxAngleY, t);
        }

        string targetName = _targetTransform != null ? _targetTransform.name : "?";
        GUI.Label(new Rect(10, 120, 320, 60), $"[PotYRotation]\nSource: {GetSourceLabel()}  Normalized: {t:F3}  Target angle Y: {angleY:F1}°\nRotating: {targetName}");
    }
}
