        public {0}? {1}
        {{
            get {{
                return  Marshal.GetDelegateForFunctionPointer<{0}>(_{1});
            }}
            set {{
                _{1} = value != null ? Marshal.GetFunctionPointerForDelegate(value) : IntPtr.Zero;
            }}
        }}