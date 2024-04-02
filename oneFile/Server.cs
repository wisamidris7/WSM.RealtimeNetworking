using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace WSM.ServerRealtime.Scripts;

internal class Client
{

    public static int dataBufferSize = 4096;
    public int id;
    public TCP tcp;
    public UDP udp;
    public string sendToken = "xxxxx";
    public string receiveToken = "xxxxx";

    public Client(int _clientId)
    {
        id = _clientId;
        tcp = new TCP(id);
        udp = new UDP(id);
    }

    public class TCP
    {
        public TcpClient? socket;
        private readonly int id;
        private NetworkStream? stream;
        private Packet? receivedData;
        private byte[]? receiveBuffer;

        public TCP(int _id)
        {
            id = _id;
        }

        public void Initialize(TcpClient _socket)
        {
            socket = _socket;
            socket.ReceiveBufferSize = dataBufferSize;
            socket.SendBufferSize = dataBufferSize;
            stream = socket.GetStream();
            receivedData = new Packet();
            receiveBuffer = new byte[dataBufferSize];
            _ = stream.BeginRead(receiveBuffer, 0, dataBufferSize, IncomingData, null);
            using Packet packet = new((int)Packet.ID.INITIALIZATION);
            Server.clients[id].sendToken = Tools.GenerateToken();
            packet.Write(id);
            packet.Write(Server.clients[id].sendToken);
            packet.WriteLength();
            Server.clients[id].tcp.SendData(packet);
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
                Tools.LogError(ex.Message, ex.StackTrace);
            }
        }

        private void IncomingData(IAsyncResult result)
        {
            try
            {
                int length = stream.EndRead(result);
                if (length <= 0)
                {
                    Server.clients[id].Disconnect();
                    return;
                }
                byte[] data = new byte[length];
                Array.Copy(receiveBuffer, data, length);
                receivedData.Reset(CheckData(data));
                _ = stream.BeginRead(receiveBuffer, 0, dataBufferSize, IncomingData, null);
            } catch (Exception ex)
            {
                Tools.LogError(ex.Message, ex.StackTrace);
                Server.clients[id].Disconnect();
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
                _ = Task.Run(() =>
                {
                    try
                    {
                        using Packet _packet = new(_packetBytes);
                        int _packetId = _packet.ReadInt();
                        Server.packetHandlers[_packetId](id, _packet);
                    } catch (Exception ex)
                    {
                        Tools.LogError(ex.Message, ex.StackTrace);
                    }
                });
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

        public void Disconnect()
        {
            socket.Close();
            stream = null;
            receivedData = null;
            receiveBuffer = null;
            socket = null;
        }
    }

    public class UDP
    {
        public IPEndPoint? endPoint;
        private readonly int id;

        public UDP(int _id)
        {
            id = _id;
        }

        public void Connect(IPEndPoint _endPoint)
        {
            endPoint = _endPoint;
        }

        public void SendData(Packet _packet)
        {
            Server.SendDataUDP(endPoint, _packet);
        }

        public void CheckData(Packet _packetData)
        {
            int _packetLength = _packetData.ReadInt();
            byte[] _packetBytes = _packetData.ReadBytes(_packetLength);
            _ = Task.Run(() =>
            {
                try
                {
                    using Packet _packet = new(_packetBytes);
                    int _packetId = _packet.ReadInt();
                    Server.packetHandlers[_packetId](id, _packet);
                } catch (Exception ex)
                {
                    Tools.LogError(ex.Message, ex.StackTrace);
                }
            });
        }

        public void Disconnect()
        {
            endPoint = null;
        }
    }

    private void Disconnect()
    {
        if (tcp.socket != null)
        {
            Console.WriteLine("Client with IP {0} has been disconnected.", tcp.socket.Client.RemoteEndPoint);
            IPEndPoint? ip = tcp.socket.Client.RemoteEndPoint as IPEndPoint;
            Terminal.OnClientDisconnected(id, ip.Address.ToString());
            tcp.Disconnect();
        } else
        {
            Console.WriteLine("Client with unkown IP has been disconnected.");
            Terminal.OnClientDisconnected(id, "unknown");
        }
        if (udp.endPoint != null)
        {
            udp.Disconnect();
        }
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
        buffer = []; // Initialize buffer
        readPos = 0; // Set readPos to 0
    }

    /// <summary>Creates a new packet with a given ID. Used for sending.</summary>
    /// <param name="_id">The packet ID.</param>
    public Packet(int _id)
    {
        buffer = []; // Initialize buffer
        readPos = 0; // Set readPos to 0
        Write(_id); // Write packet id to the buffer
    }

    /// <summary>Creates a packet from which data can be read. Used for receiving.</summary>
    /// <param name="_data">The bytes to add to the packet.</param>
    public Packet(byte[] _data)
    {
        buffer = []; // Initialize buffer
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
internal class Receiver
{

    public static void Initialization(int clientID, Packet packet)
    {
        string token = packet.ReadString();
        Server.clients[clientID].receiveToken = token;
    }

    public static void ReceiveNull(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            Terminal.ReceivedEvent(clientID, packetID);
        }
    }

    public static void ReceiveCustom(int clientID, Packet packet)
    {
        if (packet != null)
        {
            Terminal.ReceivedPacket(clientID, packet);
        }
    }

    public static void ReceiveString(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            string data = packet.ReadString();
            Terminal.ReceivedString(clientID, packetID, data);
        }
    }

    public static void ReceiveInteger(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            int data = packet.ReadInt();
            Terminal.ReceivedInteger(clientID, packetID, data);
        }
    }

