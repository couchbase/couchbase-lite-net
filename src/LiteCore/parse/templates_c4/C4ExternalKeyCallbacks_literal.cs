    internal unsafe struct C4ExternalKeyCallbacks
    {
        public IntPtr publicKeyData;
        public IntPtr decrypt;
        public IntPtr sign;
        public IntPtr free;
    }