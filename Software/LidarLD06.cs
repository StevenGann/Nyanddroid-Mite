using System;
using System.Threading;
using System.Numerics;
using System.IO.Ports;
using System.Collections.Concurrent;
using System.Text;
using System.Linq;

namespace NyandroidMite
{
    /// <summary>
    /// Implementation of ISensor2D for the LD06 LIDAR sensor.
    /// Handles communication and data processing for the LD06 LIDAR module.
    /// </summary>
    /// <remarks>
    /// The LD06 LIDAR is a 2D laser scanner that provides distance measurements in a 360-degree field of view.
    /// This class handles:
    /// - Serial communication with the sensor
    /// - Data packet processing and validation
    /// - Coordinate transformation from polar to Cartesian coordinates
    /// - Point filtering based on distance and confidence thresholds
    /// 
    /// The sensor communicates using a binary protocol where each packet contains:
    /// - Start bytes (2 bytes)
    /// - Motor speed (2 bytes)
    /// - Start angle (2 bytes)
    /// - 12 data points, each containing:
    ///   - Distance (2 bytes)
    ///   - Confidence (1 byte)
    /// - End angle (2 bytes)
    /// - Timestamp (2 bytes)
    /// - CRC (1 byte)
    /// </remarks>
    public class LidarLD06 : ISensor2D, IDisposable
    {
        // Protocol constants
        /// <summary>The baud rate for serial communication with the LD06 LIDAR.</summary>
        private const int LIDAR_BAUD_RATE = 230400;
        /// <summary>Minimum motor speed threshold for valid data.</summary>
        private const int WAIT_MOTOR = 5;
        /// <summary>Number of distance measurements per data packet.</summary>
        private const int POINTS_PER_PACK = 12;
        /// <summary>Minimum valid distance measurement in millimeters.</summary>
        private const int DISTANCE_MIN = 50;
        /// <summary>Minimum confidence value for valid measurements.</summary>
        private const int CONFIDENCE_MIN = 10;

        /// <summary>The name of the serial port the LIDAR is connected to.</summary>
        private readonly string _portName;
        /// <summary>The serial port connection to the LIDAR.</summary>
        private SerialPort? _serialPort;
        /// <summary>Thread-safe queue containing the most recent LIDAR measurements.</summary>
        private readonly ConcurrentQueue<Vector2> _points = new();
        /// <summary>Background thread for reading LIDAR data.</summary>
        private readonly Thread? _readThread;
        /// <summary>Flag indicating whether the background thread should continue running.</summary>
        private bool _isRunning = true;
        /// <summary>The position offset of the LIDAR relative to the robot's center.</summary>
        private Vector2 _offset;
        /// <summary>The rotation offset of the LIDAR in radians.</summary>
        private float _rotationRadians;

