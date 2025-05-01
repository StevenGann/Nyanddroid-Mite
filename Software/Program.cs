using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace NyandroidMite
{
    public class FirmwareManager : IDisposable
    {
        private SerialPort? _serialPort;
        private readonly string _portName;
        private readonly int _baudRate;
        private bool _isDisposed;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _readTask;

        public event EventHandler<ButtonStatesEventArgs>? ButtonStatesReceived;
        public event EventHandler<AnalogValuesEventArgs>? AnalogValuesReceived;

        public FirmwareManager(string? portName = null, int baudRate = 115200)
        {
            _portName = portName ?? DetectMicrocontrollerPort() ?? throw new Exception("No microcontroller found");
            _baudRate = baudRate;
            _cancellationTokenSource = new CancellationTokenSource();
        }

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
                        return port;
                    }
                }
                catch
                {
                    // Ignore errors and try next port
                    continue;
                }
            }

            return null;
        }

        public bool IsConnected => _serialPort?.IsOpen ?? false;

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

        public void Disconnect()
        {
            _cancellationTokenSource.Cancel();
            _readTask?.Wait();
            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;
        }

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
                        Console.WriteLine($"Error reading from serial port: {ex.Message}");
                    }

                    await Task.Delay(10, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

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

    public class ButtonStatesEventArgs : EventArgs
    {
        public bool[] ButtonStates { get; }

        public ButtonStatesEventArgs(bool[] buttonStates)
        {
            ButtonStates = buttonStates;
        }
    }

    public class AnalogValuesEventArgs : EventArgs
    {
        public int[] AnalogValues { get; }

        public AnalogValuesEventArgs(int[] analogValues)
        {
            AnalogValues = analogValues;
        }
    }
}
