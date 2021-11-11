using System;
using System.Net;

namespace SerialToNetDotnet
{
    class Client
    {
        const int telnetCmdLen = 2;
        public enum State
        {
            signing,
            normal,
            iac, // Interpret as command
        }

        public enum SignatureAppendResult
        {
            finished,
            resume,
            empty,
        }

        public State m_state { get; set; }
        private State m_prevState = State.normal;

        private IPEndPoint m_remoteEp;
        private readonly uint m_id;
        private byte[] m_telnetCmd;
        private int m_iacByteCounter;
        private Server m_server;
        public string m_signature { get; set; } = "";

        public Client(uint clientId, IPEndPoint remoteEp, Server server)
        {
            this.m_state = State.signing;
            this.m_id = clientId;
            this.m_remoteEp = remoteEp;
            this.m_server = server;
            this.m_telnetCmd = new byte[telnetCmdLen];
            this.m_iacByteCounter = 0;
        }

        public void ToIacState()
        {
            m_prevState = m_state;
            m_state = State.iac;
            m_iacByteCounter = 0;
        }

        public void AddIacByte(byte v)
        {
            m_telnetCmd[m_iacByteCounter] = v;
            ++m_iacByteCounter;

            if (m_iacByteCounter == telnetCmdLen)
            {
                m_state = m_prevState;
                // TODO interpret command, call server
            }
        }

        public SignatureAppendResult AddSignatureChar(byte ch)
        {
            // CR - finish signature input if it is not empty
            if ((char)ch == '\n')
            {
                if (m_signature.Length != 0)
                {
                    m_state = State.normal;
                    return SignatureAppendResult.finished;
                }
                else
                    return SignatureAppendResult.empty;
            }
            else if (!Char.IsControl((char)ch))
            {
                m_signature += (char)ch;
                return SignatureAppendResult.resume;
            }
            else
                return SignatureAppendResult.resume;
        }
    }
}
