using System.Net;
using System.Net.Sockets;

namespace WSM.ClientRealtime.Scripts;

public class Client
{

    private static readonly int dataBufferSize = 4096;
    private static readonly int connectTimeout = 5000;

    public int id { get; private set; } = 0;

    public string sendToken { get; private set; } = "xxxxx";

    public string receiveToken { get; private set; } = "xxxxx";
    public TCP tcp;
    public UDP udp;

    public bool isConnected { get; private set; } = false;
    private delegate void PacketHandler(Packet _packet);
    private static Dictionary<int, PacketHandler> packetHandlers;
    private bool _connecting = false;
    private bool _initialized = false;
    public Settings settings => new();
    public static Client instance { get; set; }
    public Client()
    {
        instance = this;
        Initialize();
    }
    private void Initialize()
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
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

        tcp = new TCP();
        udp = new UDP();

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

        tcp.Connect();
    }

    public class TCP
    {
        public TcpClient? socket;
        private NetworkStream? stream;
        private Packet? receivedData;
        private byte[]? receiveBuffer;

        public void Connect()
        {
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
                result = socket.BeginConnect(instance.settings.ip, instance.settings.port, ConnectCallback, socket);
                waiting = result.AsyncWaitHandle.WaitOne(connectTimeout, false);
            } catch (Exception)
            {
                instance._connecting = false;
                RealtimeNetworking.instance._Connection(false);
                return;
            }
            if (!waiting || !socket.Connected)
            {
                instance._connecting = false;
                RealtimeNetworking.instance._Connection(false);
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
            instance._connecting = false;
            instance.isConnected = true;
            stream = socket.GetStream();
            receivedData = new Packet();
            _ = stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
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

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                int length = stream.EndRead(result);
                if (length <= 0)
                {
                    instance.Disconnect();
                    return;
                }
                byte[] data = new byte[length];
                Array.Copy(receiveBuffer, data, length);
                receivedData.Reset(CheckData(data));
                _ = stream.BeginRead(receiveBuffer, 0, dataBufferSize, ReceiveCallback, null);
            } catch
            {
                Disconnect();
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

        private void Disconnect()
        {
            instance.Disconnect();
            stream = null;
            receivedData = null;
            receiveBuffer = null;
            socket = null;
        }

    }

    public class UDP
    {
        public UdpClient? socket;
        public IPEndPoint? endPoint;

        public UDP()
        {
            endPoint = new IPEndPoint(IPAddress.Parse(instance.settings.ip), instance.settings.port);
        }

        public void Connect(int port)
        {
            socket = new UdpClient(port);
            socket.Connect(endPoint);
            _ = socket.BeginReceive(ReceiveCallback, null);
            using Packet _packet = new();
            SendData(_packet);
        }

        public void SendData(Packet _packet)
        {
            try
            {
                _packet.InsertInt(instance.id);
                _ = (socket?.BeginSend(_packet.ToArray(), _packet.Length(), null, null));
            } catch (Exception ex)
            {
                Console.WriteLine($"Error sending data to server via UDP: {ex}");
            }
        }

        private void ReceiveCallback(IAsyncResult result)
        {
            try
            {
                byte[] data = socket.EndReceive(result, ref endPoint);
                _ = socket.BeginReceive(ReceiveCallback, null);
                if (data.Length < 4)
                {
                    instance.Disconnect();
                    return;
                }
                CheckData(data);
            } catch
            {
                Disconnect();
            }
        }

        private void CheckData(byte[] data)
        {
            using (Packet _packet = new(data))
            {
                int length = _packet.ReadInt();
                data = _packet.ReadBytes(length);
            }
            using (Packet _packet = new(data))
            {
                int _packetId = _packet.ReadInt();
                packetHandlers[_packetId](_packet);
            }
        }

        private void Disconnect()
        {
            instance.Disconnect();
            endPoint = null;
            socket = null;
        }
    }

    private void Disconnect()
    {
        if (isConnected)
        {
            isConnected = false;
            tcp.socket?.Close();
            udp.socket?.Close();
        }
    }

    public void ConnectionResponse(bool result, int id, string token1, string token2)
    {
        this.id = id;
        sendToken = token1;
        receiveToken = token2;
        RealtimeNetworking.instance._Connection(true);
    }

}