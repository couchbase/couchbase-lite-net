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
            var version = typeof(Native).GetTypeInfo().Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

#if NETCOREAPP2_0 || NETFRAMEWORK
            NetDesktop.LoadLiteCore();
#endif
        }

        #endregion
    }
}