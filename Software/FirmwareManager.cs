using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Common;

namespace NyandroidMite
{
    /// <summary>
    /// Manages communication with the Nyandroid Mite microcontroller firmware.
    /// Handles serial port communication, device detection, and command sending.
    /// </summary>
    public class FirmwareManager : IDisposable
    {
        private SerialPort? _serialPort;
        private readonly string _portName;
        private readonly int _baudRate;
        private bool _isDisposed;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _readTask;

        /// <summary>
        /// Event raised when button states are received from the microcontroller.
        /// </summary>
        public event EventHandler<ButtonStatesEventArgs>? ButtonStatesReceived;

        /// <summary>
        /// Event raised when analog values are received from the microcontroller.
        /// </summary>
        public event EventHandler<AnalogValuesEventArgs>? AnalogValuesReceived;

        /// <summary>
        /// Initializes a new instance of the FirmwareManager class.
        /// </summary>
        /// <param name="portName">The name of the serial port to use. If null, will attempt to auto-detect the port.</param>
        /// <param name="baudRate">The baud rate for serial communication. Defaults to 115200.</param>
        /// <exception cref="Exception">Thrown when no microcontroller is found and no port name is specified.</exception>
        public FirmwareManager(string? portName = null, int baudRate = 115200)
        {
            _portName = portName ?? DetectMicrocontrollerPort() ?? throw new Exception("No microcontroller found");
            _baudRate = baudRate;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Attempts to detect the Nyandroid Mite microcontroller by scanning available serial ports.
        /// </summary>
        /// <returns>The port name if found, null otherwise.</returns>
        /// <remarks>
        /// On Linux systems, only USB serial devices (/dev/ttyUSB* and /dev/ttyACM*) are checked.
        /// The detection process involves sending a handshake command and waiting for a specific response.
        /// </remarks>
        public static string? DetectMicrocontrollerPort()
        {
            // Get all available serial ports
            var ports = SerialPort.GetPortNames();
            
            // On Linux, filter to only USB serial devices
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                ports = ports.Where(p => p.StartsWith("/dev/ttyUSB") || p.StartsWith("/dev/ttyACM")).ToArray();
            }

            foreach (var port in ports)
            {
                Logging.Log($"Checking port {port}", Logging.Level.Debug);
                try
                {
                    using var testPort = new SerialPort(port, 115200)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 500
                    };

                    testPort.Open();
                    testPort.WriteLine("HANDSHAKE");
                    
                    // Wait for response
                    var response = testPort.ReadLine().Trim();
                    if (response == "NYANDROID_MITE")
                    {
                        Logging.Log($"Found at port {port}", Logging.Level.Debug);
                        return port;
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log($"Error:\n{ex.Message}", Logging.Level.Debug);
                    // Ignore errors and try next port
                    continue;
                }
            }

            return null;
        }

        /// <summary>
        /// Gets a value indicating whether the manager is currently connected to the microcontroller.
        /// </summary>
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        /// <summary>
        /// Establishes a connection to the microcontroller.
        /// </summary>
        /// <exception cref="Exception">Thrown when connection to the specified port fails.</exception>
        public void Connect()
        {
            if (_serialPort != null && _serialPort.IsOpen)
                return;

            _serialPort = new SerialPort(_portName, _baudRate)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };

            try
            {
                _serialPort.Open();
                StartReading();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to connect to port {_portName}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Disconnects from the microcontroller and stops reading data.
        /// </summary>
        public void Disconnect()
        {
            _cancellationTokenSource.Cancel();
            _readTask?.Wait();
            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;
        }

        /// <summary>
        /// Starts an asynchronous task to continuously read data from the serial port.
        /// </summary>
        private void StartReading()
        {
            _readTask = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_serialPort?.IsOpen == true)
                        {
                            string line = _serialPort.ReadLine();
                            ProcessSensorData(line);
                        }
                    }
                    catch (TimeoutException)
                    {
                        // Ignore timeout exceptions as they're expected when no data is available
                    }
                    catch (Exception ex)
                    {
                        Logging.Log($"Error reading from serial port: {ex.Message}", Logging.Level.Debug);
                    }

                    await Task.Delay(10, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Processes sensor data received from the microcontroller.
        /// </summary>
        /// <param name="data">The raw data string received from the microcontroller.</param>
        /// <remarks>
        /// Expected format: "B:0000000 A:123,456,789,..."
        /// - B: followed by 7 digits (0 or 1) representing button states
        /// - A: followed by comma-separated analog values
        /// </remarks>
        private void ProcessSensorData(string data)
        {
            // Format: B:0000000 A:123,456,789,...
            var parts = data.Split(' ');
            if (parts.Length != 2)
                return;

            // Process button states
            if (parts[0].StartsWith("B:"))
            {
                var buttonStates = parts[0][2..].Select(c => c == '1').ToArray();
                ButtonStatesReceived?.Invoke(this, new ButtonStatesEventArgs(buttonStates));
            }

            // Process analog values
            if (parts[1].StartsWith("A:"))
            {
                var analogValues = parts[1][2..].Split(',').Select(int.Parse).ToArray();
                AnalogValuesReceived?.Invoke(this, new AnalogValuesEventArgs(analogValues));
            }
        }

        /// <summary>
        /// Sends a command to control motors and servos.
        /// </summary>
        /// <param name="motor1Speed">Speed for motor 1 (-255 to 255).</param>
        /// <param name="motor2Speed">Speed for motor 2 (-255 to 255).</param>
        /// <param name="servos">Array of servo pin and position pairs to control.</param>
        /// <exception cref="InvalidOperationException">Thrown when not connected to the microcontroller.</exception>
        /// <remarks>
        /// Command format: "M1,M2,S1,P1,S2,P2,..."
        /// - M1: Motor 1 speed
        /// - M2: Motor 2 speed
        /// - S1: Servo 1 pin
        /// - P1: Servo 1 position
        /// - S2: Servo 2 pin
        /// - P2: Servo 2 position
        /// </remarks>
        public void SendCommand(int motor1Speed, int motor2Speed, params (int pin, int position)[] servos)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Not connected to firmware");

            // Format: M1,M2,S1,P1,S2,P2,...
            var command = $"{motor1Speed},{motor2Speed}";
            
            foreach (var (pin, position) in servos)
            {
                command += $",{pin},{position}";
            }

            _serialPort?.WriteLine(command);
        }

        /// <summary>
        /// Releases all resources used by the FirmwareManager.
        /// </summary>
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                Disconnect();
                _cancellationTokenSource.Dispose();
            }
        }
    }
} 