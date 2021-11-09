using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace SerialToNetDotnet
{
    class Exposer
    {
        public class Config
        {
            public int tcpPort { get; set; }
            public int baudRate { get; set; }
            public string portName { get; set; }
        }

        private Config m_config;
        private readonly Server m_server;
        private readonly SerialPort m_serial;

        public Exposer(Config config)
        {
            m_config = config;
            m_server = new Server(m_config.tcpPort, m_config.portName);
            m_server.DataReceived += NetDataReceivedHandler;

            m_serial = new SerialPort();
        }


        public void Start()
        {
            // Get a list of serial port names.
            string[] ports = SerialPort.GetPortNames();

            if (ports.Contains(m_config.portName))
            {
                m_serial.PortName = m_config.portName;
                m_serial.BaudRate = m_config.baudRate;
                m_serial.Parity = Parity.None;
                m_serial.DataBits = 8;
                m_serial.StopBits = StopBits.One;
                m_serial.Handshake = Handshake.None;
                m_serial.DataReceived += SerialDataReceivedHandler;

                try
                {
                    m_serial.Open();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Failed to open port {0}: {1}", m_serial.PortName, e);
                }

                try
                {
                    m_server.Start();
                }
                catch { }
            }
            else
            {
                Console.WriteLine("Error opening port {0}: no port", m_config.portName);
            }

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
    }
}
