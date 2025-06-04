    internal unsafe partial struct C4PeerSyncCollection
    {
        public C4CollectionSpec collection;
        private byte _pushEnabled;
        private byte _pullEnabled;
        public FLSlice optionsDictFleece;
        private IntPtr _pushFilter;
        private IntPtr _pullFilter;
        public void* callbackContext;

        public bool pushEnabled
        {
            get
            {
                return Convert.ToBoolean(_pushEnabled);
            }
            set
            {
                _pushEnabled = Convert.ToByte(value);
            }
        }

        public bool pullEnabled
        {
            get
            {
                return Convert.ToBoolean(_pullEnabled);
            }
            set
            {
                _pullEnabled = Convert.ToByte(value);
            }
        }
    }