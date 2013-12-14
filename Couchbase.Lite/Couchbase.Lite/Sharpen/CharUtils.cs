using System;
using Com.Couchbase.Lite.Util;

namespace Couchbase.Lite
{
    internal static class CharUtils
    {
        public static int Digit(char character, int radix) {
            if (radix != 16)
                throw new ArgumentException("Only hex/base16 is supported.", "radix");
            return URIUtils.HexToInt(character);
        }
    }
}

