using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Numerics;
using System.Text;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Serialization;

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

    public string sendToken { get; private set; } = "xxxxx";

    public string receiveToken { get; private set; } = "xxxxx";

    public bool isConnected { get; private set; } = false;
    private delegate void PacketHandler(Packet _packet);
    private Dictionary<int, PacketHandler> packetHandlers;
    private bool _connecting = false;
    private bool _initialized = false;
    public Settings settings => new();
    private readonly Receiver _receiver;
    private readonly RealtimeNetworking _realtimeNetworking;
    public Client(RealtimeNetworking realtimeNetworking)
    {
        _initialized = true;
        _receiver = new(this, realtimeNetworking!);
        _realtimeNetworking = realtimeNetworking;
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
            _realtimeNetworking.Connection(false);
            return;
        }
        if (!waiting || !socket.Connected)
        {
            _connecting = false;
            _realtimeNetworking.Connection(false);
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
        _realtimeNetworking.Connection(true);
    }

}
public class Packet : IDisposable
{
    public enum ID
    {
        INITIALIZATION = 1, NULL = 2, CUSTOM = 3, STRING = 4, BOOLEAN = 5, FLOAT = 6, LONG = 7, SHORT = 8, VECTOR3 = 9, QUATERNION = 10, BYTE = 11, BYTES = 12, INTEGER = 13
    }

    private List<byte>? buffer;
    private byte[]? readableBuffer;
    private int readPos;

    public void SetID(int id)
    {
        buffer.InsertRange(0, BitConverter.GetBytes(id));
    }

    /// <summary>Creates a new empty packet (without an ID).</summary>
    public Packet()
    {
        buffer = new List<byte>(); // Initialize buffer
        readPos = 0; // Set readPos to 0
    }

    /// <summary>Creates a new packet with a given ID. Used for sending.</summary>
    /// <param name="_id">The packet ID.</param>
    public Packet(int _id)
    {
        buffer = new List<byte>(); // Initialize buffer
        readPos = 0; // Set readPos to 0
        Write(_id); // Write packet id to the buffer
    }

    /// <summary>Creates a packet from which data can be read. Used for receiving.</summary>
    /// <param name="_data">The bytes to add to the packet.</param>
    public Packet(byte[] _data)
    {
        buffer = new List<byte>(); // Initialize buffer
        readPos = 0; // Set readPos to 0
        SetBytes(_data);
    }

    #region Functions
    /// <summary>Sets the packet's content and prepares it to be read.</summary>
    /// <param name="_data">The bytes to add to the packet.</param>
    public void SetBytes(byte[] _data)
    {
        Write(_data);
        readableBuffer = buffer.ToArray();
    }

    /// <summary>Inserts the length of the packet's content at the start of the buffer.</summary>
    public void WriteLength()
    {
        buffer.InsertRange(0, BitConverter.GetBytes(buffer.Count)); // Insert the byte length of the packet at the very beginning
    }

    /// <summary>Inserts the given int at the start of the buffer.</summary>
    /// <param name="_value">The int to insert.</param>
    public void InsertInt(int _value)
    {
        buffer.InsertRange(0, BitConverter.GetBytes(_value)); // Insert the int at the start of the buffer
    }

    /// <summary>Gets the packet's content in array form.</summary>
    public byte[] ToArray()
    {
        readableBuffer = buffer.ToArray();
        return readableBuffer;
    }

    /// <summary>Gets the length of the packet's content.</summary>
    public int Length()
    {
        return buffer.Count; // Return the length of buffer
    }

    /// <summary>Gets the length of the unread data contained in the packet.</summary>
    public int UnreadLength()
    {
        return Length() - readPos; // Return the remaining length (unread)
    }

    /// <summary>Resets the packet instance to allow it to be reused.</summary>
    /// <param name="_shouldReset">Whether or not to reset the packet.</param>
    public void Reset(bool _shouldReset = true)
    {
        if (_shouldReset)
        {
            buffer.Clear(); // Clear buffer
            readableBuffer = null;
            readPos = 0; // Reset readPos
        } else
        {
            readPos -= 4; // "Unread" the last read int
        }
    }
    #endregion

    #region Write Data
    /// <summary>Adds a byte to the packet.</summary>
    /// <param name="_value">The byte to add.</param>
    public void Write(byte _value)
    {
        buffer.Add(_value);
    }

