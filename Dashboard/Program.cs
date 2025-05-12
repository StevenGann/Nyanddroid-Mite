using System;
using System.Collections.Generic;
using System.Text.Json;
using Common;
using System.Numerics;

namespace Dashboard
{
    class Program
    {
        static void Main(string[] args)
        {
            Logging.LogLevel = Logging.Level.Warning;
            // Example: Connect to Software node (adjust ports as needed)
            using var connector = new TcpConnector();
            connector.Connect(ListeningPort: 5555, TargetPort: 5556, TargetIp: "192.168.0.161");

            LidarVisualizer.Connector = connector;
            LidarVisualizer.Start();

            Console.WriteLine("Dashboard server ready, waiting for robot...");
            while (true)
            {
                (string message, byte[]? data) = connector.Receive();
                if (!string.IsNullOrEmpty(message))
                {
                    if (message.StartsWith("[LOGS]"))
                    {
                        string json = message.Substring("[LOGS]".Length);
                        try
                        {
                            var logs = JsonSerializer.Deserialize<List<LogEntry>>(json);
                            if (logs != null)
                            {
                                foreach (var entry in logs)
                                {
                                    Logging.Log(entry.Message, entry.Level);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to deserialize logs: {ex.Message}");
                            Logging.Log($"Failed to deserialize logs: {ex.Message}", Logging.Level.Warning);
                        }
                    }
                    else if (message.StartsWith("[POINTS]") && data != null)
                    {
                        // Decode byte[] to Vector2[] (Int16 X, Int16 Y pairs)
                        int count = data.Length / 4;
                        var points = new Vector2[count];
                        for (int i = 0; i < count; i++)
                        {
                            short x = BitConverter.ToInt16(data, i * 4);
                            short y = BitConverter.ToInt16(data, i * 4 + 2);
                            points[i] = new Vector2(x, y);
                        }
                        //Console.WriteLine($"Received {points.Length} LIDAR points");
                        LidarVisualizer.UpdatePoints(points);
                    }
                    else
                    {
                        Console.WriteLine($"Received: {message}");
                    }
                }
                System.Threading.Thread.Sleep(1);
            }
        }
    }
}

