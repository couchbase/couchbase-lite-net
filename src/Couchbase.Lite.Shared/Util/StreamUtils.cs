using System;
using Sharpen;
using System.IO;
using System.Collections.Generic;

namespace Couchbase.Lite.Util
{
    public static class StreamUtils
    {
        /// <exception cref="System.IO.IOException"></exception>
//        public static void CopyStream(InputStream @is, OutputStream os)
//        {
//            int n;
//            byte[] buffer = new byte[16384];
//            while ((n = @is.Read(buffer)) > -1)
//            {
//                os.Write(buffer, 0, n);
//            }
//            os.Close();
//            @is.Close();
//        }

        /// <exception cref="System.IO.IOException"></exception>
        internal static void CopyStreamsToFolder(IDictionary<String, Stream> streams, FilePath folder)
        {
            foreach (var entry in streams)
            {
                var file = new FilePath(folder, entry.Key);
                CopyStreamToFile(entry.Value, file);
            }
        }

        /// <exception cref="System.IO.IOException"></exception>
        internal static void CopyStreamToFile(Stream inStream, FilePath file)
        {
            var outStream = new FileStream(file.GetAbsolutePath(), FileMode.OpenOrCreate);
            var n = 0;
            var buffer = new byte[16384];
            while ((n = inStream.Read(buffer, n, buffer.Length)) > -1)
            {
                outStream.Write(buffer, 0, n);
            }
            outStream.Close();
            inStream.Close();
        }
    }
}

