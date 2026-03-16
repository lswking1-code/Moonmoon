using System.Collections;
using System.Collections.Generic;
using UnityEngine;


// Handles messages from Arduino
public class PingPongTest : MonoBehaviour
{
    public ArduinoConnector Arduino;

    [Button(Editor = false)]
    public void Ping()
    {
        Arduino.Send("PING");
    }

    public void MessageHandler (string message)
    {
        Debug.Log($"message: '{message}'");
        if (message == "PING")
            Arduino.Send("PONG");
    }
}