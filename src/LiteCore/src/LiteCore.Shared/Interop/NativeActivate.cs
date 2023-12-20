using System;
using System.IO;
using System.Reflection;

using Couchbase.Lite.Support;

namespace LiteCore.Interop
{
    internal partial class Native
    {
        #region Constructors

        static Native()
        {
#if NET6_0_OR_GREATER
            if(OperatingSystem.IsIOS()) {
                System.Diagnostics.Debug.WriteLine("Setting DllImportResolver");
                System.Runtime.InteropServices.NativeLibrary.SetDllImportResolver(typeof(Native).Assembly, (libraryName, assembly, searchPath) =>
                {
                    System.Diagnostics.Debug.WriteLine($"DllImportResolver called for {libraryName}");
                    if (libraryName == Constants.DllName) {
                        libraryName = "@rpath/LiteCore.framework/LiteCore";
                    }

                    return System.Runtime.InteropServices.NativeLibrary.Load(libraryName);
                });
            }
#elif NETSTANDARD2_0
            throw new PlatformNotSupportedException(
                "Pure .NET Standard variant executed.  This means that Couchbase Lite is running on an unsupported platform");
#endif

#if NEEDS_LITECORE_LOAD
            NetDesktop.LoadLiteCore();
#endif
        }

        #endregion
    }
}