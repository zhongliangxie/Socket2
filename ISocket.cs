using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Socket2
{
    public abstract class ISocket
    {
        public void HandleReceivedDatagram(byte[] inBuffer, int length,bool willBeReused)
        {

            ReceiveIncomingCommands(inBuffer, length);
        }

        public abstract bool Receive(out byte[] data);
        public abstract bool Send(byte[] data);

        public void ReceiveIncomingCommands(byte[] inBuffer, int length)
        {
            return;
        }
    }
}
