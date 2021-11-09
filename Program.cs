using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Core;
using YamlDotNet.Helpers;
using System.IO;

namespace SerialToNetDotnet
{
    class PortCfg
    {
        public string serial_port { get; set; }
        public int baudrate { get; set; }
        public int tcp_port { get; set; }
    }

    class Configuration
    {
        public List <PortCfg> links { get; set; }
    }

    class Program
    {
        private static List <Exposer> exposers;

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Specifiy path to config file");
                return;
            }

            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            Configuration config = deserializer.Deserialize<Configuration>(File.ReadAllText(args[0]));

            exposers = new List <Exposer>();

            foreach (var link in config.links)
            {
                Exposer.Config cfg = new Exposer.Config
                {
                    baudRate = link.baudrate,
                    portName = link.serial_port,
                    tcpPort = link.tcp_port
                };

                exposers.Add(new Exposer(cfg));
            }

            foreach (var srv in exposers)
            {
                srv.Start();
            }


            while (true)
            {
                _ = Console.ReadLine();
            }
        }
    }
}
