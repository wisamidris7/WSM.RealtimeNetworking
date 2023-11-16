﻿using System.Net;
using System.Net.Sockets;

namespace WSM.ServerRealtime.Scripts;

internal class Server
{

    public static int MaxPlayers { get; private set; }
    public static int Port { get; private set; }
    public static Dictionary<int, Client> clients = [];
    public delegate void PacketHandler(int clientID, Packet packet);
    public static Dictionary<int, PacketHandler> packetHandlers;

    private static TcpListener tcpListener;
    private static UdpClient udpListener;

    public static void Start(int maxPlayers, int port)
    {
        MaxPlayers = maxPlayers;
        Port = port;
        for (int i = 1; i <= MaxPlayers; i++)
        {
            clients.Add(i, new Client(i));
        }
        packetHandlers = new Dictionary<int, PacketHandler>()
        {
            { (int)Packet.ID.STRING, Receiver.ReceiveString },
            { (int)Packet.ID.BOOLEAN, Receiver.ReceiveBoolean },
            { (int)Packet.ID.VECTOR3, Receiver.ReceiveVector3 },
            { (int)Packet.ID.QUATERNION, Receiver.ReceiveQuaternion },
            { (int)Packet.ID.FLOAT, Receiver.ReceiveFloat },
            { (int)Packet.ID.INTEGER, Receiver.ReceiveInteger },
            { (int)Packet.ID.LONG, Receiver.ReceiveLong },
            { (int)Packet.ID.SHORT, Receiver.ReceiveShort },
            { (int)Packet.ID.BYTES, Receiver.ReceiveBytes },
            { (int)Packet.ID.BYTE, Receiver.ReceiveByte },
            { (int)Packet.ID.INITIALIZATION, Receiver.Initialization },
            { (int)Packet.ID.NULL, Receiver.ReceiveNull },
            { (int)Packet.ID.CUSTOM, Receiver.ReceiveCustom },
        };
        tcpListener = new TcpListener(IPAddress.Any, Port);
        tcpListener.Start();
        _ = tcpListener.BeginAcceptTcpClient(OnConnectedTCP, null);
        udpListener = new UdpClient(Port);
        _ = udpListener.BeginReceive(OnConnectedUDP, null);
        Terminal.Start();
    }

    private static void OnConnectedTCP(IAsyncResult result)
    {
        TcpClient client = tcpListener.EndAcceptTcpClient(result);
        _ = tcpListener.BeginAcceptTcpClient(OnConnectedTCP, null);
        Console.WriteLine("Incoming connection from {0}.", client.Client.RemoteEndPoint);
        for (int i = 1; i <= MaxPlayers; i++)
        {
            if (clients[i].tcp.socket == null)
            {
                IPEndPoint? ip = client.Client.RemoteEndPoint as IPEndPoint;
                clients[i].tcp.Initialize(client);
                Terminal.OnClientConnected(i, ip.Address.ToString());
                return;
            }
        }
        Console.WriteLine("{0} failed to connect. Server is at full capacity.", client.Client.RemoteEndPoint);
    }

    private static void OnConnectedUDP(IAsyncResult result)
    {
        try
        {
            IPEndPoint clientEndPoint = new(IPAddress.Any, 0);
            byte[] data = udpListener.EndReceive(result, ref clientEndPoint);
            _ = udpListener.BeginReceive(OnConnectedUDP, null);
            if (data.Length < 4)
            {
                return;
            }
            using Packet packet = new(data);
            int id = packet.ReadInt();
            if (id == 0)
            {
                return;
            }
            if (clients[id].udp.endPoint == null)
            {
                clients[id].udp.Connect(clientEndPoint);
                return;
            }
            if (clients[id].udp.endPoint.ToString() == clientEndPoint.ToString())
            {
                clients[id].udp.CheckData(packet);
            }
        } catch (Exception ex)
        {
            Tools.LogError(ex.Message, ex.StackTrace);
        }
    }

    public static void SendDataUDP(IPEndPoint clientEndPoint, Packet packet)
    {
        try
        {
            if (clientEndPoint != null)
            {
                _ = udpListener.BeginSend(packet.ToArray(), packet.Length(), clientEndPoint, null, null);
            }
        } catch (Exception ex)
        {
            Tools.LogError(ex.Message, ex.StackTrace);
        }
    }

}