namespace Common
{
    public interface INetworkConnector
    {
        void Connect(int ListeningPort, int TargetPort, string TargetIp = "127.0.0.1");
        (string message, byte[]? data) Receive();
        void Send(string message, byte[]? data = null);
    }
}