using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Reads potentiometer values from Arduino serial port for use in Unity.
/// Arduino must send one value per line via Serial.println(value) (0-1023 or 0-4095 supported).
/// Requires NuGet package System.IO.Ports, or a Unity project with Api Compatibility Level set to .NET Framework.
/// </summary>
public class ArduinoPotInput : MonoBehaviour
{
    [Header("Serial Port Settings")]
    [Tooltip("COM port name, e.g. COM3, COM4. Check Device Manager or Arduino IDE")]
    public string portName = "COM3";

    [Tooltip("Baud rate; must match Arduino Serial.begin()")]
    public int baudRate = 9600;

    [Header("Value Range")]
    [Tooltip("Whether to normalize to 0~1 (for direct use with Slider, volume, etc.)")]
    public bool normalizeTo01 = true;

    [Tooltip("Max integer value sent by Arduino, e.g. 1023 (10-bit ADC default), 4095 (12-bit ADC)")]
    public int maxRawValue = 1023;

    [Tooltip("When checked: if potentiometer at 'start' outputs max value, treat as 0; at 'end' outputs min value, treat as 1")]
    public bool invertPotentiometer = false;

    [Header("Debug")]
    [Tooltip("When checked, show normalized and raw values in top-left of Game view")]
    public bool showDebugValue = true;

    // Raw value 0-4095 (if Arduino sends 0-1023 it is normalized by 1023)
    public int RawValue { get; private set; }

    // Normalized 0~1 (when normalizeTo01 is true)
    public float NormalizedValue { get; private set; }

    // Raw joystick values (correspond to joyX, joyY in Arduino PotForUnity.ino)
    public int RawJoyX { get; private set; }
    public int RawJoyY { get; private set; }

    // Joystick normalized -1~1 (center is 0, for movement direction control)
    public float JoyX { get; private set; }
    public float JoyY { get; private set; }

    object _serial;
    bool _connected;
    string _readBuffer = "";
    string _lastDebugError = "";
    float _lastReceivedTime = -1f;

    static Type _serialPortType;
    static PropertyInfo _isOpenProp;
    static MethodInfo _readExistingMethod, _openMethod, _closeMethod, _disposeMethod;
    static bool _reflectionResolved;
    static readonly char[] NewlineChars = { '\r', '\n' };

