    internal unsafe struct C4ReplicationCollection
    {
        public C4CollectionSpec collection;
        public C4ReplicatorMode push;
        public C4ReplicatorMode pull;
        public FLSlice optionsDictFleece;
        public IntPtr pushFilter;
        public IntPtr pullFilter;
        public void* callbackContext;
    }