using System;
using System.Net.NetworkInformation;
using MeshNetworkEventAggregator.Framework;
using MeshNetworkEventAggregator.Framework.Interfaces;

namespace MeshNetworkEventAggregator.TestHarness
{

    //Start up two, or more instances
    class Program
    {
        public static bool RunForever = true;
        static void Main(string[] args)
        {
            
            var network = new MeshNetworkMessengerHub("LoopbackNetwork", NetworkInterfaceType.Loopback, System.Net.Sockets.AddressFamily.InterNetwork);
   
            network.Subscribe<ChatMessage>(a =>
            {
                Console.WriteLine($"{a.From} > {a.Msg}"); 
            });
          
            
            do
            {   
                Console.Write(" > ");
                var msg = Console.ReadLine();
                network.Publish(new ChatMessage() { Msg = msg, From = Environment.UserName }); 
            } while (RunForever);

        }

    }

    public class ChatMessage : IMeshNetworkMessage
    {
        public string Msg { get; set; }
        public string From { get; set; }
    }
}
