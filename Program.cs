using System;
using System.Collections.Generic;

namespace SerialToNetDotnet
{
    class Program
    {
        private static Dictionary <string, Exposer> exposers;

        static void Main(string[] args)
        {
            exposers = new Dictionary<string, Exposer>();

            Exposer.Config cfg = new Exposer.Config
            {
                baudRate = 115200,
                portName = "COM4",
                tcpPort = 3100
            };

            exposers.Add("COM4", new Exposer(cfg));
            exposers["COM4"].Start();

            while (true)
            {
                _ = Console.ReadLine();
            }
        }
    }
}
