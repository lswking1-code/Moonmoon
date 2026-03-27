using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

/// <summary>
/// Writes ArduinoPotInput potentiometer value into Unity Input System as PotentiometerDevice.
/// Attach to an object with ArduinoPotInput (or assign Pot Input) to bind &lt;Potentiometer&gt;/value in Input Actions.
/// </summary>
public class ArduinoPotInputSystemBridge : MonoBehaviour
{
    [Header("Potentiometer Source")]
    [Tooltip("Leave empty to auto-find ArduinoPotInput on self or children")]
    public ArduinoPotInput potInput;

    PotentiometerDevice _device;

    void Start()
    {
        potInput = potInput ?? GetComponentInChildren<ArduinoPotInput>();
        if (potInput == null) { Debug.LogError("[ArduinoPotInputSystemBridge] ArduinoPotInput not found."); return; }

        _device = (PotentiometerDevice)InputSystem.AddDevice(new InputDeviceDescription { interfaceName = "ArduinoPot", product = "Potentiometer" });
        Debug.Log("[ArduinoPotInputSystemBridge] Potentiometer device registered with Input System.");
    }// Cursor AI generated

    void Update()
    {
        if (_device == null || potInput == null) return;
        InputSystem.QueueStateEvent(_device, new PotentiometerState { value = Mathf.Clamp01(potInput.GetNormalized()) });
    }// Cursor AI generated

    void OnDisable()
    {
        RemoveDevice();
    }

    void OnDestroy()
    {
        RemoveDevice();
    }

    void RemoveDevice()
    {
        if (_device == null) return;
        try { if (_device.added) InputSystem.RemoveDevice(_device); }
        finally { _device = null; }
    }// Cursor AI generated
}
