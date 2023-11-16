using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WSM.ClientRealtime.Scripts;

public class Client
{

    private const int dataBufferSize = 4096;
    private const int connectTimeout = 5000;
    public TcpClient? socket;
    private NetworkStream? stream;
    private Packet? receivedData;
    private byte[]? receiveBuffer;
    public int id { get; private set; } = 0;

    public RealtimeNetworking RealtimeNetworking { get; set; }
    public string sendToken { get; private set; } = "xxxxx";

    public string receiveToken { get; private set; } = "xxxxx";

    public bool isConnected { get; private set; } = false;
    private delegate void PacketHandler(Packet _packet);
    private Dictionary<int, PacketHandler> packetHandlers;
    private bool _connecting = false;
    private bool _initialized = false;
    public Settings settings => new();
    private readonly Receiver _receiver;
    public Client()
    {
        _initialized = true;
        _receiver = new(this, RealtimeNetworking!);
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    public void ConnectToServer()
    {
        if (isConnected || _connecting)
        {
            return;
        }

        _connecting = true;


        packetHandlers = new Dictionary<int, PacketHandler>()
        {
            { (int)Packet.ID.STRING, _receiver.ReceiveString },
            { (int)Packet.ID.BOOLEAN, _receiver.ReceiveBoolean },
            { (int)Packet.ID.VECTOR3, _receiver.ReceiveVector3 },
            { (int)Packet.ID.QUATERNION, _receiver.ReceiveQuaternion },
            { (int)Packet.ID.FLOAT, _receiver.ReceiveFloat },
            { (int)Packet.ID.INTEGER, _receiver.ReceiveInteger },
            { (int)Packet.ID.LONG, _receiver.ReceiveLong },
            { (int)Packet.ID.SHORT, _receiver.ReceiveShort },
            { (int)Packet.ID.BYTES, _receiver.ReceiveBytes },
            { (int)Packet.ID.BYTE, _receiver.ReceiveByte },
            { (int)Packet.ID.INITIALIZATION, _receiver.Initialization },
            { (int)Packet.ID.NULL, _receiver.ReceiveNull },
            { (int)Packet.ID.CUSTOM, _receiver.ReceiveCustom },
        };

        socket = new TcpClient
        {
            ReceiveBufferSize = dataBufferSize,
            SendBufferSize = dataBufferSize
        };
        receiveBuffer = new byte[dataBufferSize];
        IAsyncResult? result = null;
        bool waiting = false;
        try
        {
            result = socket.BeginConnect(settings.ip, settings.port, ConnectCallback, socket);
            waiting = result.AsyncWaitHandle.WaitOne(connectTimeout, false);
        } catch (Exception)
        {
            _connecting = false;
            RealtimeNetworking.Connection(false);
            return;
        }
        if (!waiting || !socket.Connected)
        {
            _connecting = false;
            RealtimeNetworking.Connection(false);
            return;
        }
    }
    private void ConnectCallback(IAsyncResult result)
    {
        socket.EndConnect(result);
        if (!socket.Connected)
        {
            return;
        }
        _connecting = false;
        isConnected = true;
        stream = socket.GetStream();
        receivedData = new Packet();
        _ = stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
    }
    private void ReceiveCallback(IAsyncResult result)
    {
        try
        {
            int length = stream.EndRead(result);
            if (length <= 0)
            {
                TCPDisconnect();
                return;
            }
            byte[] data = new byte[length];
            Array.Copy(receiveBuffer, data, length);
            receivedData.Reset(CheckData(data));
            _ = stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
        } catch
        {
            TCPDisconnect();
        }
    }
    private bool CheckData(byte[] _data)
    {
        int length = 0;
        receivedData.SetBytes(_data);
        if (receivedData.UnreadLength() >= 4)
        {
            length = receivedData.ReadInt();
            if (length <= 0)
            {
                return true;
            }
        }
        while (length > 0 && length <= receivedData.UnreadLength())
        {
            byte[] _packetBytes = receivedData.ReadBytes(length);
            using (Packet _packet = new(_packetBytes))
            {
                int id = _packet.ReadInt();
                packetHandlers[id](_packet);
            }
            length = 0;
            if (receivedData.UnreadLength() >= 4)
            {
                length = receivedData.ReadInt();
                if (length <= 0)
                {
                    return true;
                }
            }
        }
        return length <= 1;
    }
    public void SendData(Packet _packet)
    {
        try
        {
            if (socket != null)
            {
                _ = stream.BeginWrite(_packet.ToArray(), 0, _packet.Length(), null, null);
            }
        } catch (Exception ex)
        {
            Console.WriteLine($"Error sending data to server via TCP: {ex}");
        }
    }
    private void TCPDisconnect()
    {
        Disconnect();
        stream = null;
        receivedData = null;
        receiveBuffer = null;
        socket = null;
    }
    private void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            socket?.Close();
        }
    }

    public void ConnectionResponse(bool result, int id, string token1, string token2)
    {
        this.id = id;
        sendToken = token1;
        receiveToken = token2;
        RealtimeNetworking.Connection(true);
    }

}