#if LITECORE_PACKAGED
    internal
#else
    public
#endif
    unsafe partial struct C4ReplicatorParameters
    {
        public C4ReplicatorMode push;
        public C4ReplicatorMode pull;
        public C4Slice optionsDictFleece;
        private IntPtr validationFunc;
        private IntPtr onStatusChanged;
        private IntPtr onDocumentError;
        public void* callbackContext;
    }
