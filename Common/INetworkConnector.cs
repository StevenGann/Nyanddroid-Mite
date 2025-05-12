namespace Common
{
    /// <summary>
    /// Defines a contract for network connectors supporting bidirectional communication.
    /// </summary>
    public interface INetworkConnector
    {
        /// <summary>
        /// Establishes a connection for bidirectional communication.
        /// </summary>
        /// <param name="ListeningPort">The local port to listen on for incoming connections.</param>
        /// <param name="TargetPort">The remote port to connect to.</param>
        /// <param name="TargetIp">The IP address of the remote host. Defaults to 127.0.0.1.</param>
        void Connect(int ListeningPort, int TargetPort, string TargetIp = "127.0.0.1");

        /// <summary>
        /// Receives a message and optional data from the remote endpoint.
        /// </summary>
        /// <returns>A tuple containing the received message and optional data as a byte array.</returns>
        (string message, byte[]? data) Receive();

        /// <summary>
        /// Sends a message and optional data to the remote endpoint.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="data">Optional data to send as a byte array.</param>
        void Send(string message, byte[]? data = null);
    }
}