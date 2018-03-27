#if LITECORE_PACKAGED
    internal
#else
    public
#endif 
    unsafe struct FLDictIterator
    {
        #pragma warning disable CS0169

        private void* _private1;
        private uint _private2;
        private byte _private3;

        // _private4[3]
        private void* _private4;
        private void* _private5;
        private void* _private6;

        #pragma warning restore CS0169
    }
