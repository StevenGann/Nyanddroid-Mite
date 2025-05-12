using System;
using System.Collections.Generic;
using System.Text.Json;
using Common;
using System.Numerics;
using static Raylib_cs.Raylib;
using Raylib_cs;
using System.Threading;

namespace Dashboard
{
    class LidarVisualizer
    {
        private static Vector2[] points = new Vector2[0];
        private static readonly object pointsLock = new object();
        private static double scale = 0.1;

        public static INetworkConnector Connector { get; set; }

        private static Vector2 offset = new Vector2(0, 0);

        private static int frameCounter = 0;

        private static int throttle = 128;
        public static void Run()
        {
            InitWindow(1024, 1024, "LIDAR Visualizer");

            FirmwareCommand lastCommand = new FirmwareCommand { Motor1Speed = 0, Motor2Speed = 0 };
            while (!WindowShouldClose())
            {
                BeginDrawing();
                ClearBackground(Color.Black);

                int pointsCount;
                lock (pointsLock)
                {
                    pointsCount = points.Length;

                    DrawPoints(points, scale, 1024, 1024, offset);
                }
                DrawText($"Points: {pointsCount}", 4, 4, 20, Color.LightGray);
                DrawText($"Throttle: {throttle}", 4, 30, 20, Color.LightGray);

                EndDrawing();

                // Handle input for panning (arrow keys)
                float panStep = 1f;
                if (IsKeyDown(KeyboardKey.Right)) offset.X += panStep;
                if (IsKeyDown(KeyboardKey.Left)) offset.X -= panStep;
                if (IsKeyDown(KeyboardKey.Down)) offset.Y += panStep;
                if (IsKeyDown(KeyboardKey.Up)) offset.Y -= panStep;

                // Handle input for zooming (plus/minus keys)
                double scaleStep = 0.001;
                if (IsKeyDown(KeyboardKey.Equal)) scale += scaleStep; // '+' key (usually shares '=')
                if (IsKeyDown(KeyboardKey.Minus)) scale = Math.Max(0.01, scale - scaleStep);

                if (IsKeyDown(KeyboardKey.R)) throttle += 1;
                if (IsKeyDown(KeyboardKey.F)) throttle -= 1;

                // Track key states for W, A, S, D
                bool w = IsKeyDown(KeyboardKey.W);
                bool a = IsKeyDown(KeyboardKey.A);
                bool s = IsKeyDown(KeyboardKey.S);
                bool d = IsKeyDown(KeyboardKey.D);

                FirmwareCommand command;
                if (w) { command = new FirmwareCommand { Motor1Speed = -throttle, Motor2Speed = -throttle }; }
                else if (s) { command = new FirmwareCommand { Motor1Speed = throttle, Motor2Speed = throttle }; }
                else if (a) { command = new FirmwareCommand { Motor1Speed = -throttle, Motor2Speed = throttle }; }
                else if (d) { command = new FirmwareCommand { Motor1Speed = throttle, Motor2Speed = -throttle }; }
                else { command = new FirmwareCommand { Motor1Speed = 0, Motor2Speed = 0 }; }

                // Only send if the command has changed
                if (command.Motor1Speed != lastCommand.Motor1Speed || command.Motor2Speed != lastCommand.Motor2Speed)
                {
                    string commandString = "[COMMAND]" + command.ToJson();
                    Connector.Send(commandString);
                    lastCommand = command;
                }

                frameCounter++;
            }
        }

        private static void DrawPoints(Vector2[] points, double scale, double width, double height, Vector2 offset)
        {
            int minX = -(int)(scale * width / 2);
            int maxX = (int)(scale * width / 2);
            int minY = -(int)(scale * height / 2);
            int maxY = (int)(scale * height / 2);

            for (int i = 0; i < points.Length; i++)
            {
                int x = (int)(points[i].X * scale + width / 2 + offset.X);
                int y = (int)(points[i].Y * scale + height / 2 + offset.Y);
                DrawLine(x, y, (int)((width / 2) + offset.X), (int)((height / 2) + offset.Y), Color.Lime);
                DrawPixel(x, y, Color.White);
                DrawPixel(x - 1, y - 1, Color.White);
                DrawPixel(x + 1, y + 1, Color.White);
                DrawPixel(x - 1, y + 1, Color.White);
                DrawPixel(x + 1, y - 1, Color.White);
            }
        }



        public static void UpdatePoints(Vector2[] newPoints)
        {
            lock (pointsLock)
            {
                points = newPoints;
            }
        }

        public static void Start()
        {
            Thread thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Start();
        }
    }
}