    internal unsafe struct C4ReplicatorParameters
    {
        public FLSlice optionsDictFleece;
        public IntPtr onStatusChanged;
        public IntPtr onDocumentEnded;
        public IntPtr onBlobProgress;
		public IntPtr propertyEncryptor;
		public IntPtr propertyDecryptor;
        public void* callbackContext;
        public C4SocketFactory* socketFactory;
        public C4ReplicationCollection* collections;
        public IntPtr collectionCount;
    }