    /// <summary>Adds an array of bytes to the packet.</summary>
    /// <param name="_value">The byte array to add.</param>
    public void Write(byte[] _value)
    {
        buffer.AddRange(_value);
    }

    /// <summary>Adds a short to the packet.</summary>
    /// <param name="_value">The short to add.</param>
    public void Write(short _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    /// <summary>Adds an int to the packet.</summary>
    /// <param name="_value">The int to add.</param>
    public void Write(int _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    /// <summary>Adds a long to the packet.</summary>
    /// <param name="_value">The long to add.</param>
    public void Write(long _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    /// <summary>Adds a float to the packet.</summary>
    /// <param name="_value">The float to add.</param>
    public void Write(float _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    /// <summary>Adds a double to the packet.</summary>
    /// <param name="_value">The double to add.</param>
    public void Write(double _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    /// <summary>Adds a bool to the packet.</summary>
    /// <param name="_value">The bool to add.</param>
    public void Write(bool _value)
    {
        buffer.AddRange(BitConverter.GetBytes(_value));
    }

    /// <summary>Adds a string to the packet.</summary>
    /// <param name="_value">The string to add.</param>
    public void Write(string _value)
    {
        Write(_value.Length); // Add the length of the string to the packet
        buffer.AddRange(Encoding.ASCII.GetBytes(_value)); // Add the string itself
    }

    /// <summary>Adds a Vector3 to the packet.</summary>
    /// <param name="_value">The Vector3 to add.</param>
    public void Write(Vector3 _value)
    {
        Write(_value.X);
        Write(_value.Y);
        Write(_value.Z);
    }

    /// <summary>Adds a Quaternion to the packet.</summary>
    /// <param name="_value">The Quaternion to add.</param>
    public void Write(Quaternion _value)
    {
        Write(_value.X);
        Write(_value.Y);
        Write(_value.Z);
        Write(_value.W);
    }
    #endregion

    #region Read Data
    /// <summary>Reads a byte from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public byte ReadByte(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            byte _value = readableBuffer[readPos]; // Get the byte at readPos' position
            if (_moveReadPos)
            {
                // If _moveReadPos is true
                readPos += 1; // Increase readPos by 1
            }
            return _value; // Return the byte
        } else
        {
            throw new Exception("Could not read value of type 'byte'!");
        }
    }

    /// <summary>Reads an array of bytes from the packet.</summary>
    /// <param name="_length">The length of the byte array.</param>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public byte[] ReadBytes(int _length, bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            byte[] _value = buffer.GetRange(readPos, _length).ToArray(); // Get the bytes at readPos' position with a range of _length
            if (_moveReadPos)
            {
                // If _moveReadPos is true
                readPos += _length; // Increase readPos by _length
            }
            return _value; // Return the bytes
        } else
        {
            throw new Exception("Could not read value of type 'byte[]'!");
        }
    }

    /// <summary>Reads a short from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public short ReadShort(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            short _value = BitConverter.ToInt16(readableBuffer, readPos); // Convert the bytes to a short
            if (_moveReadPos)
            {
                // If _moveReadPos is true and there are unread bytes
                readPos += 2; // Increase readPos by 2
            }
            return _value; // Return the short
        } else
        {
            throw new Exception("Could not read value of type 'short'!");
        }
    }

    /// <summary>Reads an int from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public int ReadInt(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            int _value = BitConverter.ToInt32(readableBuffer, readPos); // Convert the bytes to an int
            if (_moveReadPos)
            {
                // If _moveReadPos is true
                readPos += 4; // Increase readPos by 4
            }
            return _value; // Return the int
        } else
        {
            throw new Exception("Could not read value of type 'int'!");
        }
    }

    /// <summary>Reads a long from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public long ReadLong(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            long _value = BitConverter.ToInt64(readableBuffer, readPos); // Convert the bytes to a long
            if (_moveReadPos)
            {
                // If _moveReadPos is true
                readPos += 8; // Increase readPos by 8
            }
            return _value; // Return the long
        } else
        {
            throw new Exception("Could not read value of type 'long'!");
        }
    }

    /// <summary>Reads a float from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public float ReadFloat(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            float _value = BitConverter.ToSingle(readableBuffer, readPos); // Convert the bytes to a float
            if (_moveReadPos)
            {
                // If _moveReadPos is true
                readPos += 4; // Increase readPos by 4
            }
            return _value; // Return the float
        } else
        {
            throw new Exception("Could not read value of type 'float'!");
        }
    }

    /// <summary>Reads a double from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public double ReadDouble(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            double _value = BitConverter.ToSingle(readableBuffer, readPos); // Convert the bytes to a double
            if (_moveReadPos)
            {
                // If _moveReadPos is true
                readPos += 8; // Increase readPos by 8
            }
            return _value; // Return the double
        } else
        {
            throw new Exception("Could not read value of type 'float'!");
        }
    }

    /// <summary>Reads a bool from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public bool ReadBool(bool _moveReadPos = true)
    {
        if (buffer.Count > readPos)
        {
            // If there are unread bytes
            bool _value = BitConverter.ToBoolean(readableBuffer, readPos); // Convert the bytes to a bool
            if (_moveReadPos)
            {
                // If _moveReadPos is true
                readPos += 1; // Increase readPos by 1
            }
            return _value; // Return the bool
        } else
        {
            throw new Exception("Could not read value of type 'bool'!");
        }
    }

    /// <summary>Reads a string from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public string ReadString(bool _moveReadPos = true)
    {
        try
        {
            int _length = ReadInt(); // Get the length of the string
            string _value = Encoding.ASCII.GetString(readableBuffer, readPos, _length); // Convert the bytes to a string
            if (_moveReadPos && _value.Length > 0)
            {
                // If _moveReadPos is true string is not empty
                readPos += _length; // Increase readPos by the length of the string
            }
            return _value; // Return the string
        } catch
        {
            throw new Exception("Could not read value of type 'string'!");
        }
    }

    /// <summary>Reads a Vector3 from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public Vector3 ReadVector3(bool _moveReadPos = true)
    {
        return new Vector3(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
    }

    /// <summary>Reads a Quaternion from the packet.</summary>
    /// <param name="_moveReadPos">Whether or not to move the buffer's read position.</param>
    public Quaternion ReadQuaternion(bool _moveReadPos = true)
    {
        return new Quaternion(ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos), ReadFloat(_moveReadPos));
    }
    #endregion

    private bool disposed = false;

    protected virtual void Dispose(bool _disposing)
    {
        if (!disposed)
        {
            if (_disposing)
            {
                buffer = null;
                readableBuffer = null;
                readPos = 0;
            }
            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

}
public class RealtimeNetworking
{

    #region Events
    public event NoCallback OnDisconnectedFromServer;
    public event ActionCallback OnConnectingToServerResult;
    public event PacketCallback OnPacketReceived;
    public event NullCallback OnEmptyReceived;
    public event StringCallback OnStringReceived;
    public event IntegerCallback OnIntegerReceived;
    public event BooleanCallback OnBooleanReceived;
    public event FloatCallback OnFloatReceived;
    public event ShortCallback OnShortReceived;
    public event LongCallback OnLongReceived;
    public event Vector3Callback OnVector3Received;
    public event QuaternionCallback OnQuaternionReceived;
    public event ByteCallback OnByteReceived;
    public event BytesCallback OnByteArrayReceived;
    public delegate void ActionCallback(bool successful);
    public delegate void NoCallback();
    public delegate void PacketCallback(Packet packet);
    public delegate void NullCallback(int id);
    public delegate void StringCallback(int id, string value);
    public delegate void IntegerCallback(int id, int value);
    public delegate void BooleanCallback(int id, bool value);
    public delegate void FloatCallback(int id, float value);
    public delegate void ShortCallback(int id, short value);
    public delegate void LongCallback(int id, long value);
    public delegate void Vector3Callback(int id, Vector3 value);
    public delegate void QuaternionCallback(int id, Quaternion value);
    public delegate void ByteCallback(int id, byte value);
    public delegate void BytesCallback(int id, byte[] value);
    #endregion

    private Client _client;
    public RealtimeNetworking(Client client)
    {
        _client = client;
    }
    public void Connect()
    {
        _client.ConnectToServer();
    }

    public void Connection(bool result) => OnConnectingToServerResult?.Invoke(result);
    public void Disconnected() => OnDisconnectedFromServer?.Invoke();
    public void ReceivePacket(Packet packet) => OnPacketReceived?.Invoke(packet);
    public void ReceiveNull(int id) => OnEmptyReceived?.Invoke(id);
    public void ReceiveString(int id, string value) => OnStringReceived?.Invoke(id, value);
    public void ReceiveInteger(int id, int value) => OnIntegerReceived?.Invoke(id, value);
    public void ReceiveFloat(int id, float value) => OnFloatReceived?.Invoke(id, value);
    public void ReceiveBoolean(int id, bool value) => OnBooleanReceived?.Invoke(id, value);
    public void ReceiveShort(int id, short value) => OnShortReceived?.Invoke(id, value);
    public void ReceiveLong(int id, long value) => OnLongReceived?.Invoke(id, value);
    public void ReceiveVector3(int id, Vector3 value) => OnVector3Received?.Invoke(id, value);
    public void ReceiveQuaternion(int id, Quaternion value) => OnQuaternionReceived?.Invoke(id, value);
    public void ReceiveByte(int id, byte value) => OnByteReceived?.Invoke(id, value);
    public void ReceiveBytes(int id, byte[] value) => OnByteArrayReceived?.Invoke(id, value);

    public void SetClient(Client client)
    {
        _client = client;
    }
}

public class Receiver
{
    private readonly Client _client;
    private readonly RealtimeNetworking _realtimeNetworking;

    public Receiver(Client client, RealtimeNetworking realtimeNetworking)
    {
        _client = client;
        _realtimeNetworking = realtimeNetworking;
    }

    public void Initialization(Packet packet)
    {
        int id = packet.ReadInt();
        string receiveToken = packet.ReadString();
        string sendToken = Tools.GenerateToken();
        _client.ConnectionResponse(true, id, sendToken, receiveToken);
        using (Packet response = new((int)Packet.ID.INITIALIZATION))
        {
            response.Write(receiveToken);
            response.WriteLength();
            _client.SendData(response);
        }
    }

    public void ReceiveNull(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            _realtimeNetworking.ReceiveNull(packetID);
        }
    }

    public void ReceiveCustom(Packet packet)
    {
        if (packet != null)
        {
            _realtimeNetworking.ReceivePacket(packet);
        }
    }

    public void ReceiveString(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            string data = packet.ReadString();
            _realtimeNetworking.ReceiveString(packetID, data);
        }
    }

    public void ReceiveInteger(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            int data = packet.ReadInt();
            _realtimeNetworking.ReceiveInteger(packetID, data);
        }
    }

    public void ReceiveBoolean(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            bool data = packet.ReadBool();
            _realtimeNetworking.ReceiveBoolean(packetID, data);
        }
    }

    public void ReceiveFloat(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            float data = packet.ReadFloat();
            _realtimeNetworking.ReceiveFloat(packetID, data);
        }
    }

    public void ReceiveShort(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            short data = packet.ReadShort();
            _realtimeNetworking.ReceiveShort(packetID, data);
        }
    }

    public void ReceiveLong(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            long data = packet.ReadLong();
            _realtimeNetworking.ReceiveLong(packetID, data);
        }
    }

    public void ReceiveVector3(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            Vector3 data = packet.ReadVector3();
            _realtimeNetworking.ReceiveVector3(packetID, new Vector3(data.X, data.Y, data.Z));
        }
    }

    public void ReceiveQuaternion(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            Quaternion data = packet.ReadQuaternion();
            _realtimeNetworking.ReceiveQuaternion(packetID, new Quaternion(data.X, data.Y, data.Z, data.W));
        }
    }

    public void ReceiveByte(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            byte data = packet.ReadByte();
            _realtimeNetworking.ReceiveByte(packetID, data);
        }
    }

    public void ReceiveBytes(Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            int bytesLenght = packet.ReadInt();
            byte[] data = packet.ReadBytes(bytesLenght);
            _realtimeNetworking.ReceiveBytes(packetID, data);
        }
    }

}
public class Sender
{

