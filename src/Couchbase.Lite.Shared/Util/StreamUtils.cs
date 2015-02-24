using System;
using Sharpen;
using System.IO;
using System.Collections.Generic;

namespace Couchbase.Lite.Util
{
    public static class StreamUtils
    {
        #if NET_3_5

        public static void CopyTo(this Stream input, Stream output)
        {
            byte[] buffer = new byte[16 * 1024]; // Fairly arbitrary size
            int bytesRead;

            while ((bytesRead = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, bytesRead);
            }
        }

        #endif

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
            while ((n = inStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                outStream.Write(buffer, 0, n);
            }
            outStream.Dispose();
            inStream.Dispose();
        }
    }
}