    static void ResolveSerialPortType()
    {
        if (_reflectionResolved) return;
        _reflectionResolved = true;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                _serialPortType = asm.GetType("System.IO.Ports.SerialPort");
                if (_serialPortType == null) continue;
                _isOpenProp = _serialPortType.GetProperty("IsOpen");
                _readExistingMethod = _serialPortType.GetMethod("ReadExisting", Type.EmptyTypes);
                _openMethod = _serialPortType.GetMethod("Open", Type.EmptyTypes);
                _closeMethod = _serialPortType.GetMethod("Close", Type.EmptyTypes);
                _disposeMethod = _serialPortType.GetMethod("Dispose", Type.EmptyTypes);
                if (_readExistingMethod != null) break;
            }
            catch { _serialPortType = null; }
        }
    }

    void Start()
    {
        OpenPort();
    }

    void OnDestroy()
    {
        ClosePort();
    }

    void Update()
    {
        if (!_connected || _serial == null || !GetIsOpen()) return;

        try
        {
            string read = (string)_readExistingMethod.Invoke(_serial, null);
            if (!string.IsNullOrEmpty(read)) _readBuffer += read;

            if (_readBuffer.Length > 0)
            {
                int newline = _readBuffer.IndexOfAny(NewlineChars);
                while (newline >= 0)
                {
                    TryApplyValue(_readBuffer.Substring(0, newline).Trim());
                    _readBuffer = _readBuffer.Substring(newline + 1).TrimStart('\r', '\n');
                    newline = _readBuffer.IndexOfAny(NewlineChars);
                }
                if (_readBuffer.Length > 0 && _readBuffer.Length <= 10)
                {
                    string trimmed = _readBuffer.Trim();
                    if (trimmed.Length > 0 && IsDigitsOnly(trimmed) && TryApplyValue(trimmed))
                        _readBuffer = "";
                }
                if (_readBuffer.Length > 64) _readBuffer = _readBuffer.Substring(_readBuffer.Length - 32);
                _lastDebugError = "";
            }
        }
        catch (Exception e)
        {
            _lastDebugError = e.Message;
            Debug.LogWarning("ArduinoPot read exception: " + e.Message);
        }
    }

    static bool IsDigitsOnly(string s)
    {
        foreach (char c in s) { if (c != '-' && !char.IsDigit(c)) return false; }
        return true;
    }

    bool TryApplyValue(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        string trimmed = line.Trim();
        int max = Mathf.Max(1, maxRawValue);
        float mid = max * 0.5f;

        // Supports "pot,joyX,joyY" three-column format (matches PotForUnity.ino)
        string[] parts = trimmed.Split(',');
        if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int potVal))
        {
            RawValue = Mathf.Clamp(potVal, 0, max);
            float norm = RawValue / (float)max;
            NormalizedValue = invertPotentiometer ? 1f - norm : norm;
        }
        if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int joyXVal))
        {
            RawJoyX = Mathf.Clamp(joyXVal, 0, max);
            JoyX = Mathf.Clamp((RawJoyX - mid) / mid, -1f, 1f);
        }
        if (parts.Length >= 3 && int.TryParse(parts[2].Trim(), out int joyYVal))
        {
            RawJoyY = Mathf.Clamp(joyYVal, 0, max);
            JoyY = Mathf.Clamp((RawJoyY - mid) / mid, -1f, 1f);
        }

        _lastReceivedTime = Time.time;
        return true;
    }

    bool GetIsOpen()
    {
        if (_serial == null || _isOpenProp == null) return false;
        try { return (bool)_isOpenProp.GetValue(_serial); } catch { return false; }
    }

    void OpenPort()
    {
        ResolveSerialPortType();
        if (_serialPortType == null)
        {
            Debug.LogError("[ArduinoPot] System.IO.Ports.SerialPort not found. Install NuGet package System.IO.Ports, or set Api Compatibility Level to .NET Framework in Player settings.");
            _connected = false;
            return;
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Debug.LogWarning("[ArduinoPot] System.IO.Ports is Windows-only. Serial port skipped on current platform; value remains 0.");
            _connected = false;
            return;
        }

        try
        {
            _serial = Activator.CreateInstance(_serialPortType, portName, baudRate);
            if (_serial == null) throw new Exception("CreateInstance returned null");
            _serialPortType.GetProperty("ReadTimeout").SetValue(_serial, 500);
            _serialPortType.GetProperty("WriteTimeout").SetValue(_serial, 500);
            _openMethod.Invoke(_serial, null);
            var dtrProp = _serialPortType.GetProperty("DtrEnable");
            if (dtrProp != null) dtrProp.SetValue(_serial, true);
            _connected = true;
            Debug.Log($"[ArduinoPot] Connected {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[ArduinoPot] Failed to open {portName}: " + e.Message);
            _connected = false;
        }
    }

    void ClosePort()
    {
        _connected = false;
        if (_serial != null && GetIsOpen())
        {
            try { _closeMethod?.Invoke(_serial, null); _disposeMethod?.Invoke(_serial, null); } catch { }
        }
        _serial = null;
    }

    /// <summary> Returns 0~1 value for UI, volume, blending, etc. </summary>
    public float GetNormalized()
    {
        if (normalizeTo01) return NormalizedValue;
        float t = RawValue / (float)Mathf.Max(1, maxRawValue);
        return invertPotentiometer ? 1f - t : t;
    }

    /// <summary> Returns the raw integer value. </summary>
    public int GetRaw() => RawValue;

    void OnGUI()
    {
        if (!showDebugValue) return;
        int max = Mathf.Max(1, maxRawValue);
        string dataStatus = _lastReceivedTime < 0 ? "Waiting for data..." :
            (Time.time - _lastReceivedTime < 0.5f ? "Receiving data" : "No new data");
        string text = $"Status: {(_connected ? "Connected " + portName : "Disconnected")}\n{dataStatus}\nPot normalized: {GetNormalized():F3}\nRaw: {RawValue} / {max}\nJoy X: {JoyX:F2} Y: {JoyY:F2}\nBuffer: {_readBuffer.Length} chars";
        if (!string.IsNullOrEmpty(_lastDebugError)) text += $"\nError: {_lastDebugError}";
        var style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 8, 8) };
        GUI.Box(new Rect(10, 10, 220, string.IsNullOrEmpty(_lastDebugError) ? 100 : 120), text, style);
    }
}
