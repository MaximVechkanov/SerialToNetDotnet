using System;
using System.Collections.Generic;
using System.IO.Ports;

namespace SerialToNetDotnet
{
    public enum TerminalType
    {
        raw,
        telnet,
        unknown
    }

    class Connector
    {
        public class Config
        {
            public int tcpPort { get; set; }
            public int baudRate { get; set; }
            public string portName { get; set; }
            public List<char> skipChars { get; set; }
            public int databits { get; set; }

            public int stopbits { get; set; }
            public TerminalType terminalType { get; set; }

            public Parity parity { get; set; }
        }

        private readonly Config m_config;
        private readonly Server m_server;
        private readonly SerialPort m_serial;
        public bool IsStarted { get; private set; }

        public Connector(Config config)
        {
            m_config = config;
            m_server = new Server(m_config.tcpPort, m_config.portName, config.skipChars);
            m_server.DataReceived += NetDataReceivedHandler;

            m_serial = new SerialPort();
            IsStarted = false;
        }


        public void Start()
        {

            m_serial.PortName = m_config.portName;
            m_serial.BaudRate = m_config.baudRate;
            m_serial.Parity = m_config.parity;
            m_serial.DataBits = m_config.databits;
            m_serial.StopBits = stopBitsIntToEnum(m_config.stopbits);
            m_serial.Handshake = Handshake.None;
            m_serial.DataReceived += SerialDataReceivedHandler;

            try
            {
                m_serial.Open();
            }
            catch
            {
                throw new Exception(String.Format("Cannot open port {0}", m_config.portName));
            }

            try
            {
                m_server.Start();
            }
            catch
            {
                throw new Exception("Server cannot be started");
            }

            IsStarted = true;
        }

        private void NetDataReceivedHandler(byte[] buffer, int numBytes)
        {
            m_serial.Write(buffer, 0, numBytes);
        }

        private void SerialDataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = (SerialPort)sender;
            int numRx = sp.BytesToRead;
            byte[] rxBuf = new byte[numRx];
            sp.Read(rxBuf, 0, numRx);

            m_server.SendBytesToAll(rxBuf);
        }

        public override string ToString()
        {
            string res = string.Format(
                "Serial {0}, is opened: {1}, type: {2}. Server on port {3}, clients:\r\n",
                m_config.portName,
                m_serial.IsOpen,
                m_config.terminalType.ToString(),
                m_config.tcpPort);

            res += m_server.getClientsString();

            return res;
        }

        internal static StopBits stopBitsIntToEnum(int num)
        {
            switch (num)
            {
                case 0:
                    return StopBits.None;
                case 1:
                    return StopBits.One;
                case 2:
                    return StopBits.Two;
                default:
                    throw new ArgumentException(String.Format("Cannot convert {0} to enum StopBits", num));
            }
        }
    }
}
