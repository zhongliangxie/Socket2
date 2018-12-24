namespace Socket2
{
    public interface IPeerListener
    {
        void OnReceive(byte[] data);

        void OnStatusChanged(int status);

        byte[] PingData();
        void DebugReturn(DebugLevel level, string debugReturn);
        //void Send(byte[] data);
    }
}
