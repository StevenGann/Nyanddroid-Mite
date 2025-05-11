using System;
using System.Threading;
using System.Numerics;
using Common;
using System.Text.Json;
using System.Linq;

namespace NyandroidMite
{
    /// <summary>
    /// Provides a visualization interface for the LD06 LIDAR sensor data.
    /// </summary>
    class Program
    {
        // Visualization settings
        private const int CANVAS_WIDTH = 80;
        private const int CANVAS_HEIGHT = 35;  // Reduced height to ensure room for legend
        private const float SCALE = 0.005f; // Scale factor to convert mm to canvas units
        private const int REFRESH_RATE = 25; // Milliseconds between updates

        private static TcpConnector connector = new();

        /// <summary>
        /// The entry point of the application.
        /// </summary>
        static void Main(string[] args)
        {
            // Parse command line arguments
            bool enableVisualization = false;
            string portName = Environment.OSVersion.Platform == PlatformID.Unix ? "/dev/ttyUSB0" : "COM6";

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--visualize":
                    case "-v":
                        enableVisualization = true;
                        break;
                    case "--port":
                    case "-p":
                        if (i + 1 < args.Length)
                            portName = args[++i];
                        break;
                }
            }

            // Configure logging based on visualization mode
            if (enableVisualization)
            {
                Logging.ConsoleOutput = false;
                Console.WriteLine("Nyandroid Mite LIDAR Visualizer");
                Console.WriteLine("==============================");
            }
            else
            {
                Console.WriteLine("Nyandroid Mite LIDAR Service");
                Console.WriteLine("===========================");
            }

            Logging.LogLevel = Logging.Level.Debug;
            connector.Connect(5556, 5555, "192.168.0.166");
            Console.WriteLine($"Using port: {portName}");
            Console.WriteLine("Press Ctrl+C to exit");
            Console.WriteLine();

            // Create and configure LIDAR
            using var lidar = new LidarLD06(portName);

