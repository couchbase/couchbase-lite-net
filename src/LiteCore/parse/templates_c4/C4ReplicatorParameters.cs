    internal unsafe partial struct C4ReplicatorParameters
    {
        public C4ReplicatorMode push;
        public C4ReplicatorMode pull;
        public C4Slice optionsDictFleece;
        public IntPtr validationFunc;
        public IntPtr onStatusChanged;
        public IntPtr onDocumentError;
        public void* callbackContext;
    }