    public static void ReceiveBoolean(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            bool data = packet.ReadBool();
            Terminal.ReceivedBoolean(clientID, packetID, data);
        }
    }

    public static void ReceiveFloat(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            float data = packet.ReadFloat();
            Terminal.ReceivedFloat(clientID, packetID, data);
        }
    }

    public static void ReceiveShort(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            short data = packet.ReadShort();
            Terminal.ReceivedShort(clientID, packetID, data);
        }
    }

    public static void ReceiveLong(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            long data = packet.ReadLong();
            Terminal.ReceivedLong(clientID, packetID, data);
        }
    }

    public static void ReceiveVector3(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            Vector3 data = packet.ReadVector3();
            Terminal.ReceivedVector3(clientID, packetID, data);
        }
    }

    public static void ReceiveQuaternion(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            Quaternion data = packet.ReadQuaternion();
            Terminal.ReceivedQuaternion(clientID, packetID, data);
        }
    }

    public static void ReceiveByte(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            byte data = packet.ReadByte();
            Terminal.ReceivedByte(clientID, packetID, data);
        }
    }

    public static void ReceiveBytes(int clientID, Packet packet)
    {
        if (packet != null)
        {
            int packetID = packet.ReadInt();
            int bytesLenght = packet.ReadInt();
            byte[] data = packet.ReadBytes(bytesLenght);
            Terminal.ReceivedBytes(clientID, packetID, data);
        }
    }

}
internal class Sender
{

    #region Core
    /// <summary>Sends a packet to a client via TCP.</summary>
    /// <param name="clientID">The client to send the packet the packet to.</param>
    /// <param name="packet">The packet to send to the client.</param>
    private static void SendTCPData(int clientID, Packet packet)
    {
        packet.WriteLength();
        Server.clients[clientID].tcp.SendData(packet);
    }

    /// <summary>Sends a packet to a client via UDP.</summary>
    /// <param name="clientID">The client to send the packet the packet to.</param>
    /// <param name="packet">The packet to send to the client.</param>
    private static void SendUDPData(int clientID, Packet packet)
    {
        packet.WriteLength();
        Server.clients[clientID].udp.SendData(packet);
    }

