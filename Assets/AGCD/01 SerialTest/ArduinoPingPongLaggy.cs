using UnityEngine;
using System.IO.Ports;

/* Ping Pong
 *  
 *  This scripts connects to Arduino using the serial port,
 *  and shows how to implement a simple PING/PONG protocol.
 *  
 *  This script freezes is a toy example, as it will cause lag!
 */
public class ArduinoPingPongLag : MonoBehaviour
{
    SerialPort Serial;

    void Start()
    {
        Serial = new SerialPort("COM3", 9600); // Change COM port as needed
        Serial.Open();
        Serial.ReadTimeout = 1000;  // Timeout for reading

        //InvokeRepeating("SendPing", 2.0f, 2.0f); // Send PING every 2 seconds
    }

    void Update()
    {
        if (Serial.IsOpen && Serial.BytesToRead > 0)
        {
            string message = Serial.ReadLine().Trim();
            Debug.Log("Received: " + message);

            if (message == "PING")
                Serial.WriteLine("PONG");
            //else if (message == "PONG")
            //    Debug.Log("PONG received!");
                //Serial.WriteLine("PING");
        }
    }

    void SendPing()
    {
        if (Serial.IsOpen)
            Serial.WriteLine("PING");
    }

    void OnApplicationQuit()
    {
        if (Serial != null && Serial.IsOpen)
            Serial.Close();
    }
}