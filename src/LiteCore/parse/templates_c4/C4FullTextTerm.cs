#if LITECORE_PACKAGED
    internal
#else
    public
#endif 
    struct C4FullTextTerm
    {
        public uint termIndex;
        public uint start, length;
    }