            try
            {
                // Center the LIDAR in the visualization
                lidar.ConfigureSensor(Vector2.Zero, 0);

                Console.WriteLine("Connecting to LIDAR...");
                lidar.Connect();
                Console.WriteLine("Connected successfully!");

                if (enableVisualization)
                {
                    RunVisualization(lidar);
                }
                else
                {
                    RunDataCollection(lidar);
                }
            }
            catch (Exception ex)
            {
                Logging.Log($"Error: {ex.Message}", Logging.Level.Error);
            }
        }

        /// <summary>
        /// Runs the LIDAR in visualization mode with console output
        /// </summary>
        private static void RunVisualization(LidarLD06 lidar)
        {
            // Configure console
            try
            {
                Console.BufferHeight = Math.Max(Console.BufferHeight, CANVAS_HEIGHT + 8);
                Console.WindowHeight = Math.Min(50, Console.BufferHeight);
            }
            catch
            {
                // Some console environments don't support buffer size modification
            }

            // Setup visualization
            Console.CursorVisible = false;
            char[,] canvas = new char[CANVAS_HEIGHT, CANVAS_WIDTH];

            try
            {
                // Main visualization loop
                while (true)
                {
                    var perfToken = Performance.Start("Main visualization loop");
                    try
                    {
                        // Clear canvas
                        ClearCanvas(canvas);

                        // Get latest scan data
                        Vector2[] points = lidar.QuerySensor();

                        // Plot points on canvas
                        foreach (Vector2 point in points)
                        {
                            // Scale and transform point to canvas coordinates
                            int x = (int)(point.X * SCALE + CANVAS_WIDTH / 2);
                            int y = (int)(point.Y * SCALE + CANVAS_HEIGHT / 2);

                            // Check if point is within canvas bounds
                            if (x >= 0 && x < CANVAS_WIDTH && y >= 0 && y < CANVAS_HEIGHT)
                            {
                                canvas[y, x] = '#';
                            }
                        }

                        // Draw canvas
                        DrawCanvas(canvas);

                        // Draw legend and diagnostics
                        try
                        {
                            if (Console.BufferHeight >= CANVAS_HEIGHT + 6)
                            {
                                Console.SetCursorPosition(0, CANVAS_HEIGHT);
                                Console.WriteLine($"Points: {points.Length}    ");
                                Console.WriteLine("# = Detected obstacle");
                                Console.WriteLine("+ = LIDAR position");
                                Console.WriteLine($"Scale: 1 unit = {1 / SCALE:F1}mm");
                                Console.WriteLine($"Press Ctrl+C to exit");
                            }
                        }
                        catch
                        {
                            // If we can't write the legend, just skip it
                        }

                        Thread.Sleep(REFRESH_RATE);
                    }
                    catch (Exception ex)
                    {
                        // Log visualization errors but continue running
                        Console.SetCursorPosition(0, 0);
                        Logging.Log($"Visualization error: {ex.Message}", Logging.Level.Error);
                        Thread.Sleep(1000);
                    }
                    Performance.Stop(perfToken);
                    SendLog();
                }
            }
            finally
            {
                Console.CursorVisible = true;
            }
        }

        /// <summary>
        /// Runs the LIDAR in data collection mode without visualization
        /// </summary>
        private static void RunDataCollection(LidarLD06 lidar)
        {
            while (true)
            {
                var perfToken = Performance.Start("LIDAR data collection");
                try
                {
                    // Get latest scan data
                    Vector2[] points = lidar.QuerySensor();
                    Logging.Log($"Collected {points.Length} points", Logging.Level.Performance);
                    SendPoints(points);
                }
                catch (Exception ex)
                {
                    Logging.Log($"Data collection error: {ex.Message}", Logging.Level.Error);
                }
                Performance.Stop(perfToken);
                Thread.Sleep(REFRESH_RATE);
                SendLog();
            }
        }

        /// <summary>
        /// Clears the canvas by filling it with spaces and a border
        /// </summary>
        private static void ClearCanvas(char[,] canvas)
        {
            // Fill with spaces
            for (int y = 0; y < CANVAS_HEIGHT; y++)
            {
                for (int x = 0; x < CANVAS_WIDTH; x++)
                {
                    canvas[y, x] = ' ';
                }
            }

            // Draw border
            for (int x = 0; x < CANVAS_WIDTH; x++)
            {
                canvas[0, x] = '─';
                canvas[CANVAS_HEIGHT - 1, x] = '─';
            }
            for (int y = 0; y < CANVAS_HEIGHT; y++)
            {
                canvas[y, 0] = '│';
                canvas[y, CANVAS_WIDTH - 1] = '│';
            }

            // Draw corners
            canvas[0, 0] = '┌';
            canvas[0, CANVAS_WIDTH - 1] = '┐';
            canvas[CANVAS_HEIGHT - 1, 0] = '└';
            canvas[CANVAS_HEIGHT - 1, CANVAS_WIDTH - 1] = '┘';

            // Mark LIDAR position
            canvas[CANVAS_HEIGHT / 2, CANVAS_WIDTH / 2] = '+';
        }

        /// <summary>
        /// Draws the canvas to the console
        /// </summary>
        private static void DrawCanvas(char[,] canvas)
        {
            try
            {
                Console.SetCursorPosition(0, 0);

                for (int y = 0; y < CANVAS_HEIGHT; y++)
                {
                    for (int x = 0; x < CANVAS_WIDTH; x++)
                    {
                        Console.Write(canvas[y, x]);
                    }
                    Console.WriteLine();
                }
            }
            catch
            {
                // If we can't position cursor, try to write anyway
                for (int y = 0; y < CANVAS_HEIGHT; y++)
                {
                    for (int x = 0; x < CANVAS_WIDTH; x++)
                    {
                        Console.Write(canvas[y, x]);
                    }
                    Console.WriteLine();
                }
            }
        }

        private static void SendLog()
        {
            var logs = Logging.GetLog();
            if(logs.Count == 0) return; 
            var logEntries = logs.Select(l => new LogEntry { Level = l.level, Message = l.message }).ToList();
            string payload = "[LOGS]" + JsonSerializer.Serialize(logEntries, new JsonSerializerOptions { WriteIndented = false });

            bool sent = false;
            while (!sent)
            {
                try
                {
                    Console.WriteLine("Sending message: " + payload);
                    connector.Send(payload);
                    sent = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Send failed, retrying: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }
        }

        private static void SendPoints(Vector2[] points)
        {
            var serializablePoints = points.Select(p => ((short)p.X, (short)p.Y)).ToArray();
            string message = "[POINTS]";
            byte[] data = new byte[serializablePoints.Length * 4];
            for (int i = 0; i < serializablePoints.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(serializablePoints[i].Item1), 0, data, i * 4, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(serializablePoints[i].Item2), 0, data, i * 4 + 2, 2);
            }
            connector.Send(message, data);
        }
        
    }
}