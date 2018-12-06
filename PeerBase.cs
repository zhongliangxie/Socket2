using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Socket2
{
    public class PeerBase
    {
        private SocketTcp sock;
        private Queue<Action> ActionQueue;
        public Queue<byte[]> incomingList;
        private IPeerListener listener;
        private List<byte[]> outgoingStream;
        private object EnqueueLock;
        private int lastPingResult;
        public int timePingInterval = 5000;
        public byte[] pingData;

        public bool Connected =>
            this.PeerState == SocketState.Connected;

        public SocketState PeerState
        {
            get
            {
                return sock.State;
            }
        }

        public PeerBase(IPeerListener listener, string address, int port)
        {
            lastPingResult = Environment.TickCount;

            EnqueueLock = new object();
            this.listener = listener;
            outgoingStream = new List<byte[]>();
            incomingList = new Queue<byte[]>();
            ActionQueue = new Queue<Action>();
            this.sock = new SocketTcp(this, address, port);
        }

        public void Service()
        {
            while (this.DispatchIncomingCommands())
            {
            }
            while (this.SendOutgoingCommands())
            {
            }
        }

        bool DispatchIncomingCommands()
        {
            while (true)
            {
                Action action;
                lock (this.ActionQueue)
                {
                    if (this.ActionQueue.Count <= 0)
                    {
                        byte[] buff;
                        lock (this.incomingList)
                        {
                            if (this.incomingList.Count <= 0)
                                return false;

                            buff = this.incomingList.Dequeue();
                        }
                        this.listener.OnReceive(buff);
                        return true;
                    }
                    action = ActionQueue.Dequeue();
                }
                action();
            }
        }

        bool SendOutgoingCommands()
        {
            if (this.PeerState != SocketState.Disconnected)
            {
                if (!Connected)
                    return false;

                if (this.PeerState == SocketState.Connected && Environment.TickCount - this.lastPingResult > this.timePingInterval)
                    SendPing();

            lock (this.outgoingStream)
            {
                foreach(byte[] buff in outgoingStream)
                {
                    this.SendData(buff);
                }
                this.outgoingStream.Clear();
            }
            }
            return false;
        }

        public void SendPing()
        {
            lastPingResult = Environment.TickCount;
            pingData = this.listener.PingData();
            if (pingData != null)
                this.EnqueueOperation(pingData);
        }

        private void SendData(byte[] buff)
        {
            sock.Send(buff, buff.Length);
        }

        public bool Send(byte[] v)
        {
            lock (this.EnqueueLock)
            {
                return EnqueueOperation(v);
            }
        }

        private bool EnqueueOperation(byte[] data)
        {
            lock (this.outgoingStream)
            {
                this.outgoingStream.Add(data);
            }
            return true;
        }

        public bool Connect()
        {
            return sock.Connect();
        }

        public void OnStatusChanged(int c)
        {
            lock (ActionQueue)
            {
                ActionQueue.Enqueue(() => this.listener.OnStatusChanged(c));
            }
        }

        public void InitCallBack()
        {
            lock (ActionQueue)
            {
                ActionQueue.Enqueue(() => this.listener.OnStatusChanged(2));
            }
          
        }

        public void ReceiveIncomingCommands(byte[] inbuff, int dataLength)
        {
            lock (this.incomingList)
            {
                this.incomingList.Enqueue(inbuff);

                //this.listener.OnReceive(inbuff);
                //NetStream stream1 = new NetStream(inbuff);

                //short cmd1 = stream1.ReadInt16();

                //byte[] b2 = stream1.ReadByte(dataLength - 2);
                //if (cmd1 == 1002)
                //{
                //    int size1 = (inbuff[0] << 8) | inbuff[1];

                //    m_1002_toc r1002 = m_1002_toc.ParseFrom(b2);
                //    var uid = r1002.Uid;
                //    // Write the response to the console.
                //    System.Diagnostics.Debug.WriteLine("Response received : {0}" + uid);
                //}
            }
        }

        public void Disconnect()
        {
            sock.Dispose();
        }
    }
}
