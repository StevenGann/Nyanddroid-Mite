using System;
using System.Threading;
using System.Collections.Concurrent;
using NetMQ;
using NetMQ.Sockets;

namespace Common
{
    /// <summary>
    /// Implements INetworkConnector using two NetMQ PairSockets and a NetMQPoller for robust, symmetric, bidirectional communication.
    /// </summary>
    public class NetMQConnector : INetworkConnector, IDisposable
    {
        private PairSocket? _bindSocket;
        private PairSocket? _connectSocket;
        private NetMQPoller? _poller;
        private readonly ConcurrentQueue<(string, byte[]?)> _receivedMessages = new();
        private readonly AutoResetEvent _messageReceived = new(false);
        private volatile bool _running = false;
        private bool _isBound = false;
        private bool _isConnected = false;
        private string? _bindAddress;
        private string? _connectAddress;
        private readonly object _connectLock = new();
        private Thread? _pollerThread;

        public bool IsConnected => _isBound && _isConnected && _bindSocket != null && !_bindSocket.IsDisposed && _connectSocket != null && !_connectSocket.IsDisposed;

        public void Connect(int listeningPort, int targetPort, string targetIp = "127.0.0.1")
        {
            _running = true;
            _bindAddress = $"tcp://*:{listeningPort}";
            _connectAddress = $"tcp://{targetIp}:{targetPort}";

            _bindSocket = new PairSocket();
            _bindSocket.Options.Linger = TimeSpan.Zero;
            _bindSocket.Bind(_bindAddress);
            _isBound = true;
            Logging.Log($"PairSocket bound to {_bindAddress}", Logging.Level.Info);

            _connectSocket = new PairSocket();
            _connectSocket.Options.Linger = TimeSpan.Zero;
            _connectSocket.Connect(_connectAddress);
            _isConnected = true;
            Logging.Log($"PairSocket connected to {_connectAddress}", Logging.Level.Info);

            _poller = new NetMQPoller { _bindSocket };
            _bindSocket.ReceiveReady += (s, e) =>
            {
                if (!_running) return;
                try
                {
                    var msg = e.Socket.ReceiveFrameString();
                    byte[]? data = null;
                    if (e.Socket.TryReceiveFrameBytes(out var bytes))
                    {
                        if (bytes != null && bytes.Length > 0)
                            data = bytes;
                    }
                    Logging.Log($"PairSocket received message: {msg}, data length: {data?.Length ?? 0}", Logging.Level.Debug);
                    _receivedMessages.Enqueue((msg, data));
                    _messageReceived.Set();
                }
                catch (Exception ex)
                {
                    if (_running)
                        Logging.Log($"NetMQConnector: ReceiveReady error: {ex.Message}", Logging.Level.Error);
                }
            };

            _pollerThread = new Thread(() =>
            {
                try
                {
                    _poller.Run();
                }
                catch (Exception ex)
                {
                    if (_running)
                        Logging.Log($"NetMQConnector: Poller error: {ex.Message}", Logging.Level.Error);
                }
            }) { IsBackground = true };
            _pollerThread.Start();
        }

        public void Send(string message, byte[]? data = null)
        {
            if (!_running) throw new ObjectDisposedException(nameof(NetMQConnector));
            int waited = 0;
            while (!IsConnected && waited < 2000)
            {
                Thread.Sleep(50);
                waited += 50;
            }
            if (!IsConnected) throw new InvalidOperationException("NetMQConnector: Not connected to peer (after waiting).");
            try
            {
                if (data != null && data.Length > 0)
                {
                    _connectSocket!.SendMoreFrame(message).SendFrame(data);
                }
                else
                {
                    _connectSocket!.SendMoreFrame(message).SendFrame(Array.Empty<byte>());
                }
                Logging.Log($"PairSocket sent message: {message}, data length: {data?.Length ?? 0}", Logging.Level.Debug);
            }
            catch (Exception ex)
            {
                Logging.Log($"NetMQConnector: Send failed: {ex.Message}", Logging.Level.Error);
                throw;
            }
        }

        public (string message, byte[]? data) Receive()
        {
            if (!_running) throw new ObjectDisposedException(nameof(NetMQConnector));
            int waited = 0;
            while (!IsConnected && waited < 2000)
            {
                Thread.Sleep(50);
                waited += 50;
            }
            if (!IsConnected) throw new InvalidOperationException("NetMQConnector: Not connected to peer (after waiting).");
            _messageReceived.WaitOne();
            if (_receivedMessages.TryDequeue(out var msg))
                return msg;
            return (string.Empty, null);
        }

        public void Dispose()
        {
            _running = false;
            try { if (_poller != null && _bindSocket != null) _poller.Remove(_bindSocket); } catch { }
            try { if (_poller != null && _connectSocket != null) _poller.Remove(_connectSocket); } catch { }
            _messageReceived.Set();
            if (_poller != null && _poller.IsRunning)
            {
                _poller.Stop();
            }
            if (_pollerThread != null && _pollerThread.IsAlive)
            {
                _pollerThread.Join(2000);
                _pollerThread = null;
            }
            _poller?.Dispose();
            try { _bindSocket?.Dispose(); } catch { }
            try { _connectSocket?.Dispose(); } catch { }
            Thread.Sleep(250);
        }
    }
}
