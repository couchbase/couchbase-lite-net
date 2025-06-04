using System.Runtime.InteropServices;

namespace LiteCore.Interop;

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void C4LogObserverCallback(C4LogEntry* entry, void* context);
