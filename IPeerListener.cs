namespace Socket2
{
    public interface IPeerListener
    {
        void OnReceive(byte[] data);

        void OnStatusChanged(int status);

        byte[] PingData();
        //void Send(byte[] data);
    }
}
