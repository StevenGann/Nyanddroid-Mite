using System;
using System.Threading;
using Common;
using Xunit;

namespace Mite.Tests
{
    public class NetworkConnectorTests : IDisposable
    {
        private INetworkConnector? _connectorA;
        private INetworkConnector? _connectorB;

        // Helper method to test two connectors sending messages back and forth
        private void RunConnectorEchoTest(INetworkConnector connectorA, INetworkConnector connectorB)
        {
            // Use random ports to avoid conflicts
            var rand = new Random();
            int portA = rand.Next(20000, 30000);
            int portB = portA + 1;

            Console.WriteLine($"Connecting: A({portA}->{portB})");
            connectorA.Connect(portA, portB);
            Thread.Sleep(200); // Give A's listener time to start
            Console.WriteLine($"Connecting: B({portB}->{portA})");
            connectorB.Connect(portB, portA);
            Console.WriteLine("Both connectors connected.");

            string messageA = "Hello from A";
            string messageB = "Hello from B";

            // Prepare random data for testing
            byte[] randomData = new byte[1024*1024];
            rand.NextBytes(randomData);

            // Thread for A to send and then receive
            (string? msg, byte[]? data) receivedByA = (null, null);
            var threadA = new Thread(() =>
            {
                Console.WriteLine("[A] Sending messageA with randomData...");
                connectorA.Send(messageA, randomData);
                Console.WriteLine("[A] Sent messageA, now waiting to receive...");
                receivedByA = connectorA.Receive();
                Console.WriteLine($"[A] Received: {receivedByA.msg}, data length: {receivedByA.data?.Length}");
            });

            // Thread for B to receive and then send
            (string? msg, byte[]? data) receivedByB = (null, null);
            var threadB = new Thread(() =>
            {
                Console.WriteLine("[B] Waiting to receive messageA...");
                receivedByB = connectorB.Receive();
                Console.WriteLine($"[B] Received: {receivedByB.msg}, data length: {receivedByB.data?.Length}, now sending messageB with null data...");
                connectorB.Send(messageB, null);
                Console.WriteLine("[B] Sent messageB.");
            });

            threadB.Start();
            Thread.Sleep(100); // Ensure B is ready to receive
            threadA.Start();
            Console.WriteLine("Threads started, waiting for join...");
            threadA.Join(5000);
            Thread.Sleep(200); // Give time for the message to be delivered
            threadB.Join(5000);
            Console.WriteLine("Threads joined, asserting results...");

            // Assert message and data
            Assert.Equal(messageA, receivedByB.msg);
            Assert.NotNull(receivedByB.data);
            Assert.Equal(randomData, receivedByB.data);
            Assert.Equal(messageB, receivedByA.msg);
            Assert.Null(receivedByA.data);
            Console.WriteLine("Test completed successfully.");
        }

        [Fact]
        public void TcpConnector_EchoTest()
        {
            Console.WriteLine("Starting TcpConnector_EchoTest");
            _connectorA = new TcpConnector();
            _connectorB = new TcpConnector();
            RunConnectorEchoTest(_connectorA, _connectorB);
        }

        [Fact]
        public void NetMQConnector_EchoTest()
        {
            Console.WriteLine("Starting NetMQConnector_EchoTest");
            _connectorA = new NetMQConnector();
            _connectorB = new NetMQConnector();
            RunConnectorEchoTest(_connectorA, _connectorB);
        }

        

        public void Dispose()
        {
            (_connectorA as IDisposable)?.Dispose();
            (_connectorB as IDisposable)?.Dispose();
        }
    }
}
