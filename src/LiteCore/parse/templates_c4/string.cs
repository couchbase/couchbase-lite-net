        public string {0}
        {{
            get {{
                return Marshal.PtrToStringAnsi(_{0});
            }}
            set {{
                var old = Interlocked.Exchange(ref _{0}, Marshal.StringToHGlobalAnsi(value));
                Marshal.FreeHGlobal(old);
            }}
        }}