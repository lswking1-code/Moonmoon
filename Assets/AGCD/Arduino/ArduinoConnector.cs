using UnityEngine;
using System.IO.Ports;
using System.Collections.Concurrent;
using System;
using System.Threading.Tasks;
using UnityEngine.Events;

/* Arduino Connector
 *  
 *  This scripts connects to Arduino using the serial port,
 *  and shows how to send and receive simple commands.
 */
public class ArduinoConnector : MonoBehaviour
{
    [Header("Serial Port")]
    public string Port = "COM3";
    public int BaudRate = 9600;
    // 300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 28800, 31250, 38400, 57600, 115200
    public int ReadTimeout  = 1000;
    public int WriteTimeout = 1000;

    private SerialPort Serial;


    private bool isRunning = true;
    private ConcurrentQueue<string> inboxQueue  = new ConcurrentQueue<string>();
    private ConcurrentQueue<string> outboxQueue = new ConcurrentQueue<string>();


    // Invoked when a message is received
    public UnityEvent<string> MessageCallback;

    /*
    void Awake()
    {
        // Default message handler
        MessageCallback.AddListener(MessageHandler);
    }
    */

    [Button(Editor = false)]
    async public void OpenSerialConnection()
    {
        Serial = new SerialPort(Port, BaudRate);
        Serial.ReadTimeout  = ReadTimeout;
        Serial.WriteTimeout = WriteTimeout;

        Serial.Open();

        await Task.Run(ReadAndWriteSerialLoop);
    }

    void ReadAndWriteSerialLoop()
    {
        while (isRunning && Serial.IsOpen)
        {
            try
            {
                // Read data (if available)
                string message = Serial.ReadLine().Trim();
                inboxQueue.Enqueue(message);
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
                Serial.WriteLine(command);
            }
        }
    }

    void Update()
    {
        if (inboxQueue.TryDequeue(out string message))
        {
            //Debug.Log($"From ArduinoConnector: '{message}'");
            // Default message handler
            MessageHandler(message); 

            // UnityEvents
            MessageCallback.Invoke(message);
        }
    }

    public void Send (string message)
    {
        outboxQueue.Enqueue(message);
    }



    // Override this method in case you want to extend this class
    public virtual void MessageHandler(string message) { }


    [Button(Editor = false)]
    public bool CloseSerialConnection()
    {
        isRunning = false;
        if (Serial != null && Serial.IsOpen)
        {
            Serial.Close();
            return true;
        }

        return false;
    }

    void OnApplicationQuit() => CloseSerialConnection();
}