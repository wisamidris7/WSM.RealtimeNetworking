using System.Net;
using System.Numerics;

namespace WSM.ClientRealtime.Scripts;
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