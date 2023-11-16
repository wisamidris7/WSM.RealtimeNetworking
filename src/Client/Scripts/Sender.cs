using System.Numerics;

namespace WSM.ClientRealtime.Scripts;
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