    internal unsafe struct C4ListenerConfig
    {
        public ushort port;
        public FLSlice networkInterface;
        public C4ListenerAPIs apis;
        public C4TLSConfig* tlsConfig;
        public IntPtr httpAuthCallback;
        public void* callbackContext;
        public FLSlice directory;
        private byte _allowCreateDBs;
        private byte _allowDeleteDBs;
        private byte _allowPush;
        private byte _allowPull;
        private byte _enableDeltaSync;

        public bool allowCreateDBs
        {
            get {
                return Convert.ToBoolean(_allowCreateDBs);
            }
            set {
                _allowCreateDBs = Convert.ToByte(value);
            }
        }

        public bool allowDeleteDBs
        {
            get {
                return Convert.ToBoolean(_allowDeleteDBs);
            }
            set {
                _allowDeleteDBs = Convert.ToByte(value);
            }
        }

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
