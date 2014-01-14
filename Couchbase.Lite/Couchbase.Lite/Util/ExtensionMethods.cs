using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Diagnostics;
using System.Net.Http;

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

        public static String Fmt(this String str, params String[] vals)
        {
            return String.Format(str, vals);
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

