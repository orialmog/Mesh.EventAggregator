using System;

namespace MeshNetworkEventAggregator.Framework.ServiceDiscovery
{
    public interface ILogger
    {
        void Info(string message);
        void Error(Exception e);
    }


    public class ConsoleLog : ILogger
    {
        public void Error(Exception e)
        {
            Console.BackgroundColor = ConsoleColor.Red;
            Console.WriteLine(e.ToString());
            Console.ForegroundColor = ConsoleColor.White;
        }

        public void Info(string message)
        {
            Console.WriteLine(message);
        }
    }
}