#if NET8_0_OR_GREATER
using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

using LiteCore.Interop;

namespace LiteCore.Util;

[CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedOut, typeof(ManagedToUnmanagedOut))]
internal static class FLSliceResultMarshaller
{
    public ref struct ManagedToUnmanagedOut
    {
        private string? _managed;
        private FLSliceResult _unmanaged;
        
        public unsafe void FromUnmanaged(FLSliceResult unmanaged)
        {
            _unmanaged = unmanaged;
            if (_unmanaged.buf == null) {
                return;
            }
            
            if (unmanaged.size == 0) {
                _managed = String.Empty;
                return;
            }

            _managed = new string((sbyte*)unmanaged.buf, 0, 
                (int)unmanaged.size, Encoding.UTF8);
        }

        public string? ToManaged() => _managed;

        public void Free() => Native.FLSliceResult_Release(_unmanaged);
    }
}
#endif