        /// <summary>
        /// CRC lookup table for the LD06 protocol.
        /// Used to validate data packet integrity.
        /// </summary>
        private static readonly byte[] LDCRC = {
            0x00, 0x4d, 0x9a, 0xd7, 0x79, 0x34, 0xe3, 0xae, 0xf2, 0xbf, 0x68, 0x25, 0x8b, 0xc6, 0x11, 0x5c,
            0xa9, 0xe4, 0x33, 0x7e, 0xd0, 0x9d, 0x4a, 0x07, 0x5b, 0x16, 0xc1, 0x8c, 0x22, 0x6f, 0xb8, 0xf5,
            0x1f, 0x52, 0x85, 0xc8, 0x66, 0x2b, 0xfc, 0xb1, 0xed, 0xa0, 0x77, 0x3a, 0x94, 0xd9, 0x0e, 0x43,
            0xb6, 0xfb, 0x2c, 0x61, 0xcf, 0x82, 0x55, 0x18, 0x44, 0x09, 0xde, 0x93, 0x3d, 0x70, 0xa7, 0xea,
            0x3e, 0x73, 0xa4, 0xe9, 0x47, 0x0a, 0xdd, 0x90, 0xcc, 0x81, 0x56, 0x1b, 0xb5, 0xf8, 0x2f, 0x62,
            0x97, 0xda, 0x0d, 0x40, 0xee, 0xa3, 0x74, 0x39, 0x65, 0x28, 0xff, 0xb2, 0x1c, 0x51, 0x86, 0xcb,
            0x21, 0x6c, 0xbb, 0xf6, 0x58, 0x15, 0xc2, 0x8f, 0xd3, 0x9e, 0x49, 0x04, 0xaa, 0xe7, 0x30, 0x7d,
            0x88, 0xc5, 0x12, 0x5f, 0xf1, 0xbc, 0x6b, 0x26, 0x7a, 0x37, 0xe0, 0xad, 0x03, 0x4e, 0x99, 0xd4,
            0x7c, 0x31, 0xe6, 0xab, 0x05, 0x48, 0x9f, 0xd2, 0x8e, 0xc3, 0x14, 0x59, 0xf7, 0xba, 0x6d, 0x20,
            0xd5, 0x98, 0x4f, 0x02, 0xac, 0xe1, 0x36, 0x7b, 0x27, 0x6a, 0xbd, 0xf0, 0x5e, 0x13, 0xc4, 0x89,
            0x63, 0x2e, 0xf9, 0xb4, 0x1a, 0x57, 0x80, 0xcd, 0x91, 0xdc, 0x0b, 0x46, 0xe8, 0xa5, 0x72, 0x3f,
            0xca, 0x87, 0x50, 0x1d, 0xb3, 0xfe, 0x29, 0x64, 0x38, 0x75, 0xa2, 0xef, 0x41, 0x0c, 0xdb, 0x96,
            0x42, 0x0f, 0xd8, 0x95, 0x3b, 0x76, 0xa1, 0xec, 0xb0, 0xfd, 0x2a, 0x67, 0xc9, 0x84, 0x53, 0x1e,
            0xeb, 0xa6, 0x71, 0x3c, 0x92, 0xdf, 0x08, 0x45, 0x19, 0x54, 0x83, 0xce, 0x60, 0x2d, 0xfa, 0xb7,
            0x5d, 0x10, 0xc7, 0x8a, 0x24, 0x69, 0xbe, 0xf3, 0xaf, 0xe2, 0x35, 0x78, 0xd6, 0x9b, 0x4c, 0x01,
            0xf4, 0xb9, 0x6e, 0x23, 0x8d, 0xc0, 0x17, 0x5a, 0x06, 0x4b, 0x9c, 0xd1, 0x7f, 0x32, 0xe5, 0xa8
        };

        /// <summary>
        /// Initializes a new instance of the LidarLD06 class.
        /// </summary>
        /// <param name="portName">The serial port name to connect to (e.g., "COM3" on Windows or "/dev/ttyUSB0" on Unix)</param>
        /// <remarks>
        /// The constructor initializes the background thread but does not start it.
        /// Call <see cref="Connect"/> to establish the connection and start reading data.
        /// </remarks>
        public LidarLD06(string portName)
        {
            _portName = portName;
            _readThread = new Thread(ReadLidarData) { IsBackground = true };
        }

        /// <summary>
        /// Connects to the LIDAR sensor and starts reading data.
        /// </summary>
        /// <exception cref="Exception">Thrown when connection to the specified port fails.</exception>
        /// <remarks>
        /// This method:
        /// 1. Creates a new serial port connection with the correct baud rate
        /// 2. Opens the serial port
        /// 3. Starts the background thread for reading data
        /// </remarks>
        public void Connect()
        {
            _serialPort = new SerialPort(_portName, LIDAR_BAUD_RATE);
            try
            {
                _serialPort.Open();
                _readThread?.Start();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to LIDAR on port {_portName}: {ex.Message}", ex);
            }
        }

        /// <inheritdoc/>
        public void ConfigureSensor(Vector2 offset, float rotationRadians)
        {
            _offset = offset;
            _rotationRadians = rotationRadians;
        }

        /// <inheritdoc/>
        public (Vector2 offset, float rotationRadians) GetConfiguration()
        {
            return (_offset, _rotationRadians);
        }

        /// <inheritdoc/>
        public Vector2[] QuerySensor()
        {
            return _points.ToArray();
        }

