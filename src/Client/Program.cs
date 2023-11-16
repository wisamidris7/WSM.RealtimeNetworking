using WSM.ClientRealtime.Scripts;

await Task.Delay(1000);
Client client = new();
RealtimeNetworking instance = RealtimeNetworking.instance;
RealtimeNetworking.OnDisconnectedFromServer += Disconnected;
RealtimeNetworking.OnConnectingToServerResult += ConnectResult;
RealtimeNetworking.OnPacketReceived += PacketReceived;

RealtimeNetworking.Connect();

Console.ReadKey();
void Disconnected()
{
    Console.WriteLine("Disconnected from server.");
}

void ConnectResult(bool successful)
{
    if (successful)
    {
        Console.WriteLine("Connected to server successfully.");
        Sender.TCP_Send(123, "Hello world");
    } else
    {
        Console.WriteLine("Failed to connect the server.");
    }
}

void PacketReceived(Packet packet)
{
    int code = packet.ReadInt();
    if (code == 555)
    {
        string time = packet.ReadString();
        Console.WriteLine("Server Time: " + time);
    }
}
