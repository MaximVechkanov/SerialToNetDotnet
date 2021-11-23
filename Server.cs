using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    public enum EchoType
    {
        none,
        device,
        server
    }

    class Server
    {
        NetServerConfig m_cfg;

        private Socket m_serverSocket;
        // TODO List<Clients>
        private Dictionary<Socket, Client> m_clients;
        private const uint m_bufLen = 16;
        private byte[] m_rxBuffer;
        private readonly string m_portName;
        public delegate void DataReceivedHandler(byte[] buffer, int numBytes);
        public event DataReceivedHandler DataReceived;

        public delegate void ClientConnectEvent();
        public event ClientConnectEvent OnFirstClientConnected;
        public event ClientConnectEvent OnLastClientDisconnected;

        private readonly char m_lineEndChar;

        public Server(NetServerConfig config, string comPortName)
        {
            this.m_portName = comPortName;
            this.m_cfg = config;
            this.m_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.m_clients = new Dictionary<Socket, Client>();

            this.m_rxBuffer = new byte[m_bufLen];

            // In telnet, "\r\n" is the correct line ending. React only om the last one in signing
            if (m_cfg.terminalType == TerminalType.telnet)
                this.m_lineEndChar = '\n';
            else if (m_cfg.terminalType == TerminalType.raw)
                this.m_lineEndChar = '\r';
        }

        public void Start()
        {
            m_serverSocket.Bind(new IPEndPoint(IPAddress.Any, m_cfg.tcpPort));
            m_serverSocket.Listen(0);
            m_serverSocket.BeginAccept(new AsyncCallback(IncomeConnectionCallback), m_serverSocket);

            Console.WriteLine("Server started on port {0} ({1})", m_cfg.tcpPort, m_portName);
        }

        public void Stop()
        {
            m_serverSocket.Close();
        }

        private void IncomeConnectionCallback(IAsyncResult result)
        {
            Socket tmpSocket = (Socket)result.AsyncState;

            Socket clientSocket = tmpSocket.EndAccept(result);

            ConnectClient(clientSocket);
            
            m_serverSocket.BeginAccept(new AsyncCallback(IncomeConnectionCallback), m_serverSocket);
        }

        private void OnClientConnectedToUnavailablePort(Socket sock)
        {
            var msg = "Failed to open serial port " + m_portName + ", please check its status";
            byte[] data = Encoding.ASCII.GetBytes(msg);

            sock.Send(data, SocketFlags.None);

            // Assuming we have not too slow network and client, so message will be delivered in 1 sec
            Thread.Sleep(1000);

            sock.Close();
        }

        private void ConnectClient(Socket clientSocket)
        {
            if (m_clients.Count == 0)
            {
                try
                {
                    OnFirstClientConnected();
                }
                catch
                {
                    Task.Run(() => { OnClientConnectedToUnavailablePort(clientSocket); } );
                    
                    return;
                }
            }

            uint clientID = (uint)m_clients.Count + 1;
            IPEndPoint remoteEp = (IPEndPoint)clientSocket.RemoteEndPoint;
            Client client = new Client(clientID, remoteEp, this);

            SendWelcomeMessage(clientSocket);

            m_clients.Add(clientSocket, client);

            Console.WriteLine("Server at {0} ({1}): client connected from {2}", m_cfg.tcpPort, m_portName, remoteEp.ToString());

            clientSocket.BeginReceive(m_rxBuffer, 0, 1, SocketFlags.None, new AsyncCallback(ReceiveDataCallback), clientSocket);
        }

        private void SendWelcomeMessage(Socket clientSocket)
        {
            if (m_cfg.terminalType == TerminalType.telnet)
            {
                byte[] options = new byte[] {
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.DO,   0x01,   // Don't Echo
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.DO,   0x21,   // Do Remote Flow Control
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.WILL, 0x01,   // Will Echo
                    (byte)TelnetCmd.IAC, (byte)TelnetCmd.WILL, 0x03    // Will Supress Go Ahead
                };

                // If echo does not perfrom by server or device - inform client about it
                if (m_cfg.echoType == EchoType.none)
                    options[7] = (byte)TelnetCmd.WONT;

                SendBytesToSocket(
                    clientSocket,
                    options
                );
            }

            SendStringToSocket(
                clientSocket,
                "You connected to COM-to-telnet on port " + m_cfg.tcpPort.ToString() + ", COM port " + m_portName + "\r\n"
                );

            SendStringToSocket(clientSocket, "Already connected clients:\r\n");
            SendStringToSocket(clientSocket, getClientsString());

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

        private IAsyncResult SendBytesToSocket(Socket sock, byte[] data)
        {
            return sock.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(SendDataCallback), sock);
        }

        private void SendByteToSocket(Socket sock, byte data)
        {
            SendBytesToSocket(sock, new byte[1] { data });
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
                    DisconnectClient(clientSocket);
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
                        (m_cfg.terminalType == TerminalType.telnet))
                    {
                        client.ToIacState();
                    }
                    else
                    {
                        // in 'raw' mode, putty sends CR on Enter, in telnet - CR+LF
                        if (client.m_state == Client.State.signing)
                        {
                            // Echo back the input symbol
                            // Note: in case of telnet if echo is disabled (none), we informed about it, so client
                            // is expected to do local echo. Do not echo in this case
                            if (!((m_cfg.terminalType == TerminalType.telnet) && (m_cfg.echoType == EchoType.none)))
                                SendByteToSocket(clientSocket, m_rxBuffer[0]);

                            var res = client.AddSignatureChar((char)m_rxBuffer[0], m_lineEndChar);

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
                            if (!m_cfg.skipChars.Contains((char)m_rxBuffer[0]))
                            {
                                // If server is asked for echo, do it
                                if (m_cfg.echoType == EchoType.server)
                                    SendByteToSocket(clientSocket, m_rxBuffer[0]);

                                DataReceived(m_rxBuffer, bytesReceived);
                            }
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
                DisconnectClient(sock);
            }
        }

        private IAsyncResult SendStringToSocket(Socket socket, string msg)
        {
            byte[] data = Encoding.ASCII.GetBytes(msg);
            return SendBytesToSocket(socket, data);
        }

        private void DisconnectClient(Socket sock)
        {
            sock.Close();
            m_clients.Remove(sock);

            if (m_clients.Count == 0)
                OnLastClientDisconnected();
        }
    }
}
