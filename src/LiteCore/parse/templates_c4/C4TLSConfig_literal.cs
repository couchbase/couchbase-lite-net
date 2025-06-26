    internal unsafe struct C4TLSConfig
    {
        public C4PrivateKeyRepresentation privateKeyRepresentation;
        public C4KeyPair* key;
        public C4Cert* certificate;
        private byte _requireClientCerts;
        public C4Cert* rootClientCerts;
        public IntPtr certAuthCallback;
        public void* tlsCallbackContext;

        public bool requireClientCerts
        {
            get {
                return Convert.ToBoolean(_requireClientCerts);
            }
            set {
                _requireClientCerts = Convert.ToByte(value);
            }
        }
    }
