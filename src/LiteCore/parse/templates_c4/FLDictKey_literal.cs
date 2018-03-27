#if LITECORE_PACKAGED
    internal
#else
    public
#endif 
    unsafe struct FLDictKey
    {
        #pragma warning disable CS0169

        // _private1[4] 
        private void* _private1a;
        private void* _private1b;
        private void* _private1c;
        private void* _private1d;
        private uint _private2;
        private uint _private3;
        private byte _private4;
        private byte _private5;

        #pragma warning restore CS0169
    }
