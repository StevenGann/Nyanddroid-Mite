using System;
using System.Threading;
using System.Numerics;

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
        private const int REFRESH_RATE = 16; // Milliseconds between updates

        /// <summary>
        /// The entry point of the application.
        /// </summary>
        static void Main(string[] args)
        {
            Console.WriteLine("Nyandroid Mite LIDAR Visualizer");
            Console.WriteLine("==============================");
            
            // Get port name from command line or use default
            string portName = args.Length > 0 ? args[0] : 
                Environment.OSVersion.Platform == PlatformID.Unix ? "/dev/ttyUSB0" : "COM6";

            Console.WriteLine($"Using port: {portName}");
            Console.WriteLine("Press Ctrl+C to exit");
            Console.WriteLine();

            // Configure console
            try
            {
                Console.BufferHeight = Math.Max(Console.BufferHeight, CANVAS_HEIGHT + 8); // Extra room for diagnostics
                Console.WindowHeight = Math.Min(50, Console.BufferHeight);
            }
            catch
            {
                // Some console environments don't support buffer size modification
            }

            // Create and configure LIDAR
            using var lidar = new LidarLD06(portName);
            
            try
            {
                // Center the LIDAR in the visualization
                lidar.ConfigureSensor(Vector2.Zero, 0);
                
                Console.WriteLine("Connecting to LIDAR...");
                lidar.Connect();
                Console.WriteLine("Connected successfully!");

                // Setup visualization
                Console.CursorVisible = false;
                char[,] canvas = new char[CANVAS_HEIGHT, CANVAS_WIDTH];

                // Main visualization loop
                while (true)
                {
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
                                Console.WriteLine($"Scale: 1 unit = {1/SCALE:F1}mm");
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
                        Console.WriteLine($"Visualization error: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                return;
            }
            finally
            {
                Console.CursorVisible = true;
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
    }
} 