    /// <summary>Sends a packet to all clients via TCP.</summary>
    /// <param name="packet">The packet to send.</param>
    private static void SendTCPDataToAll(Packet packet)
    {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++)
        {
            Server.clients[i].tcp.SendData(packet);
        }
    }

    /// <summary>Sends a packet to all clients except one via TCP.</summary>
    /// <param name="exceptClientID">The client to NOT send the data to.</param>
    /// <param name="packet">The packet to send.</param>
    private static void SendTCPDataToAll(int exceptClientID, Packet packet)
    {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++)
        {
            if (i != exceptClientID)
            {
                Server.clients[i].tcp.SendData(packet);
            }
        }
    }

    /// <summary>Sends a packet to all clients via UDP.</summary>
    /// <param name="packet">The packet to send.</param>
    private static void SendUDPDataToAll(Packet packet)
    {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++)
        {
            Server.clients[i].udp.SendData(packet);
        }
    }

    /// <summary>Sends a packet to all clients except one via UDP.</summary>
    /// <param name="exceptClientID">The client to NOT send the data to.</param>
    /// <param name="packet">The packet to send.</param>
    private static void SendUDPDataToAll(int exceptClientID, Packet packet)
    {
        packet.WriteLength();
        for (int i = 1; i <= Server.MaxPlayers; i++)
        {
            if (i != exceptClientID)
            {
                Server.clients[i].udp.SendData(packet);
            }
        }
    }
    #endregion

    #region TCP
    public static void TCP_Send(int clientID, int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendTCPData(clientID, packet);
    }

    public static void TCP_SentToAll(int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendTCPDataToAll(packet);
        }
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendTCPDataToAll(excludedClientID, packet);
        }
    }

    public static void TCP_Send(int clientID, int packetID, string data)
    {
        if (data != null && clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, string data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPDataToAll(packet);
        }
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, string data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPDataToAll(excludedClientID, packet);
        }
    }

    public static void TCP_Send(int clientID, int packetID, byte data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.BYTE);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, byte data)
    {
        using Packet packet = new((int)Packet.ID.BYTE);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, byte data)
    {
        using Packet packet = new((int)Packet.ID.BYTE);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, int packetID, byte[] data)
    {
        if (data != null && clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, byte[] data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendTCPDataToAll(packet);
        }
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, byte[] data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendTCPDataToAll(excludedClientID, packet);
        }
    }

    public static void TCP_Send(int clientID, int packetID, Vector3 data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.VECTOR3);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, Vector3 data)
    {
        using Packet packet = new((int)Packet.ID.VECTOR3);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, Vector3 data)
    {
        using Packet packet = new((int)Packet.ID.VECTOR3);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, int packetID, Quaternion data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.QUATERNION);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, Quaternion data)
    {
        using Packet packet = new((int)Packet.ID.QUATERNION);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, Quaternion data)
    {
        using Packet packet = new((int)Packet.ID.QUATERNION);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, int packetID, int data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.INTEGER);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, int data)
    {
        using Packet packet = new((int)Packet.ID.INTEGER);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, int data)
    {
        using Packet packet = new((int)Packet.ID.INTEGER);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, int packetID, bool data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.BOOLEAN);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, bool data)
    {
        using Packet packet = new((int)Packet.ID.BOOLEAN);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, bool data)
    {
        using Packet packet = new((int)Packet.ID.BOOLEAN);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, int packetID, float data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.FLOAT);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, float data)
    {
        using Packet packet = new((int)Packet.ID.FLOAT);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, float data)
    {
        using Packet packet = new((int)Packet.ID.FLOAT);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, int packetID, long data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.LONG);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, long data)
    {
        using Packet packet = new((int)Packet.ID.LONG);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, long data)
    {
        using Packet packet = new((int)Packet.ID.LONG);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }

    public static void TCP_Send(int clientID, int packetID, short data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.SHORT);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(clientID, packet);
        }
    }

    public static void TCP_SentToAll(int packetID, short data)
    {
        using Packet packet = new((int)Packet.ID.SHORT);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(packet);
    }

    public static void TCP_SentToAllExeptOne(int excludedClientID, int packetID, short data)
    {
        using Packet packet = new((int)Packet.ID.SHORT);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPDataToAll(excludedClientID, packet);
    }
    #endregion

    #region UDP
    public static void UDP_Send(int clientID, int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendUDPData(clientID, packet);
    }

    public static void UDP_SentToAll(int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendUDPDataToAll(packet);
        }
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendUDPDataToAll(excludedClientID, packet);
        }
    }

    public static void UDP_Send(int clientID, int packetID, string data)
    {
        if (data != null && clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, string data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPDataToAll(packet);
        }
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, string data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPDataToAll(excludedClientID, packet);
        }
    }

    public static void UDP_Send(int clientID, int packetID, byte data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.BYTE);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, byte data)
    {
        using Packet packet = new((int)Packet.ID.BYTE);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, byte data)
    {
        using Packet packet = new((int)Packet.ID.BYTE);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, int packetID, byte[] data)
    {
        if (data != null && clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, byte[] data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendUDPDataToAll(packet);
        }
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, byte[] data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendUDPDataToAll(excludedClientID, packet);
        }
    }

    public static void UDP_Send(int clientID, int packetID, Vector3 data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.VECTOR3);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, Vector3 data)
    {
        using Packet packet = new((int)Packet.ID.VECTOR3);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, Vector3 data)
    {
        using Packet packet = new((int)Packet.ID.VECTOR3);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, int packetID, Quaternion data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.QUATERNION);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, Quaternion data)
    {
        using Packet packet = new((int)Packet.ID.QUATERNION);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, Quaternion data)
    {
        using Packet packet = new((int)Packet.ID.QUATERNION);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, int packetID, int data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.INTEGER);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, int data)
    {
        using Packet packet = new((int)Packet.ID.INTEGER);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, int data)
    {
        using Packet packet = new((int)Packet.ID.INTEGER);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, int packetID, bool data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.BOOLEAN);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, bool data)
    {
        using Packet packet = new((int)Packet.ID.BOOLEAN);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, bool data)
    {
        using Packet packet = new((int)Packet.ID.BOOLEAN);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, int packetID, float data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.FLOAT);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, float data)
    {
        using Packet packet = new((int)Packet.ID.FLOAT);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, float data)
    {
        using Packet packet = new((int)Packet.ID.FLOAT);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, int packetID, long data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.LONG);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, long data)
    {
        using Packet packet = new((int)Packet.ID.LONG);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, long data)
    {
        using Packet packet = new((int)Packet.ID.LONG);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }

    public static void UDP_Send(int clientID, int packetID, short data)
    {
        if (clientID > 0)
        {
            using Packet packet = new((int)Packet.ID.SHORT);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(clientID, packet);
        }
    }

    public static void UDP_SentToAll(int packetID, short data)
    {
        using Packet packet = new((int)Packet.ID.SHORT);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(packet);
    }

    public static void UDP_SentToAllExeptOne(int excludedClientID, int packetID, short data)
    {
        using Packet packet = new((int)Packet.ID.SHORT);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPDataToAll(excludedClientID, packet);
    }
    #endregion

}
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