        /// <summary>
        /// Background thread method that continuously reads and processes LIDAR data.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Reads bytes from the serial port
        /// 2. Assembles complete data packets
        /// 3. Validates packet integrity using CRC
        /// 4. Passes valid packets to <see cref="ProcessLidarPacket"/> for processing
        /// 
        /// The method continues running until <see cref="_isRunning"/> is set to false
        /// or an unrecoverable error occurs.
        /// </remarks>
        private void ReadLidarData()
        {
            if (_serialPort == null) return;

            byte[] buffer = new byte[47];
            int bufferIndex = 0;
            byte crc = 0;

            while (_isRunning)
            {
                try
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        byte current = (byte)_serialPort.ReadByte();

                        if (bufferIndex < 47)
                        {
                            if (bufferIndex < 46)
                            {
                                crc = LDCRC[crc ^ current];
                            }

                            buffer[bufferIndex] = current;
                            bufferIndex++;

                            if (bufferIndex == 47)
                            {
                                ProcessLidarPacket(buffer, crc);
                                bufferIndex = 0;
                                crc = 0;
                            }
                        }
                        else
                        {
                            bufferIndex = 0;
                            crc = 0;
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception)
                {
                    // Handle any read errors by resetting the buffer
                    bufferIndex = 0;
                    crc = 0;
                    Thread.Sleep(1);
                }
            }
        }

        /// <summary>
        /// Processes a complete LIDAR data packet and converts measurements to Cartesian coordinates.
        /// </summary>
        /// <param name="buffer">The complete data packet buffer</param>
        /// <param name="crc">The calculated CRC value for validation</param>
        /// <remarks>
        /// This method:
        /// 1. Validates packet header and CRC
        /// 2. Checks motor speed
        /// 3. Extracts start and end angles
        /// 4. Processes each measurement point:
        ///    - Validates distance and confidence values
        ///    - Calculates actual angle
        ///    - Converts from polar to Cartesian coordinates
        ///    - Applies configured offset and rotation
        ///    - Adds valid points to the point queue
        /// </remarks>
        private void ProcessLidarPacket(byte[] buffer, byte crc)
        {
            if (buffer[0] != 0x54 || buffer[1] != 0x2C || buffer[46] != crc)
            {
                return; // Invalid packet
            }

            ushort motorSpeed = (ushort)(buffer[2] | (buffer[3] << 8));
            if (motorSpeed == 0)
            {
                return; // Motor not spinning
            }

            ushort startAngle = (ushort)(buffer[4] | (buffer[5] << 8));
            ushort endAngle = (ushort)(buffer[42] | (buffer[43] << 8));

            // Process each point in the packet
            for (int i = 0; i < POINTS_PER_PACK; i++)
            {
                int distanceOffset = 6 + i * 3;
                int confidenceOffset = distanceOffset + 2;

                ushort distance = (ushort)(buffer[distanceOffset] | (buffer[distanceOffset + 1] << 8));
                byte confidence = buffer[confidenceOffset];

                if (distance >= DISTANCE_MIN && confidence >= CONFIDENCE_MIN)
                {
                    // Calculate angle for this point
                    float angle = startAngle + (endAngle - startAngle) * i / (POINTS_PER_PACK - 1);
                    angle = (angle % 36000) / 100.0f; // Convert to degrees

                    // Convert polar coordinates to Cartesian
                    float angleRad = (angle + _rotationRadians) * (float)Math.PI / 180.0f;
                    float x = distance * (float)Math.Cos(angleRad) + _offset.X;
                    float y = distance * (float)Math.Sin(angleRad) + _offset.Y;

                    // Add point to the queue, limiting size to prevent memory issues
                    while (_points.Count >= 1000)
                    {
                        _points.TryDequeue(out _);
                    }
                    _points.Enqueue(new Vector2(x, y));
                }
            }
        }

        /// <summary>
        /// Releases all resources used by the LidarLD06 instance.
        /// </summary>
        /// <remarks>
        /// This method:
        /// 1. Stops the background reading thread
        /// 2. Closes and disposes of the serial port connection
        /// </remarks>
        public void Dispose()
        {
            _isRunning = false;
            _readThread?.Join(1000);
            _serialPort?.Close();
            _serialPort?.Dispose();
        }
    }
}


