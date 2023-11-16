using System.Numerics;

namespace WSM.ClientRealtime.Scripts;
public class Sender
{

    #region Core
    private static void SendTCPData(Packet _packet)
    {
        _packet.WriteLength();
        Client.instance.tcp.SendData(_packet);
    }

    private static void SendUDPData(Packet _packet)
    {
        _packet.WriteLength();
        Client.instance.udp.SendData(_packet);
    }
    #endregion

    #region TCP
    public static void TCP_Send(int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendTCPData(packet);
    }

    public static void TCP_Send(Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendTCPData(packet);
        }
    }

    public static void TCP_Send(int packetID, string data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendTCPData(packet);
        }
    }

    public static void TCP_Send(int packetID, byte[] data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendTCPData(packet);
        }
    }

    public static void TCP_Send(int packetID, byte data)
    {
        using Packet packet = new((int)Packet.ID.BYTE);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPData(packet);
    }

    public static void TCP_Send(int packetID, int data)
    {
        using Packet packet = new((int)Packet.ID.INTEGER);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPData(packet);
    }

    public static void TCP_Send(int packetID, bool data)
    {
        using Packet packet = new((int)Packet.ID.BOOLEAN);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPData(packet);
    }

    public static void TCP_Send(int packetID, float data)
    {
        using Packet packet = new((int)Packet.ID.FLOAT);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPData(packet);
    }

    public static void TCP_Send(int packetID, short data)
    {
        using Packet packet = new((int)Packet.ID.SHORT);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPData(packet);
    }

    public static void TCP_Send(int packetID, long data)
    {
        using Packet packet = new((int)Packet.ID.LONG);
        packet.Write(packetID);
        packet.Write(data);
        SendTCPData(packet);
    }

    public static void TCP_Send(int packetID, Vector3 data)
    {
        using Packet packet = new((int)Packet.ID.VECTOR3);
        packet.Write(packetID);
        packet.Write(new Vector3(data.X, data.Y, data.Z));
        SendTCPData(packet);
    }

    public static void TCP_Send(int packetID, Quaternion data)
    {
        using Packet packet = new((int)Packet.ID.QUATERNION);
        packet.Write(packetID);
        packet.Write(new Quaternion(data.X, data.Y, data.Z, data.W));
        SendTCPData(packet);
    }
    #endregion

    #region UDP
    public static void UDP_Send(int packetID)
    {
        using Packet packet = new((int)Packet.ID.NULL);
        packet.Write(packetID);
        SendUDPData(packet);
    }

    public static void UDP_Send(Packet packet)
    {
        if (packet != null)
        {
            packet.SetID((int)Packet.ID.CUSTOM);
            SendUDPData(packet);
        }
    }

    public static void UDP_Send(int packetID, string data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.STRING);
            packet.Write(packetID);
            packet.Write(data);
            SendUDPData(packet);
        }
    }

    public static void UDP_Send(int packetID, byte[] data)
    {
        if (data != null)
        {
            using Packet packet = new((int)Packet.ID.BYTES);
            packet.Write(packetID);
            packet.Write(data.Length);
            packet.Write(data);
            SendUDPData(packet);
        }
    }

    public static void UDP_Send(int packetID, byte data)
    {
        using Packet packet = new((int)Packet.ID.BYTE);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPData(packet);
    }

    public static void UDP_Send(int packetID, int data)
    {
        using Packet packet = new((int)Packet.ID.INTEGER);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPData(packet);
    }

    public static void UDP_Send(int packetID, bool data)
    {
        using Packet packet = new((int)Packet.ID.BOOLEAN);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPData(packet);
    }

    public static void UDP_Send(int packetID, float data)
    {
        using Packet packet = new((int)Packet.ID.FLOAT);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPData(packet);
    }

    public static void UDP_Send(int packetID, short data)
    {
        using Packet packet = new((int)Packet.ID.SHORT);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPData(packet);
    }

    public static void UDP_Send(int packetID, long data)
    {
        using Packet packet = new((int)Packet.ID.LONG);
        packet.Write(packetID);
        packet.Write(data);
        SendUDPData(packet);
    }

    public static void UDP_Send(int packetID, Vector3 data)
    {
        using Packet packet = new((int)Packet.ID.VECTOR3);
        packet.Write(packetID);
        packet.Write(new Vector3(data.X, data.Y, data.Z));
        SendUDPData(packet);
    }

    public static void UDP_Send(int packetID, Quaternion data)
    {
        using Packet packet = new((int)Packet.ID.QUATERNION);
        packet.Write(packetID);
        packet.Write(new Quaternion(data.X, data.Y, data.Z, data.W));
        SendUDPData(packet);
    }
    #endregion

}