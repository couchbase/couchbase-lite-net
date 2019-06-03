    internal unsafe struct C4ExtraInfo
    {
        public void* pointer;
        private IntPtr _destructor;

        public C4ExtraInfoDestructor destructor
        {
            get => Marshal.GetDelegateForFunctionPointer<C4ExtraInfoDestructor>(_destructor);
            set => _destructor = Marshal.GetFunctionPointerForDelegate(value);
        }
    }
    