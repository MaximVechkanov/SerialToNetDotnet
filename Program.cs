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
        public List <char> skip_chars;
        public List <PortCfg> links { get; set; }
    }

    class Program
    {
        private static List <Exposer> exposers;

        static void Main(string[] args)
        {
            string fileName;
            if (args.Length == 0)
            {
                fileName = "server_config.yaml";
                Console.Error.WriteLine("Using default path to config file: {0}", fileName);
            }
            else
                fileName = args[0];

            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            Configuration config = deserializer.Deserialize<Configuration>(File.ReadAllText(fileName));

            exposers = new List <Exposer>();

            foreach (var link in config.links)
            {
                Exposer.Config srvCfg = new Exposer.Config
                {
                    baudRate = link.baudrate,
                    portName = link.serial_port,
                    tcpPort = link.tcp_port,
                    skipChars = config.skip_chars
                };

                exposers.Add(new Exposer(srvCfg));
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
