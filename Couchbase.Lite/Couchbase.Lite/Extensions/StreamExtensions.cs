using System;
using System.IO;

namespace Couchbase.Lite {

    public static class StreamExtensions {

        /// <summary>
        /// Readies a stream to be read from the beginning.
        /// </summary>
        /// <remarks>
        /// Implements the same semantics as Java's Stream.Reset().
        /// </remarks>
        /// <param name="stream">Stream.</param>
        public static void Reset(this Stream stream) {
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);
        }
    }
}

