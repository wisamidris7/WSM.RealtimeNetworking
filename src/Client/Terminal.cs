using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WSM.ClientRealtime.Scripts;

namespace WSM.ClientRealtime;
internal class Terminal
{
    public static Sender Sender { get; set; } = default!;
    public static void Disconnected()
    {
        Console.WriteLine("Disconnected from server.");
    }

    public static void Connected(bool successful)
    {
        if (successful)
        {
            // This Is Example
            Console.WriteLine("Connected to server successfully.");
            Sender.Send(123, "Hello world");
        } else
        {
            Console.WriteLine("Failed to connect the server.");
        }
    }

    public static void PacketReceived(Packet packet)
    {
        int code = packet.ReadInt();
        if (code == 555)
        {
            string time = packet.ReadString();
            Console.WriteLine("Server Time: " + time);
        }
    }
}
