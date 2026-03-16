using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

/// <summary>
/// Custom potentiometer device in Input System: single 0~1 axis "value".
/// Use with ArduinoPotInputSystemBridge to add and update this device when Arduino is connected.
/// </summary>
public struct PotentiometerState : IInputStateTypeInfo
{
    public FourCC format => new FourCC('P', 'O', 'T', '1');

    [InputControl(name = "value", layout = "Axis", format = "FLT", displayName = "Potentiometer")]
    public float value;
}

#if UNITY_EDITOR
[UnityEditor.InitializeOnLoad]
#endif
[InputControlLayout(stateType = typeof(PotentiometerState))]
public class PotentiometerDevice : InputDevice
{
    public AxisControl value { get; protected set; }

    static PotentiometerDevice()
    {
        Initialize();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        InputSystem.RegisterLayout<PotentiometerDevice>(
            matches: new InputDeviceMatcher().WithInterface("ArduinoPot"));
    }

    protected override void FinishSetup()
    {
        base.FinishSetup();
        value = GetChildControl<AxisControl>("value");
    }

    public static PotentiometerDevice current { get; private set; }

    public override void MakeCurrent()
    {
        base.MakeCurrent();
        current = this;
    }

    protected override void OnRemoved()
    {
        base.OnRemoved();
        if (current == this)
            current = null;
    }
}
