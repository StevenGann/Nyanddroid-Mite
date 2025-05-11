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

        private static Vector2 offset = new Vector2(0, 0);
        public static void Run()
        {
            InitWindow(1024, 1024, "LIDAR Visualizer");
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

                EndDrawing();
            }
        }

        private static void DrawPoints(Vector2[] points, double scale, double width, double height, Vector2 offset)
        {
            int minX = - (int)(scale * width / 2);
            int maxX = (int)(scale * width / 2);
            int minY = - (int)(scale * height / 2);
            int maxY = (int)(scale * height / 2);

            for (int i = 0; i < points.Length; i++)
            {
                int x = (int)(points[i].X * scale + width / 2 + offset.X);
                int y = (int)(points[i].Y * scale + height / 2 + offset.Y);
                DrawLine(x, y, (int)((width / 2) + offset.X), (int)((height / 2) + offset.Y), Color.Lime);
                DrawPixel(x,y, Color.White);
                DrawPixel(x-1,y-1, Color.White);
                DrawPixel(x+1,y+1, Color.White);
                DrawPixel(x-1,y+1, Color.White);
                DrawPixel(x+1,y-1, Color.White);
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