using NetMQ;
using NetMQ.Sockets;

namespace common;

public class SubsystemConnector
{
    private ResponseSocket server;
    private RequestSocket client;

    public SubsystemConnector(int ListeningPort, int TargetPort, string TargetIp = "localhost")
    {
        Console.WriteLine("binding");
        server = new ResponseSocket($"@tcp://0.0.0.0:{ListeningPort}"); // bind
        Console.WriteLine("connecting");
        client = new RequestSocket($">tcp://{TargetIp}:{TargetPort}");  // connect

        //Console.WriteLine("sending");
        // Send a message from the client socket
        //client.SendFrame("Hello");
    }

    public string Receive()
    {
        string message = server.ReceiveFrameString();
        //Console.WriteLine($"Received: {message}");
        server.SendFrame("[ACK]");
        return message;
    }

    public void Send(string Message)
    {        
        client.SendFrame(Message);
        string ack = client.ReceiveFrameString();
        if( ack != "[ACK]")
        {
            Console.WriteLine("SEND ERROR ACK=" + ack);
        }
    }
}
