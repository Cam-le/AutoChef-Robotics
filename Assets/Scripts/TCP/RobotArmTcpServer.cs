using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class RobotArmTcpServer : MonoBehaviour
{
    [Header("TCP Settings")]
    public int port = 5000;
    public bool startServerOnAwake = true;
    public bool displayDebugInfo = true;

    [Header("Connection Info")]
    [SerializeField] private string serverIP = "Not available";
    [SerializeField] private bool isListening = false;
    [SerializeField] private int connectedClients = 0;

    private TcpListener listener;
    private Thread serverThread;
    private bool isRunning = false;
    private object _lockObject = new object();

    // Queue for thread-safe logging
    private Queue<string> logQueue = new Queue<string>();
    // Queue for commands to be processed on the main thread
    private Queue<string> commandQueue = new Queue<string>();

    void Awake()
    {
        Application.runInBackground = true; // Allow Unity to run in background

        if (startServerOnAwake)
        {
            StartCoroutine(StartServerDelayed());
        }
    }

    IEnumerator StartServerDelayed()
    {
        // Wait for a frame to ensure Unity is fully initialized
        yield return null;
        StartServer();
    }

    void Update()
    {
        // Process any logs from the background thread
        lock (_lockObject)
        {
            while (logQueue.Count > 0)
            {
                string log = logQueue.Dequeue();
                if (displayDebugInfo)
                {
                    Debug.Log($"[RobotTcpServer] {log}");
                }
            }
        }

        // Process any commands from clients
        lock (_lockObject)
        {
            while (commandQueue.Count > 0)
            {
                string command = commandQueue.Dequeue();
                ProcessCommand(command);
            }
        }
    }

    public void StartServer()
    {
        if (isRunning) return;

        try
        {
            // Get all IP addresses of this machine
            IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());
            string ipInfo = "Available IPs:\n";
            foreach (IPAddress ip in localIPs)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4 only
                {
                    ipInfo += $"{ip}\n";
                    serverIP = ip.ToString(); // Use last found IP
                }
            }

            // Try to start on all interfaces
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            isRunning = true;
            isListening = true;

            // Log on the main thread
            lock (_lockObject)
            {
                logQueue.Enqueue($"Server started on port {port}");
                logQueue.Enqueue(ipInfo);
                logQueue.Enqueue($"Connect to this server using: {serverIP}:{port}");
            }

            // Start the server thread
            serverThread = new Thread(ListenForClients);
            serverThread.IsBackground = true;
            serverThread.Start();
        }
        catch (Exception e)
        {
            // Log on the main thread
            lock (_lockObject)
            {
                logQueue.Enqueue($"Error starting server: {e.Message}");
                isListening = false;
            }
        }
    }

    public void StopServer()
    {
        isRunning = false;
        isListening = false;

        if (listener != null)
        {
            listener.Stop();
            listener = null;
        }

        if (serverThread != null && serverThread.IsAlive)
        {
            serverThread.Join(500); // Wait for thread to finish with timeout
            serverThread = null;
        }

        lock (_lockObject)
        {
            logQueue.Enqueue("Server stopped");
        }
    }

    private void ListenForClients()
    {
        while (isRunning)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();

                lock (_lockObject)
                {
                    connectedClients++;
                    logQueue.Enqueue($"Client connected from {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
                    logQueue.Enqueue($"Active connections: {connectedClients}");
                }

                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
            catch (SocketException e)
            {
                if (isRunning) // Only log if we didn't intentionally stop
                {
                    lock (_lockObject)
                    {
                        logQueue.Enqueue($"Socket exception: {e.Message}");
                    }
                }
                break;
            }
            catch (Exception e)
            {
                lock (_lockObject)
                {
                    logQueue.Enqueue($"Error accepting client: {e.Message}");
                }
                // Small delay to prevent CPU spike in case of repeated errors
                Thread.Sleep(100);
            }
        }
    }

    private void HandleClient(TcpClient client)
    {
        try
        {
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                // Set read timeout to prevent hanging
                stream.ReadTimeout = 30000; // 30 seconds

                while (isRunning && client.Connected && (bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    // Add to logging queue
                    lock (_lockObject)
                    {
                        logQueue.Enqueue($"Received: {message}");
                    }

                    // Add to command processing queue
                    lock (_lockObject)
                    {
                        commandQueue.Enqueue(message);
                    }

                    // Send response
                    string response = "Command received and queued for processing";
                    byte[] responseData = Encoding.UTF8.GetBytes(response);
                    stream.Write(responseData, 0, responseData.Length);
                }
            }
        }
        catch (Exception e)
        {
            lock (_lockObject)
            {
                logQueue.Enqueue($"Error handling client: {e.Message}");
            }
        }
        finally
        {
            client.Close();
            lock (_lockObject)
            {
                connectedClients--;
                logQueue.Enqueue("Client disconnected");
                logQueue.Enqueue($"Active connections: {connectedClients}");
            }
        }
    }

    private void ProcessCommand(string command)
    {
        // TODO: Implement your robot arm command processing logic here
        Debug.Log($"Processing command: {command}");

        // Example: Parse command for robot arm
        // Format could be "MOVE:Joint1:45.0" to move joint 1 to 45 degrees
        try
        {
            string[] parts = command.Split(':');
            if (parts.Length >= 3)
            {
                string action = parts[0];
                string joint = parts[1];
                float value = float.Parse(parts[2]);

                if (action == "MOVE")
                {
                    // Call your robot arm control method here
                    Debug.Log($"Moving {joint} to {value} degrees");
                    // Example: MoveJoint(joint, value);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error processing command: {e.Message}");
        }
    }

    void OnGUI()
    {
        if (displayDebugInfo)
        {
            // Display connection info in top-left corner
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"Server IP: {serverIP}");
            GUILayout.Label($"Port: {port}");
            GUILayout.Label($"Status: {(isListening ? "Listening" : "Not Listening")}");
            GUILayout.Label($"Connected Clients: {connectedClients}");
            GUILayout.EndArea();
        }
    }

    void OnApplicationQuit()
    {
        StopServer();
    }

    void OnDisable()
    {
        StopServer();
    }
}