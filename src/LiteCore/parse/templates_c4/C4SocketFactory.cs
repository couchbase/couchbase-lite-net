#if LITECORE_PACKAGED
    internal
#else
    public
#endif 
    unsafe struct C4SocketFactory
    {
#pragma warning disable 0414
        private byte providesWebSockets;
#pragma warning restore 0414
        public IntPtr open;
        public IntPtr write;
        public IntPtr completedReceive;
        public IntPtr close;
        private IntPtr requestClose; // unused in .NET
        public IntPtr dispose;

        public C4SocketFactory(SocketOpenDelegate open, SocketCloseDelegate close, SocketWriteDelegate write, SocketCompletedReceiveDelegate completedReceive,
            SocketDisposeDelegate dispose)
        {
            this.open = Marshal.GetFunctionPointerForDelegate(open);
            this.write = Marshal.GetFunctionPointerForDelegate(write);
            this.completedReceive = Marshal.GetFunctionPointerForDelegate(completedReceive);
            this.close = Marshal.GetFunctionPointerForDelegate(close);
            this.requestClose = IntPtr.Zero;
            this.providesWebSockets = 0;
            this.dispose = Marshal.GetFunctionPointerForDelegate(dispose);
        }
    }
