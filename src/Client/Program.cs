using WSM.ClientRealtime;
using WSM.ClientRealtime.Scripts;

#if DEBUG
await Task.Delay(1000); // This Delay You Can Remove But This Required For Testing In Debug
#endif

var client = new Client();
var networkingInstance = new RealtimeNetworking(client);
networkingInstance.OnDisconnectedFromServer += Terminal.Disconnected;
networkingInstance.OnConnectingToServerResult += Terminal.Connected;
networkingInstance.OnPacketReceived += Terminal.PacketReceived;
networkingInstance.Connect();

Console.ReadKey();
