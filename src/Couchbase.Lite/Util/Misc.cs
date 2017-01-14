using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Lite.Util
{
    internal static class Misc
    {
        public static string CreateGUID()
        {
            var sb = new StringBuilder(Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('='));

            // URL-safe character set per RFC 4648 sec. 5:
            sb.Replace('/', '_');
            sb.Replace('+', '-');

            // prefix a '-' to make it more clear where this string came from and prevent having a leading
            // '_' character:
            sb.Insert(0, '-');
            return sb.ToString();
        }
    }
}
