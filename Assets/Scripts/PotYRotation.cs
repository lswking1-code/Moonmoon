using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rotates around local Y from an Arduino pot (<see cref="ArduinoPotInput"/>) or an Input System action.
/// Optional mouse wheel when Arduino is absent or idle.
/// </summary>
[RequireComponent(typeof(Transform))]
public class PotYRotation : MonoBehaviour
{
    [Header("Target")]
    public GameObject rotationTarget;

    [Header("Source")]
    public bool useInputSystem;
    public InputActionReference potentiometerAction;
    public ArduinoPotInput potInput;

    [Header("Y range (degrees)")]
    public float minAngleY = 0f;
    public float maxAngleY = 360f;
    public bool invertDirection = false;

    [Header("Dead zone")]
    public float minAngleStep = 0f;

    [Header("Smoothing")]
    [Range(0f, 20f)]
    public float smoothSpeed = 0f;

    [Header("Mouse wheel fallback")]
    public bool enableMouseWheelFallback = true;
    public float wheelAngleStep = 30f;
    public float arduinoNoDataTimeout = 0.5f;

    [Header("Debug")]
    public bool showRotationDebug = true;

    float _currentAngleY;
    Transform _targetTransform;
    bool _startOk;
    float _initialLocalX, _initialLocalZ;

    void AngleBounds(out float low, out float high)
    {
        low = Mathf.Min(minAngleY, maxAngleY);
        high = Mathf.Max(minAngleY, maxAngleY);
    }

    void Start()
    {
        _targetTransform = rotationTarget != null ? rotationTarget.transform : transform;
        if (_targetTransform == null)
        {
            Debug.LogError("[PotYRotation] Invalid rotation target.");
            return;
        }

        var euler = _targetTransform.localEulerAngles;
        _initialLocalX = euler.x;
        _initialLocalZ = euler.z;

        potInput ??= GetComponentInChildren<ArduinoPotInput>() ?? FindObjectOfType<ArduinoPotInput>();

        _startOk = (useInputSystem && potentiometerAction != null) || potInput != null;
        if (!_startOk)
        {
            Debug.LogError("[PotYRotation] No pot source: assign ArduinoPotInput or Input System action.");
            return;
        }

        if (useInputSystem && potentiometerAction != null)
            potentiometerAction.action.Enable();

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

    bool UseMouseWheelNow() =>
        enableMouseWheelFallback && (potInput == null || !potInput.IsConnected || !potInput.HasReceivedRecently(arduinoNoDataTimeout));// Cursor AI generated

    void Update()
    {
        if (!_startOk || _targetTransform == null) return;

        if (UseMouseWheelNow())
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.0001f)
            {
                AngleBounds(out float low, out float high);
                _currentAngleY = Mathf.Clamp(_currentAngleY + scroll * wheelAngleStep, low, high);
                ApplyRotation(_currentAngleY);
            }
            return;
        }// Cursor AI generated

        float t = GetNormalizedValue();
        if (invertDirection) t = 1f - t;
        float targetAngleY = Mathf.Lerp(minAngleY, maxAngleY, t);

        if (minAngleStep > 0f && Mathf.Abs(Mathf.DeltaAngle(_currentAngleY, targetAngleY)) < minAngleStep)
            return;

        _currentAngleY = smoothSpeed > 0f
            ? Mathf.LerpAngle(_currentAngleY, targetAngleY, smoothSpeed * Time.deltaTime)
            : targetAngleY;
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
        AngleBounds(out float low, out float high);
        float y = Mathf.Clamp(angleY, low, high);
        _targetTransform.localEulerAngles = new Vector3(_initialLocalX, y, _initialLocalZ);
    }

    string SourceLabel()
    {
        if (UseMouseWheelNow()) return "MouseWheel";
        if (!useInputSystem || potentiometerAction == null)
            return potInput != null ? "ArduinoPot" : "None";
        float fromAction = Mathf.Clamp01(potentiometerAction.action.ReadValue<float>());
        return fromAction > 0f ? "InputSystem" : (potInput != null ? "ArduinoPot(fallback)" : "InputSystem=0");
    }

    void OnGUI()
    {
        if (!showRotationDebug || !_startOk || _targetTransform == null) return;

        float t;
        float angleY;
        if (UseMouseWheelNow())
        {
            angleY = _currentAngleY;
            AngleBounds(out float low, out float high);
            t = high > low ? Mathf.InverseLerp(low, high, angleY) : 0f;
        }
        else
        {
            t = GetNormalizedValue();
            if (invertDirection) t = 1f - t;
            angleY = Mathf.Lerp(minAngleY, maxAngleY, t);
        }

        GUI.Label(new Rect(10, 120, 320, 60),
            $"[PotYRotation]\nSource: {SourceLabel()}  t: {t:F3}  Y: {angleY:F1}°\nTarget: {_targetTransform.name}");
    }
}
