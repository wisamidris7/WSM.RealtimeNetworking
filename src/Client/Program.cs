using WSM.ClientRealtime;
using WSM.ClientRealtime.Scripts;

#if DEBUG
await Task.Delay(1000); // This Delay You Can Remove But This Required For Testing In Debug
#endif

var networkingInstance = new RealtimeNetworking(default!);
var client = new Client(networkingInstance);
networkingInstance.SetClient(client);
var sender = new Sender(client);
Terminal.Sender = sender;
networkingInstance.OnDisconnectedFromServer += Terminal.Disconnected;
networkingInstance.OnConnectingToServerResult += Terminal.Connected;
networkingInstance.OnPacketReceived += Terminal.PacketReceived;
networkingInstance.Connect();
Console.ReadKey();
