using UnityEngine;

/// <summary>
/// 使用 ArduinoConnector 的 MessageCallback 收到的电位器值，控制物体绕 Y 轴旋转。
/// 将本脚本的 OnArduinoMessage(string) 绑定到 ArduinoConnector.MessageCallback 即可。
/// </summary>
public class PotYRotationFromMessage : MonoBehaviour
{
    [Header("Rotation Target")]
    [Tooltip("要旋转的物体。不设置则默认使用本脚本所在物体。")]
    public GameObject rotationTarget;

    [Header("Y Axis Rotation Range (degrees)")]
    [Tooltip("电位器 = 0 时的 Y 轴角度")]
    public float minAngleY = 0f;

    [Tooltip("电位器 = 1 时的 Y 轴角度")]
    public float maxAngleY = 360f;

    [Header("Potentiometer Settings")]
    [Tooltip("Arduino 发送的最大原始值，比如 1023 或 4095")]
    public int maxRawValue = 1023;

    [Tooltip("勾选后电位器数值越大，角度越小（反向）")]
    public bool invertDirection = false;

    [Header("Optional: Smoothing")]
    [Tooltip("当大于 0 时，使用插值平滑旋转")]
    [Range(0f, 20f)]
    public float smoothSpeed = 0f;

    [Header("Debug")]
    [Tooltip("在屏幕上简单显示当前归一化值和角度")]
    public bool showRotationDebug = true;

    float _currentAngleY;
    float _normalized;          // 0~1
    Transform _targetTransform;
    float _initialLocalX, _initialLocalZ;

    void Start()
    {
        _targetTransform = rotationTarget != null ? rotationTarget.transform : transform;
        if (_targetTransform == null)
        {
            Debug.LogError("[PotYRotationFromMessage] Rotation target is invalid.");
            enabled = false;
            return;
        }

        var euler = _targetTransform.localEulerAngles;
        _initialLocalX = euler.x;
        _initialLocalZ = euler.z;

        _currentAngleY = minAngleY;
        ApplyRotation(minAngleY);
    }

    /// <summary>
    /// 供 ArduinoConnector.MessageCallback 调用的回调。
    /// 必须是 public，且有一个 string 参数。
    /// </summary>
    /// <param name="message">Arduino 通过串口发送的一整行字符串。</param>
    public void OnArduinoMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        message = message.Trim();

        // 如果 Arduino 发送的是 "pot,joyX,joyY" 这样的 CSV，可以在这里先拆分：
        // string[] parts = message.Split(',');
        // if (parts.Length > 0) message = parts[0].Trim();

        if (!int.TryParse(message, out int raw))
            return;

        int max = Mathf.Max(1, maxRawValue);
        raw = Mathf.Clamp(raw, 0, max);
        float t = raw / (float)max;   // 0~1

        if (invertDirection) t = 1f - t;

        _normalized = Mathf.Clamp01(t);
    }

    void Update()
    {
        if (_targetTransform == null) return;

        float targetAngleY = Mathf.Lerp(minAngleY, maxAngleY, _normalized);
        _currentAngleY = smoothSpeed > 0f
            ? Mathf.LerpAngle(_currentAngleY, targetAngleY, smoothSpeed * Time.deltaTime)
            : targetAngleY;

        ApplyRotation(_currentAngleY);
    }

    void ApplyRotation(float angleY)
    {
        if (_targetTransform == null) return;
        float y = Mathf.Clamp(angleY, Mathf.Min(minAngleY, maxAngleY), Mathf.Max(minAngleY, maxAngleY));
        _targetTransform.localEulerAngles = new Vector3(_initialLocalX, y, _initialLocalZ);
    }

    void OnGUI()
    {
        if (!showRotationDebug || _targetTransform == null) return;

        float angleY = Mathf.Lerp(minAngleY, maxAngleY, _normalized);
        string targetName = _targetTransform != null ? _targetTransform.name : "?";
        GUI.Label(
            new Rect(10, 190, 320, 60),
            $"[PotYRotationFromMessage]\nNormalized: {_normalized:F3}  Angle Y: {angleY:F1}°\nRotating: {targetName}"
        );
    }
}

