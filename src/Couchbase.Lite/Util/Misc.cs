using System;
using System.Text;
using Couchbase.Lite.Logging;

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

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, string tag, string message)
        {
            return CreateExceptionAndLog(domain, StatusCode.Exception, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, StatusCode code, string tag, string message)
        {
            domain.E(tag, "{0}, throwing CouchbaseLiteException ({1})", message, code);
            return new CouchbaseLiteException(message, code);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, StatusCode code, string tag,
            string format, params object[] args)
        {
            var message = String.Format(format, args);
            return CreateExceptionAndLog(domain, code, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner, string tag, string message)
        {
            return CreateExceptionAndLog(domain, inner, StatusCode.Exception, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner,
            StatusCode code, string tag, string message)
        {
            domain.E(tag, String.Format("{0}, throwing CouchbaseLiteException",
                message), inner);
            return new CouchbaseLiteException(message, inner) { Code = code };
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner,
            string tag, string format, params object[] args)
        {
            var message = String.Format(format, args);
            return CreateExceptionAndLog(domain, inner, tag, message);
        }

        public static CouchbaseLiteException CreateExceptionAndLog(DomainLogger domain, Exception inner,
            StatusCode code, string tag, string format, params object[] args)
        {
            var message = String.Format(format, args);
            return CreateExceptionAndLog(domain, inner, code, tag, message);
        }
    }
}
