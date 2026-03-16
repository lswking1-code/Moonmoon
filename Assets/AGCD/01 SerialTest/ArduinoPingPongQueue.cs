using UnityEngine;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System;

/* Ping Pong
 *  
 *  This scripts connects to Arduino using the serial port,
 *  and shows how to implement a simple PING/PONG protocol
 */ 
public class ArduinoPingPongQueue : MonoBehaviour
{
    public string Port = "COM3";
    public int BaudRate = 9600;
    
    private SerialPort Serial;
    private bool isRunning = true;
    private ConcurrentQueue<string> inboxQueue = new ConcurrentQueue<string>(); // Thread-safe queue
    private ConcurrentQueue<string> outboxQueue = new ConcurrentQueue<string>();  // Thread-safe queue for sending data


    [Button(Editor=false)]
    async void SerialStart ()
    {
        Serial = new SerialPort(Port, BaudRate)
        {
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };

        Serial.Open();
        await Task.Run(ReadAndWriteSerialLoop); // Single background task
    }




    void ReadAndWriteSerialLoop()
    {
        while (isRunning && Serial.IsOpen)
        {
            try
            {
                // Read data (if available)
                string message = Serial.ReadLine().Trim();
                inboxQueue.Enqueue(message); // Store safely for the main thread to read
            }
            catch (TimeoutException)
            {
                // Ignore read timeouts
            }
            catch (Exception e)
            {
                Debug.Log(e);
            }


            // Write pending messages
            while (outboxQueue.TryDequeue(out string command))
            {
                Serial.WriteLine(command); // Write safely in the background
            }
        }
    }

    void Update()
    {
        if (inboxQueue.TryDequeue(out string message)) // Safely get received message
        {
            Debug.Log("Received: " + message);

            if (message == "PING")
            {
                SendMessageToArduino("PONG");
            }
            //else if (message == "PONG")
            //{
            //    SendMessageToArduino("PING");
            //}
        }
    }

    public void SendMessageToArduino(string message)
    {
        outboxQueue.Enqueue(message); // Store safely to be written in the background
        Debug.Log("Queued for sending: " + message);
    }

    [Button(Editor = false)]
    bool SendPingToArduino()
    {
        if (Serial == null || ! Serial.IsOpen)
            return false;

        SendMessageToArduino("PING");
        return true;
    }

    [Button(Editor = false)]
    void SerialClose()
    {
        isRunning = false;
        if (Serial != null && Serial.IsOpen)
        {
            Serial.Close();
        }
    }

    void OnApplicationQuit()
    {
        SerialClose();
    }
}