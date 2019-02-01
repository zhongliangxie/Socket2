using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security;
using System.Threading;

namespace Socket2
{
    public enum DebugLevel : byte
    {
        OFF = 0,
        ERROR = 1,
        WARNING = 2,
        INFO = 3,
        ALL = 5
    }

    public enum SocketState
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Disconnecting = 3
    }

    public class SocketTcp : IDisposable
    {
        private readonly int MTU = 2048;
        private Socket sock;
        private readonly String ServerAddress;
        private readonly int ServerPort;
        public SocketState State;
        private Queue<byte[]> incomingList;
        private PeerBase peerBase;
        private readonly object syncer;

        public SocketTcp(PeerBase peerBase)
        {
            syncer = new object();

            this.peerBase = peerBase;

            this.incomingList = new Queue<byte[]>();
        }

        public SocketTcp(PeerBase peerBase, string serverAddress, int serverPort)
        {
            syncer = new object();

            this.peerBase = peerBase;
            this.ServerAddress = serverAddress;
            this.ServerPort = serverPort;
            this.incomingList = new Queue<byte[]>();
        }

        public bool Connect()
        {
            new Thread(new ThreadStart(DnsAndConnect))
            {
                Name = " dns thread",
                IsBackground = true
            }.Start();
            return true;
        }
        public void EnqueueDebugReturn(DebugLevel debugLevel, string message)
        {
            this.peerBase.EnqueueDebugReturn(debugLevel, message);
        }

        public void HandleException(int statusCode)
        {

            this.State = SocketState.Disconnecting;
            peerBase.EnqueueStatusCallback(statusCode);
            peerBase.EnqueueActionForDispatch(() => peerBase.Disconnect());

        }

        public void DnsAndConnect()
        {
            try
            {
                IPAddress ipAddress = Dns.GetHostAddresses(this.peerBase.ServerAddress)[0];
                if (ipAddress == null)
                {
                    throw new ArgumentException("Invalid IPAddress.Address:" + this.ServerAddress);
                }
                this.sock = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };
                this.sock.Connect(ipAddress, this.peerBase.ServerPort);
                this.State = SocketState.Connected;
                peerBase.InitCallBack();
            }
            catch (SecurityException se)
            {
                EnqueueDebugReturn(DebugLevel.ERROR, "Connect() to '" + ServerAddress + "' failed: " +  se.ToString());
               
                //System.Diagnostics.Debug.WriteLine("aaa DnsAndConnect se :" + se.StackTrace);
                return;
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine("aaa DnsAndConnect exception :" + exception.StackTrace);
                return;
            }
            new Thread(new ThreadStart(this.ReceiveLoop))
            {
                Name = " receive thread",
                IsBackground = true
            }.Start();
        }

        public void Send(byte[] data, int length)
        {
            if (!this.sock.Connected)
            {
                return;
            }
            try
            {
                this.sock.Send(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("aaa Send se :" + ex.StackTrace);
                return;
            }
            return;
        }

        public void ReceiveLoop()
        {
            MemoryStream stream = new MemoryStream(this.MTU);
            while (this.State == SocketState.Connected)
            {
                stream.Position = 0;
                stream.SetLength(0);
                try
                {
                    //byte[] buffer = new byte[1024];
                    //int num2 = this.sock.Receive(buffer, 0, 1024, SocketFlags.None);

                    //byte[] inbuffer = new byte[num2];
                    //Array.Copy(buffer, inbuffer, num2);
                    //HandleReceivedDatagram(inbuffer, num2, false);

                    int offset = 0;
                    int num2 = 0;
                    byte[] buffer = new byte[11];
                    while (offset < 11)
                    {
                        num2 = this.sock.Receive(buffer, offset, 11 - offset, SocketFlags.None);
                        offset += num2;
                        if (num2 == 0)
                        {
                            throw new SocketException(0x2746);
                        }

                    }
                    //if (buffer[0] == 240)
                    //{
                    //    HandleReceivedDatagram(buffer, buffer.Length, false);
                    //    continue;
                    //}
                    //int size = (((buffer[1] << 0x18) | (buffer[2] << 0x10)) | (buffer[3] << 8)) | buffer[4];
                    //int size = (buffer[0] << 8) | buffer[1];

                    int size = buffer[0] << 0x18 | buffer[1] << 0x10 | buffer[2] << 8 | buffer[3];//int size = (((buffer[0] << 0x18) | (buffer[1] << 0x10)) | (buffer[2] << 8)) | buffer[3];

                    stream.Write(buffer,5, offset - 9);
                    offset = 0;
                   // size -= 8;
                    buffer = new byte[size];
                    while (offset < size)
                    {
                        num2 = this.sock.Receive(buffer, offset, size - offset, SocketFlags.None);
                        offset += num2;
                        if (num2 == 0)
                        {
                            throw new SocketException(0x2746);
                        }
                    }
                    stream.Write(buffer, 0, offset);
                    if (stream.Length > 0)
                    {
                        HandleReceivedDatagram(stream.ToArray(), (int)stream.Length, false);
                    }
                }
                catch (SocketException se)
                {
                    System.Diagnostics.Debug.WriteLine("aaa SocketException se : {0}" + se.StackTrace);
                    HandleException((int)SocketState.Disconnecting);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("bb SocketException ex : {0}" + ex.StackTrace);
                    HandleException((int)SocketState.Disconnecting);
                }
            }
            this.Disconnect();
        }

        public void HandleReceivedDatagram(byte[] inBuffer, int lenght, bool willBeReused)
        {
            this.peerBase.ReceiveIncomingCommands(inBuffer, lenght);
        }

        //public void ReceiveIncomingCommands(byte[] inbuff, int dataLength)
        //{
        //    lock (this.incomingList)
        //    {
        //        this.incomingList.Enqueue(inbuff);

        //        NetStream stream1 = new NetStream(inbuff);

        //        short cmd1 = stream1.ReadInt16();

        //        byte[] b2 = stream1.ReadByte(dataLength - 2);
        //        if (cmd1 == 1002)
        //        {
        //            int size1 = (inbuff[0] << 8) | inbuff[1];

        //            m_1002_toc r1002 = m_1002_toc.ParseFrom(b2);
        //            var uid = r1002.Uid;
        //            // Write the response to the console.
        //            System.Diagnostics.Debug.WriteLine("Response received : {0}" + uid);
        //        }
        //    }
        //}

        public bool Disconnect()
        {
            EnqueueDebugReturn(DebugLevel.INFO, "SocketTcp.Disconnect()");
            State = SocketState.Disconnecting;
            lock (syncer)
            {
                if (sock != null)
                {
                    try
                    {
                        sock.Close();
                    }
                    catch (Exception exception)
                    {
                        EnqueueDebugReturn(DebugLevel.INFO, "Exception in Disconnect(): " + exception);
                    }
                    sock = null;
                }
            }
            State = SocketState.Disconnected;
            return true;
        }

        public void Dispose()
        {
            this.State = SocketState.Disconnecting;
            lock (syncer)
            {
                if (this.sock != null)
                {
                    try
                    {
                        this.sock.Close();
                    }
                    catch (Exception exception)
                    {
                        EnqueueDebugReturn(DebugLevel.INFO, "Exception in Dispose(): " + exception);
                    }
                    this.sock = null;
                }
            }
            this.State = SocketState.Disconnected;
            this.peerBase.OnStatusChanged((int)State);
        }
    }
}