internal static class Tools
{

    public static readonly string logFolderPath = "C:\\Log\\Realtime Networking\\";

    public static void LogError(string message, string trace, string folder = "")
    {
        Console.WriteLine("Error:" + "\n" + message + "\n" + trace);
        Task task = Task.Run(() =>
        {
            try
            {
                string folderPath = logFolderPath;
                if (!string.IsNullOrEmpty(folder))
                {
                    folderPath = folderPath + folder + "\\";
                }
                string path = folderPath + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss-ffff") + ".txt";
                if (!Directory.Exists(folderPath))
                {
                    _ = Directory.CreateDirectory(folderPath);
                }
                File.WriteAllText(path, message + "\n" + trace);
            } catch (Exception ex)
            {
                Console.WriteLine("Error:" + "\n" + ex.Message + "\n" + ex.StackTrace);
            }
        });
    }

    public static string GenerateToken()
    {
        return Path.GetRandomFileName().Remove(8, 1);
    }

    public static string GetIP(AddressFamily type)
    {
        IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == type)
            {
                return ip.ToString();
            }
        }
        return "0.0.0.0";
    }

    public static string GetExternalIP()
    {
        try
        {
            IPAddress ip = IPAddress.Parse(new WebClient().DownloadString("https://icanhazip.com").Replace("\\r\\n", "").Replace("\\n", "").Trim());
            return ip.ToString();
        } catch (Exception)
        {
            try
            {
                StreamReader sr = new(WebRequest.Create("https://checkip.dyndns.org").GetResponse().GetResponseStream());
                string[] ipAddress = sr.ReadToEnd().Trim().Split(':')[1][1..].Split('<');
                return ipAddress[0];
            } catch (Exception)
            {
                return "0.0.0.0";
            }
        }
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