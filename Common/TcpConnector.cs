using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Linq;

namespace Common
{
    /// <summary>
    /// Implements INetworkConnector using TCP/IP for robust, symmetric, bidirectional communication.
    /// </summary>
    public class TcpConnector : INetworkConnector, IDisposable
    {
        private TcpListener? _listener;
        private TcpClient? _connection;
        private NetworkStream? _stream;
        private Thread? _acceptThread;
        private Thread? _readThread;
        private readonly ConcurrentQueue<(string, byte[]?)> _receivedMessages = new();
        private readonly AutoResetEvent _messageReceived = new(false);
        private volatile bool _running = false;
        private int _listeningPort;
        private int _targetPort;
        private string _targetIp = "127.0.0.1";
        private readonly object _connectLock = new();

        public bool IsConnected => _connection != null && _connection.Connected && _stream != null;

        public void Connect(int ListeningPort, int TargetPort, string TargetIp = "127.0.0.1")
        {
            _listeningPort = ListeningPort;
            _targetPort = TargetPort;
            _targetIp = TargetIp;
            _running = true;

            Logging.Log($"TcpConnector: Starting listener on port {_listeningPort}", Logging.Level.Info);
            // Start listener
            _listener = new TcpListener(IPAddress.Any, _listeningPort);
            _listener.Start();

            Logging.Log($"TcpConnector: Starting accept thread on port {_listeningPort}", Logging.Level.Info);
            // Accept incoming connection in background
            _acceptThread = new Thread(() =>
            {
                try
                {
                    Logging.Log($"TcpConnector: Accept thread waiting for incoming connection on port {_listeningPort}", Logging.Level.Info);
                    var client = _listener.AcceptTcpClient();
                    lock (_connectLock)
                    {
                        if (_connection == null)
                        {
                            _connection = client;
                            _stream = _connection.GetStream();
                            Logging.Log($"TcpConnector: Accepted incoming connection on port {_listeningPort}", Logging.Level.Info);
                            StartReadThread();
                        }
                        else
                        {
                            client.Close(); // Already connected
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_running)
                        Logging.Log($"TcpConnector: Accept failed: {ex.Message}", Logging.Level.Error);
                }
            }) { IsBackground = true };
            _acceptThread.Start();

            Logging.Log($"TcpConnector: Starting connect thread to {_targetIp}:{_targetPort}", Logging.Level.Info);
            // Try to connect to peer in background
            new Thread(() =>
            {
                while (_running && _connection == null)
                {
                    try
                    {
                        Logging.Log($"TcpConnector: Attempting outgoing connection to {_targetIp}:{_targetPort}", Logging.Level.Info);
                        var client = new TcpClient();
                        var result = client.BeginConnect(_targetIp, _targetPort, null, null);
                        bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(500));
                        if (success && client.Connected)
                        {
                            client.EndConnect(result);
                            lock (_connectLock)
                            {
                                if (_connection == null)
                                {
                                    _connection = client;
                                    _stream = _connection.GetStream();
                                    Logging.Log($"TcpConnector: Outgoing connection to {_targetIp}:{_targetPort} established", Logging.Level.Info);
                                    StartReadThread();
                                }
                                else
                                {
                                    client.Close(); // Already connected
                                }
                            }
                            break;
                        }
                        else
                        {
                            client.Close();
                            Logging.Log($"TcpConnector: Outgoing connection to {_targetIp}:{_targetPort} failed, will retry", Logging.Level.Info);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logging.Log($"TcpConnector: Connect failed: {ex.Message}", Logging.Level.Debug);
                    }
                    Thread.Sleep(200);
                }
            }) { IsBackground = true }.Start();
        }

        private void StartReadThread()
        {
            _readThread = new Thread(() =>
            {
                try
                {
                    var buffer = new byte[4096];
                    var leftover = new List<byte>();
                    while (_running && _stream != null && _connection != null && _connection.Connected)
                    {
                        int bytesRead = 0;
                        try
                        {
                            bytesRead = _stream.Read(buffer, 0, buffer.Length);
                        }
                        catch (Exception ex)
                        {
                            if (_running)
                                Logging.Log($"TcpConnector: Read failed: {ex.Message}", Logging.Level.Error);
                            break;
                        }
                        if (bytesRead > 0)
                        {
                            leftover.AddRange(buffer.AsSpan(0, bytesRead).ToArray());
                            // Try to parse as many complete messages as possible
                            while (true)
                            {
                                if (leftover.Count < 4) break; // Not enough for message length
                                int msgLen = BitConverter.ToInt32(leftover.ToArray(), 0);
                                if (leftover.Count < 4 + msgLen + 4) break; // Not enough for message + data length
                                string msg = System.Text.Encoding.UTF8.GetString(leftover.ToArray(), 4, msgLen);
                                int dataLen = BitConverter.ToInt32(leftover.ToArray(), 4 + msgLen);
                                if (leftover.Count < 4 + msgLen + 4 + dataLen) break; // Not enough for data
                                byte[]? data = null;
                                if (dataLen > 0)
                                {
                                    data = leftover.Skip(4 + msgLen + 4).Take(dataLen).ToArray();
                                }
                                _receivedMessages.Enqueue((msg, data));
                                _messageReceived.Set();
                                leftover = leftover.Skip(4 + msgLen + 4 + dataLen).ToList();
                            }
                        }
                        else
                        {
                            Thread.Sleep(10);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (_running)
                        Logging.Log($"TcpConnector: ReadThread error: {ex.Message}", Logging.Level.Error);
                }
            }) { IsBackground = true };
            _readThread.Start();
        }

        public void Send(string message, byte[]? data = null)
        {
            if (!_running) throw new ObjectDisposedException(nameof(TcpConnector));
            int waited = 0;
            while (!IsConnected && waited < 2000)
            {
                Thread.Sleep(50);
                waited += 50;
            }
            if (!IsConnected)
                throw new InvalidOperationException("TcpConnector: Not connected to peer (after waiting).");
            try
            {
                byte[] msgBytes = Encoding.UTF8.GetBytes(message);
                byte[] dataBytes = data ?? Array.Empty<byte>();
                byte[] msgLen = BitConverter.GetBytes(msgBytes.Length);
                byte[] dataLen = BitConverter.GetBytes(dataBytes.Length);
                _stream.Write(msgLen, 0, 4);
                _stream.Write(msgBytes, 0, msgBytes.Length);
                _stream.Write(dataLen, 0, 4);
                if (dataBytes.Length > 0)
                    _stream.Write(dataBytes, 0, dataBytes.Length);
                _stream.Flush();
            }
            catch (Exception ex)
            {
                Logging.Log($"TcpConnector: Send failed: {ex.Message}", Logging.Level.Error);
                throw;
            }
        }

        public (string message, byte[]? data) Receive()
        {
            if (!_running) throw new ObjectDisposedException(nameof(TcpConnector));
            int waited = 0;
            while (!IsConnected && waited < 2000)
            {
                Thread.Sleep(50);
                waited += 50;
            }
            if (!IsConnected)
                throw new InvalidOperationException("TcpConnector: Not connected to peer (after waiting).");
            _messageReceived.WaitOne();
            if (_receivedMessages.TryDequeue(out var msg))
                return msg;
            return (string.Empty, null);
        }

        public void Dispose()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _connection?.Close(); } catch { }
            try { _stream?.Close(); } catch { }
            _messageReceived.Set();
            if (_acceptThread != null && _acceptThread.IsAlive)
            {
                _acceptThread.Join(500);
            }
            if (_readThread != null && _readThread.IsAlive)
            {
                _readThread.Join(500);
            }
            // Allow OS to clean up ports before next test
            Thread.Sleep(250);
        }
    }
}