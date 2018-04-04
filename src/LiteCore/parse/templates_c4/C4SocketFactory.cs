    internal unsafe struct C4SocketFactory
    {
#pragma warning disable 0414
        public byte providesWebSockets;
#pragma warning restore 0414
        public void* context;
        public IntPtr open;
        public IntPtr write;
        public IntPtr completedReceive;
        public IntPtr close;
        public IntPtr requestClose;
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
            this.context = null;
        }
        
        public C4SocketFactory(SocketOpenDelegate open, SocketRequestCloseDelegate requestClose, SocketWriteDelegate write, SocketCompletedReceiveDelegate completedReceive,
            SocketDisposeDelegate dispose)
        {
            this.open = Marshal.GetFunctionPointerForDelegate(open);
            this.write = Marshal.GetFunctionPointerForDelegate(write);
            this.completedReceive = Marshal.GetFunctionPointerForDelegate(completedReceive);
            this.close = IntPtr.Zero;
            this.requestClose = Marshal.GetFunctionPointerForDelegate(requestClose);
            this.providesWebSockets = 1;
            this.dispose = Marshal.GetFunctionPointerForDelegate(dispose);
            this.context = null;
        }
    }