    private readonly Client _client;

    public Sender(Client client)
    {
        _client = client;
    }
    private void SendPacket(Packet _packet)
    {
        _packet.WriteLength();
        _client.SendData(_packet);
    }
    public void Send(int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendPacket(packet);
    }

    public void Send(Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendPacket(packet);
        }
    }

    public void Send(int packetID, string data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendPacket(packet);
        }
    }

    public void Send(int packetID, byte[] data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendPacket(packet);
        }
    }

    public void Send(int packetID, byte data)
    {
        using Packet packet = new((int)Packet.ID.BYTE);
        packet.Write(packetID);
        packet.Write(data);
        SendPacket(packet);
    }

    public void Send(int packetID, int data)
    {
        using Packet packet = new((int)Packet.ID.INTEGER);
        packet.Write(packetID);
        packet.Write(data);
        SendPacket(packet);
    }

    public void Send(int packetID, bool data)
    {
        using Packet packet = new((int)Packet.ID.BOOLEAN);
        packet.Write(packetID);
        packet.Write(data);
        SendPacket(packet);
    }

    public void Send(int packetID, float data)
    {
        using Packet packet = new((int)Packet.ID.FLOAT);
        packet.Write(packetID);
        packet.Write(data);
        SendPacket(packet);
    }

    public void Send(int packetID, short data)
    {
        using Packet packet = new((int)Packet.ID.SHORT);
        packet.Write(packetID);
        packet.Write(data);
        SendPacket(packet);
    }

    public void Send(int packetID, long data)
    {
        using Packet packet = new((int)Packet.ID.LONG);
        packet.Write(packetID);
        packet.Write(data);
        SendPacket(packet);
    }

    public void Send(int packetID, Vector3 data)
    {
        using Packet packet = new((int)Packet.ID.VECTOR3);
        packet.Write(packetID);
        packet.Write(new Vector3(data.X, data.Y, data.Z));
        SendPacket(packet);
    }

    public void Send(int packetID, Quaternion data)
    {
        using Packet packet = new((int)Packet.ID.QUATERNION);
        packet.Write(packetID);
        packet.Write(new Quaternion(data.X, data.Y, data.Z, data.W));
        SendPacket(packet);
    }
}
public class Settings
{

