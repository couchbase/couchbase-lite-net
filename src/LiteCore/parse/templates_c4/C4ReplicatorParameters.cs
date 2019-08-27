    internal unsafe partial struct C4ReplicatorParameters
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
        private byte _dontStart;
        public bool dontStart
        {
            get {
                return Convert.ToBoolean(_dontStart);
            }
            set {
                _dontStart = Convert.ToByte(value);
            }
        }
    }
