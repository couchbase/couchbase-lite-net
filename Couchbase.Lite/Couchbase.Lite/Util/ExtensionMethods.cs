using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Net.Http;
using System.IO;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    public static class ExtensionMethods
    {
        public static IEnumerable ToEnumerable(this IEnumerator enumerator)
        {
            while(enumerator.MoveNext()) {
                yield return enumerator.Current;
            }
        }

        public static String Fmt(this String str, params IConvertible[] vals)
        {
            return String.Format(str, vals);
        }

        public static Byte[] ReadAllBytes(this Stream stream)
        {
            var chunkBuffer = new byte[Attachment.DefaultStreamChunkSize];
            // We know we'll be reading at least 1 chunk, so pre-allocate now to avoid an immediate resize.
            var blob = new List<Byte> (Attachment.DefaultStreamChunkSize);

            int bytesRead;
            do {
                chunkBuffer.Initialize ();
                // Resets all values back to zero.
                bytesRead = stream.Read (chunkBuffer, blob.Count, Attachment.DefaultStreamChunkSize);
                blob.AddRange (chunkBuffer.Take (bytesRead));
            }
            while (bytesRead < stream.Length);

            return blob.ToArray();
        }

        public static StatusCode GetStatusCode(this HttpStatusCode code)
        {
            StatusCode status;
            Enum.TryParse(code.ToString(), out status);
            return status;
        }

        public static ICredentials ToCredentialsFromUri(this HttpRequestMessage request)
        {
            Debug.Assert(request != null);
            Debug.Assert(request.RequestUri != null);

            var unescapedUserInfo = request.RequestUri.UserEscaped
                                    ? System.Web.HttpUtility.UrlDecode(request.RequestUri.UserInfo)
                                    : request.RequestUri.UserInfo;

            var userAndPassword = unescapedUserInfo.Split(new[] { ':' }, 1, StringSplitOptions.None);
            if (userAndPassword.Length != 2)
                return null;

            return new NetworkCredential(userAndPassword[0], userAndPassword[1], request.RequestUri.DnsSafeHost);
        }
    }
}

