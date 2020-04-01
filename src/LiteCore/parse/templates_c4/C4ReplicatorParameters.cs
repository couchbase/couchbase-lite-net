    internal unsafe struct C4ReplicatorParameters
    {
        public C4ReplicatorMode push;
        public C4ReplicatorMode pull;
        public FLSlice optionsDictFleece;
        public IntPtr pushFilter;
        public IntPtr validationFunc;
        public IntPtr onStatusChanged;
        public IntPtr onDocumentEnded;
        public IntPtr onBlobProgress;
        public void* callbackContext;
        public C4SocketFactory* socketFactory;
    }
