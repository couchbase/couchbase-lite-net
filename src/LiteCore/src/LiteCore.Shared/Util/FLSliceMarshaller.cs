using System;
using System.Runtime.InteropServices;
#if NET8_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;

using LiteCore.Interop;

namespace LiteCore.Util;

[CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedIn, typeof(ManagedToUnmanagedIn))]
[CustomMarshaller(typeof(byte[]), MarshalMode.ManagedToUnmanagedIn, typeof(ManagedToUnmanagedInArray))]
[CustomMarshaller(typeof(string), MarshalMode.ManagedToUnmanagedOut, typeof(UnmanagedToManagedOut))]
internal static class FLSliceMarshaller
{
    public ref struct ManagedToUnmanagedIn
    {
        private FLSlice _unmanagedValue;
        private bool _allocated;
        
        public static int BufferSize => 0x100;
        
        public unsafe void FromManaged(string? managed, Span<byte> buffer)
        {
            _allocated = false;
            if (managed is null) {
                _unmanagedValue = default;
                return;
            }
            
            const int maxUtf8BytesPerChar = 3;

            // Use the cast to long to avoid the checked operation
            if ((long)maxUtf8BytesPerChar * managed.Length > buffer.Length)
            {
                // Calculate accurate byte count when the provided stack-allocated buffer is not sufficient
                var exactByteCount = Encoding.UTF8.GetByteCount(managed);
                if (exactByteCount > buffer.Length) {
                    buffer = new Span<byte>((byte*)NativeMemory.Alloc((nuint)exactByteCount), exactByteCount);
                    _allocated = true;
                }
            }
            
            _unmanagedValue.buf = Unsafe.AsPointer(ref MemoryMarshal.GetReference(buffer));

            var byteCount = Encoding.UTF8.GetBytes(managed, buffer);
            _unmanagedValue.size = (ulong)byteCount;
        }

        public FLSlice ToUnmanaged() => _unmanagedValue;

        public unsafe void Free()
        {
            if (_allocated) {
                NativeMemory.Free(_unmanagedValue.buf);
            }
        }
    }
    
    public ref struct ManagedToUnmanagedInArray
    {
        private FLSlice _unmanagedValue;
        private GCHandle _pin;
        
        public unsafe void FromManaged(byte[]? managed)
        {
            if (managed is null) {
                _unmanagedValue = default;
                return;
            }

            _pin = GCHandle.Alloc(managed, GCHandleType.Pinned);
            _unmanagedValue.buf = (void*)_pin.AddrOfPinnedObject();
            _unmanagedValue.size = (ulong)managed.Length;
        }

        public FLSlice ToUnmanaged() => _unmanagedValue;

        public void Free()
        {
            _pin.Free();
        }
    }
    
    public ref struct UnmanagedToManagedOut
    {
        private string? _managed;
        
        public unsafe void FromUnmanaged(FLSlice unmanaged)
        {
            if (unmanaged.buf == null) {
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

        public void Free()
        {
            // no-op, this memory is owned by LiteCore
        }
    }
}

#endif
