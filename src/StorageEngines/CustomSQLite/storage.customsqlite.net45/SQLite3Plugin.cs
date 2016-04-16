using Couchbase.Lite.Util;
using System;
using System.Runtime.InteropServices;

namespace Couchbase.Lite.Storage.CustomSQLite
{
    internal static class SQLite3Plugin
    {

        [DllImport("kernel32")]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        public static void Init()
        {
            if(Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                return;
            }

            Log.To.Database.I("BAR", "FOO");
            // We just need to load the sqlite3 DLL into memory
            const string dllName = "sqlite3.dll";

            string baseDirectory = new Uri(AppDomain.CurrentDomain.BaseDirectory).LocalPath;
            if (baseDirectory == null)
            {
			    var currentAssembly = typeof(SQLite3Plugin).Assembly;
                var codeBase = currentAssembly.CodeBase;
                var uri = new UriBuilder(codeBase);
                baseDirectory = System.IO.Path.GetDirectoryName(Uri.UnescapeDataString(uri.Path));

            }
            if (baseDirectory == null) {
                return;
            }
            System.Diagnostics.Debug.Assert(System.IO.Path.IsPathRooted(baseDirectory), "baseDirectory is not rooted.");

            var architecture = IntPtr.Size == 4
                ? "x86"
                : "x64";

            var dllPath = System.IO.Path.Combine(System.IO.Path.Combine(baseDirectory, architecture), dllName);
            if (!System.IO.File.Exists(dllPath)) {
                return;
            }

            const uint LOAD_WITH_ALTERED_SEARCH_PATH = 8;

            LoadLibraryEx(dllPath, IntPtr.Zero, LOAD_WITH_ALTERED_SEARCH_PATH);
        }
    }
}
