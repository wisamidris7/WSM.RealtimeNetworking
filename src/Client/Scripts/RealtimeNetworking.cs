using System.Numerics;

namespace WSM.ClientRealtime.Scripts;
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