    public string ip = "127.0.0.1";
    public int port = 5555;

}
public static class Tools
{

    public static string GenerateToken()
    {
        return Path.GetRandomFileName().Remove(8, 1);
    }

    public static T CloneClass<T>(this T target)
    {
        return target.Serialize().Desrialize<T>();
    }

    public static void CopyTo(Stream source, Stream target)
    {
        byte[] bytes = new byte[4096]; int count;
        while ((count = source.Read(bytes, 0, bytes.Length)) != 0)
        {
            target.Write(bytes, 0, count);
        }
    }

    #region Encryption
    public static string EncrypteToMD5(string data)
    {
        UTF8Encoding ue = new();
        byte[] bytes = ue.GetBytes(data);
        MD5CryptoServiceProvider md5 = new();
        byte[] hashBytes = md5.ComputeHash(bytes);
        string hashString = "";
        for (int i = 0; i < hashBytes.Length; i++)
        {
            hashString += Convert.ToString(hashBytes[i], 16).PadLeft(2, '0');
        }
        return hashString.PadLeft(32, '0');
    }
    #endregion

    #region Serialization
    public static string Serialize<T>(this T target)
    {
        XmlSerializer xml = new(typeof(T));
        StringWriter writer = new();
        xml.Serialize(writer, target);
        return writer.ToString();
    }

