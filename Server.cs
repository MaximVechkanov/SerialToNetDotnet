using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SerialToNetDotnet
{
    internal enum TelnetCmd : byte
    {
        IAC = 0xFF,
        WILL = 0xFB,
        WONT = 0xFC,
        DO = 0xFD,
        DONT = 0xFE,

    }

    class Server
    {
        private readonly int m_port;
        private Socket m_serverSocket;
        // TODO List<Clients>
        private Dictionary<Socket, Client> m_clients;
        private const uint m_bufLen = 32;
        private byte[] m_rxBuffer;
        private readonly string m_portName;
        public delegate void DataReceivedHandler(byte[] buffer, int numBytes);
        public event DataReceivedHandler DataReceived;
        private readonly List<char> m_skippedChars;
        private readonly TerminalType m_terminalType;

        public Server(TerminalType termType, int port, string comPortName, List<char> skippedChars)
        {
            this.m_portName = comPortName;
            this.m_port = port;
            this.m_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.m_clients = new Dictionary<Socket, Client>();

            this.m_rxBuffer = new byte[m_bufLen];

            this.m_skippedChars = skippedChars;
            this.m_terminalType = termType;
        }

        public void Start()
        {
            m_serverSocket.Bind(new IPEndPoint(IPAddress.Any, this.m_port));
            m_serverSocket.Listen(0);
            m_serverSocket.BeginAccept(new AsyncCallback(IncomeConnectionCallback), m_serverSocket);

            Console.WriteLine("Server started on port {0} ({1})", this.m_port, m_portName);
        }

        public void Stop()
        {
            m_serverSocket.Close();
        }

        private void IncomeConnectionCallback(IAsyncResult result)
        {
            Socket tmpSocket = (Socket)result.AsyncState;

            Socket clientSocket = tmpSocket.EndAccept(result);

            uint clientID = (uint)m_clients.Count + 1;
            IPEndPoint remoteEp = (IPEndPoint)clientSocket.RemoteEndPoint;
            Client client = new Client(clientID, remoteEp, this);

            SendWelcomeMessage(clientSocket);

            m_clients.Add(clientSocket, client);

            Console.WriteLine("Server at {0} ({1}): client connected from {2}", m_port, m_portName, remoteEp.ToString());

            clientSocket.BeginReceive(m_rxBuffer, 0, 1, SocketFlags.None, new AsyncCallback(ReceiveDataCallback), clientSocket);
            m_serverSocket.BeginAccept(new AsyncCallback(IncomeConnectionCallback), m_serverSocket);
        }

        private void SendWelcomeMessage(Socket clientSocket)
        {
            if (m_terminalType == TerminalType.telnet)
            {
                SendBytesToSocket(
                    clientSocket,
                    new byte[] {
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.DO,   0x01,   // Don't Echo
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.DO,   0x21,   // Do Remote Flow Control
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.WILL, 0x01,   // Will Echo
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.WILL, 0x03    // Will Supress Go Ahead
                    }
                );
            }

            SendStringToSocket(
                clientSocket,
                "You connected to COM-to-telnet on port " + m_port.ToString() + ", COM port " + m_portName + "\r\n"
                );

            if (m_clients.Count != 0)
            {
                SendStringToSocket(clientSocket, "Already connected clients:\r\n");

                SendStringToSocket(clientSocket, getClientsString());
            }

            SendStringToSocket(clientSocket, "\r\n");
            AskForSignature(clientSocket);
        }

        private void AskForSignature(Socket sock)
        {
            SendStringToSocket(sock, "Please enter your short signature: ");
        }

        public string getClientsString()
        {
            if (m_clients.Count == 0)
                return "No clients connected";

            string res = "";
            foreach (Socket s in m_clients.Keys)
            {
                Client c = m_clients[s];
                res += String.Format("    {0} from {1}\r\n", c.m_signature, s.RemoteEndPoint.ToString());
            }

            return res;
        }

        private void SendBytesToSocket(Socket sock, byte[] data)
        {
            sock.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendDataCallback), sock);
        }

        private void SendDataCallback(IAsyncResult result)
        {
            Socket clientSocket = (Socket)result.AsyncState;

            clientSocket.EndSend(result);
        }

        private void ReceiveDataCallback(IAsyncResult result)
        {
            try
            {
                Socket clientSocket = (Socket)result.AsyncState;
                Client client = GetClientBySocket(clientSocket);

                int bytesReceived = clientSocket.EndReceive(result);

                if (bytesReceived == 0)
                {
                    m_clients.Remove(clientSocket);
                    clientSocket.Close();
                }

                // received data (byte) is in m_rxBuffer[0];

                if (client.m_state == Client.State.iac)
                {
                    client.AddIacByte(m_rxBuffer[0]);
                }
                else
                {
                    // Process IAC (Interpret as command) symbol in case of telnet
                    if ((m_rxBuffer[0] == (byte)TelnetCmd.IAC) &&
                        (m_terminalType == TerminalType.telnet))
                    {
                        client.ToIacState();
                    }
                    else
                    {
                        if (client.m_state == Client.State.signing)
                        {
                            // Echo back the input symbol
                            SendBytesToSocket(clientSocket, new byte[] { m_rxBuffer[0] });
                            var res = client.AddSignatureChar(m_rxBuffer[0]);

                            // If an empty signature provided - ask again
                            if (res == Client.SignatureAppendResult.empty)
                            {
                                AskForSignature(clientSocket);
                            }
                            else if (res == Client.SignatureAppendResult.finished)
                            {
                                SendStringToSocket(clientSocket, "Thanks\r\n");
                            }
                        }
                        else
                        {
                            // Normal byte in normal state
                            if (!m_skippedChars.Contains((char)m_rxBuffer[0]))
                                DataReceived(m_rxBuffer, bytesReceived);
                        }
                    }
                }

                clientSocket.BeginReceive(m_rxBuffer, 0, 1, SocketFlags.None, new AsyncCallback(ReceiveDataCallback), clientSocket);
            }
            catch { }
        }

        private Client GetClientBySocket(Socket clientSocket)
        {
            if (!m_clients.TryGetValue(clientSocket, out Client c))
                c = null;

            return c;
        }

        public void SendBytesToAll(byte[] data)
        {
            List<Socket> socksToClose = new List<Socket>();

            foreach (Socket sock in m_clients.Keys)
            {
                // Do not broadcast to a signing client
                if (m_clients[sock].m_state != Client.State.signing)
                {
                    try
                    {
                        SendBytesToSocket(sock, data);
                    }
                    catch
                    {
                        socksToClose.Add(sock);
                    }
                }
            }

            // Remove all erroneous sockets after iteration, cause cannot do it inside foreach
            foreach (Socket sock in socksToClose)
            {
                sock.Close();
                m_clients.Remove(sock);
            }
        }

        private void SendStringToSocket(Socket socket, string msg)
        {
            byte[] data = Encoding.ASCII.GetBytes(msg);
            SendBytesToSocket(socket, data);
        }
    }
}
