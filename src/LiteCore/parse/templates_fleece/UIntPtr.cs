        public ulong {0}
        {{
            get {{
                return _{0}.ToUInt64();
            }}
            set {{
                _{0} = (UIntPtr)value;
            }}
        }}