    public static T Desrialize<T>(this string target)
    {
        XmlSerializer xml = new(typeof(T));
        StringReader reader = new(target);
        return (T)xml.Deserialize(reader);
    }

    public static async Task<string> SerializeAsync<T>(this T target)
    {
        Task<string> task = Task.Run(() =>
        {
            XmlSerializer xml = new(typeof(T));
            StringWriter writer = new();
            xml.Serialize(writer, target);
            return writer.ToString();
        });
        return await task;
    }

    public static async Task<T> DesrializeAsync<T>(this string target)
    {
        Task<T> task = Task.Run(() =>
        {
            XmlSerializer xml = new(typeof(T));
            StringReader reader = new(target);
            return (T)xml.Deserialize(reader);
        });
        return await task;
    }
    #endregion

    #region Compression
    public static async Task<byte[]> CompressAsync(string target)
    {
        Task<byte[]> task = Task.Run(() =>
        {
            return Compress(target);
        });
        return await task;
    }

    public static byte[] Compress(string target)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(target);
        using MemoryStream msi = new(bytes);
        using MemoryStream mso = new();
        using (GZipStream gs = new(mso, CompressionMode.Compress))
        {
            CopyTo(msi, gs);
        }
        return mso.ToArray();
    }

    public static async Task<string> DecompressAsync(byte[] bytes)
    {
        Task<string> task = Task.Run(() =>
        {
            return Decompress(bytes);
        });
        return await task;
    }

    public static string Decompress(byte[] bytes)
    {
        using MemoryStream msi = new(bytes);
        using MemoryStream mso = new();
        using (GZipStream gs = new(msi, CompressionMode.Decompress))
        {
            CopyTo(gs, mso);
        }
        return Encoding.UTF8.GetString(mso.ToArray());
    }
    #endregion

}