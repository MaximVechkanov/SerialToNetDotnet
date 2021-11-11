using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Serialization.NamingConventions;

namespace SerialToNetDotnet
{
    class PortCfg
    {
        public string serial_port { get; set; }
        public int baudrate { get; set; }
        public int tcp_port { get; set; }
        public int databits { get; set; }
        public int stopbits { get; set; }
        public string terminal_type { get; set; }
        public string parity { get; set; }
    }

    class Configuration
    {
        public List<char> skip_chars;
        public List<PortCfg> links { get; set; }
    }

    class Program
    {
        private static List<Exposer> exposers;

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

            Configuration config;

            exposers = new List<Exposer>();
            try
            {
                config = deserializer.Deserialize<Configuration>(File.ReadAllText(fileName));
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("Failed to read config. {0}", e.Message);
                return;
            }


            foreach (var link in config.links)
            {
                var terminalTypeTmp = Exposer.TerminalType.unknown;

                try
                {
                    terminalTypeTmp = (Exposer.TerminalType)Enum.Parse(typeof(Exposer.TerminalType), link.terminal_type);
                }
                catch
                {
                    Console.Error.WriteLine("Failed to parse terminal_type ({1}) for link ({0}, {2})", link.serial_port, link.terminal_type, link.tcp_port);
                }

                try
                {
                    var parity = (System.IO.Ports.Parity)Enum.Parse(typeof(System.IO.Ports.Parity), link.parity, true);
                    Exposer.Config srvCfg = new Exposer.Config
                    {
                        baudRate = link.baudrate,
                        portName = link.serial_port,
                        tcpPort = link.tcp_port,
                        skipChars = config.skip_chars,
                        databits = link.databits,
                        stopbits = link.stopbits,
                        terminalType = terminalTypeTmp,
                        parity = parity,
                    };

                    exposers.Add(new Exposer(srvCfg));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Failed to create port mapping - check config: {0}", e);
                }
            }

            foreach (var srv in exposers)
            {
                try
                {
                    srv.Start();
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Failed to start mapper: {0}", e.Message);
                }
            }

            exposers.RemoveAll((exposer) => !exposer.m_isStarted);

            while (true)
            {
                string cmd = Console.ReadLine();

                if (cmd == "status")
                {
                    foreach (var server in exposers)
                    {
                        Console.WriteLine(server.ToString());
                    }
                }
            }
        }
    }
}
