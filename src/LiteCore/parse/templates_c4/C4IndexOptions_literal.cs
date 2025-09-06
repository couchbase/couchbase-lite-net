    internal unsafe partial struct C4IndexOptions
    {
        private IntPtr _language;
        private byte _ignoreDiacritics;
        private byte _disableStemming;
        private IntPtr _stopWords;
        private IntPtr _unnestPath;

        #if COUCHBASE_ENTERPRISE
        public C4VectorIndexOptions vector;
        #endif

        private IntPtr _where;

        public string? language
        {
            get {
                return Marshal.PtrToStringAnsi(_language);
            }
            set {
                var old = Interlocked.Exchange(ref _language, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }
        }

        public bool ignoreDiacritics
        {
            get {
                return Convert.ToBoolean(_ignoreDiacritics);
            }
            set {
                _ignoreDiacritics = Convert.ToByte(value);
            }
        }

        public bool disableStemming
        {
            get {
                return Convert.ToBoolean(_disableStemming);
            }
            set {
                _disableStemming = Convert.ToByte(value);
            }
        }

        public string? stopWords
        {
            get {
                return Marshal.PtrToStringAnsi(_stopWords);
            }
            set {
                var old = Interlocked.Exchange(ref _stopWords, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }
        }

        public string? unnestPath
        {
            get {
                return Marshal.PtrToStringAnsi(_unnestPath);
            }
            set {
                var old = Interlocked.Exchange(ref _unnestPath, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }
        }

        public string? where
        {
            get {
                return Marshal.PtrToStringAnsi(_where);
            }
            set {
                var old = Interlocked.Exchange(ref _where, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }
        }
    }
