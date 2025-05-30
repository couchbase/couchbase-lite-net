    internal unsafe struct C4ListenerConfig
    {
        public ushort port;
        public FLSlice networkInterface;
        public C4TLSConfig* tlsConfig;
        public FLSlice serverName;
        public FLSlice serverVersion;
        public IntPtr httpAuthCallback;
        public void* callbackContext;
        private byte _allowPush;
        private byte _allowPull;
        private byte _enableDeltaSync;

        public bool allowPush
        {
            get {
                return Convert.ToBoolean(_allowPush);
            }
            set {
                _allowPush = Convert.ToByte(value);
            }
        }

        public bool allowPull
        {
            get {
                return Convert.ToBoolean(_allowPull);
            }
            set {
                _allowPull = Convert.ToByte(value);
            }
        }

        public bool enableDeltaSync
        {
            get {
                return Convert.ToBoolean(_enableDeltaSync);
            }
            set {
                _enableDeltaSync = Convert.ToByte(value);
            }
        }
    }
