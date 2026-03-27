using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Reads Arduino serial lines: one CSV row per line, e.g. pot,joyX,joyY (matches PotForUnity.ino).
/// Needs System.IO.Ports (NuGet or .NET Framework compatibility).
/// </summary>
public class ArduinoPotInput : MonoBehaviour
{
    [Header("Serial")]
    [Tooltip("COM port, e.g. COM3")]
    public string portName = "COM3";
    [Tooltip("Must match Arduino Serial.begin()")]
    public int baudRate = 9600;

    [Header("Range")]
    [Tooltip("Map raw ADC to 0~1 for sliders, etc.")]
    public bool normalizeTo01 = true;
    [Tooltip("ADC max, e.g. 1023 or 4095")]
    public int maxRawValue = 1023;
    [Tooltip("If high end of pot is physically min, flip normalized output")]
    public bool invertPotentiometer = false;

    [Header("Debug")]
    [Tooltip("HUD in Game view")]
    public bool showDebugValue = true;

    public int RawValue { get; private set; }
    public float NormalizedValue { get; private set; }
    public int RawJoyX { get; private set; }
    public int RawJoyY { get; private set; }
    public float JoyX { get; private set; }
    public float JoyY { get; private set; }

    public bool IsConnected => _connected;

    /// <summary> True if any parsed serial update arrived within the given time window (fallback input when unplugged). </summary>
    /// <param name="seconds">Time window in seconds.</param>
    public bool HasReceivedRecently(float seconds = 0.5f)
    {
        if (_lastReceivedTime < 0f) return false;
        return (Time.time - _lastReceivedTime) <= Mathf.Max(0f, seconds);
    }// Cursor AI generated

    public float GetNormalized()
    {
        if (normalizeTo01) return NormalizedValue;
        float t = RawValue / (float)Mathf.Max(1, maxRawValue);
        return invertPotentiometer ? 1f - t : t;
    }// Cursor AI generated

    public int GetRaw() => RawValue;
    // Cursor AI generated
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

    void Start() => OpenPort();
    void OnDestroy() => ClosePort();

    void Update()
    {
        if (!_connected || _serial == null || !GetIsOpen()) return;

        try
        {
            string read = (string)_readExistingMethod.Invoke(_serial, null);
            if (!string.IsNullOrEmpty(read)) _readBuffer += read;

            if (_readBuffer.Length == 0) return;

            int nl;
            while ((nl = _readBuffer.IndexOfAny(NewlineChars)) >= 0)
            {
                TryApplyLine(_readBuffer.Substring(0, nl).Trim());
                _readBuffer = _readBuffer.Substring(nl + 1).TrimStart('\r', '\n');
            }

            if (_readBuffer.Length > 0 && _readBuffer.Length <= 10)
            {
                string t = _readBuffer.Trim();
                if (t.Length > 0 && IsDigitsOnly(t) && TryApplyLine(t)) _readBuffer = "";
            }

            if (_readBuffer.Length > 64) _readBuffer = _readBuffer.Substring(_readBuffer.Length - 32);
            _lastDebugError = "";
        }
        catch (Exception e)
        {
            _lastDebugError = e.Message;
            Debug.LogWarning("ArduinoPot read: " + e.Message);
        }
    }// Cursor AI generated

    static bool IsDigitsOnly(string s)
    {
        foreach (char c in s) { if (c != '-' && !char.IsDigit(c)) return false; }
        return true;
    }// Cursor AI generated

    bool TryApplyLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;

        int max = Mathf.Max(1, maxRawValue);
        float mid = max * 0.5f;
        string[] p = line.Trim().Split(',');
        bool parsed = false;

        if (p.Length >= 1 && int.TryParse(p[0].Trim(), out int pot))
        {
            parsed = true;
            RawValue = Mathf.Clamp(pot, 0, max);
            float n = RawValue / (float)max;
            NormalizedValue = invertPotentiometer ? 1f - n : n;
        }
        if (p.Length >= 2 && int.TryParse(p[1].Trim(), out int jx))
        {
            parsed = true;
            RawJoyX = Mathf.Clamp(jx, 0, max);
            JoyX = Mathf.Clamp((RawJoyX - mid) / mid, -1f, 1f);
        }
        if (p.Length >= 3 && int.TryParse(p[2].Trim(), out int jy))
        {
            parsed = true;
            RawJoyY = Mathf.Clamp(jy, 0, max);
            JoyY = Mathf.Clamp((RawJoyY - mid) / mid, -1f, 1f);
        }

        if (parsed) _lastReceivedTime = Time.time;
        return true;
    }

    bool GetIsOpen()
    {
        if (_serial == null || _isOpenProp == null) return false;
        try { return (bool)_isOpenProp.GetValue(_serial); } catch { return false; }
    }// Cursor AI generated

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
    }// Cursor AI generated

    void OpenPort()
    {
        ResolveSerialPortType();
        if (_serialPortType == null)
        {
            Debug.LogError("[ArduinoPot] System.IO.Ports.SerialPort not found. Add System.IO.Ports or use .NET Framework compatibility.");
            _connected = false;
            return;
        }
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Debug.LogWarning("[ArduinoPot] Serial is skipped on non-Windows; values stay 0.");
            _connected = false;
            return;
        }

        try
        {
            _serial = Activator.CreateInstance(_serialPortType, portName, baudRate);
            if (_serial == null) throw new InvalidOperationException("CreateInstance returned null.");
            _serialPortType.GetProperty("ReadTimeout").SetValue(_serial, 500);
            _serialPortType.GetProperty("WriteTimeout").SetValue(_serial, 500);
            _openMethod.Invoke(_serial, null);
            var dtr = _serialPortType.GetProperty("DtrEnable");
            if (dtr != null) dtr.SetValue(_serial, true);
            _connected = true;
            Debug.Log($"[ArduinoPot] {portName} @ {baudRate}");
        }
        catch (Exception e)
        {
            _serial = null;
            _connected = false;
            Debug.LogWarning($"[ArduinoPot] Could not open {portName}. Use keyboard / mouse fallback if applicable.\n{e.Message}");
        }
    }// Cursor AI generated

    void ClosePort()
    {
        _connected = false;
        if (_serial != null && GetIsOpen())
            try { _closeMethod?.Invoke(_serial, null); _disposeMethod?.Invoke(_serial, null); } catch { }
        _serial = null;
    }// Cursor AI generated

    void OnGUI()
    {
        if (!showDebugValue) return;

        int max = Mathf.Max(1, maxRawValue);
        string data = _lastReceivedTime < 0 ? "Waiting…" :
            (Time.time - _lastReceivedTime < 0.5f ? "Live" : "Idle");
        string t = $"Status: {(_connected ? portName : "Disconnected")}\n{data}\nPot: {GetNormalized():F3}\nRaw: {RawValue}/{max}\nJoy: {JoyX:F2}, {JoyY:F2}\nBuf: {_readBuffer.Length}";
        if (!string.IsNullOrEmpty(_lastDebugError)) t += $"\nErr: {_lastDebugError}";

        var style = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.UpperLeft, padding = new RectOffset(8, 8, 8, 8) };
        GUI.Box(new Rect(10, 10, 220, string.IsNullOrEmpty(_lastDebugError) ? 100 : 120), t, style);
    }// Cursor AI generated
}
