    internal unsafe partial struct C4SocketFactory
    {
        public C4SocketFraming framing;
        public void* context;
        public IntPtr open;
        public IntPtr write;
        public IntPtr completedReceive;
        public IntPtr close;
        public IntPtr requestClose;
        public IntPtr dispose;
    }
