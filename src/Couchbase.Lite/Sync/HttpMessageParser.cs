using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Couchbase.Lite.Sync
{
    internal sealed class HttpMessageParser
    {
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();

        public HttpStatusCode StatusCode { get; }

        public string Reason { get; }

        public IReadOnlyDictionary<string, string> Headers => _headers;

        public HttpMessageParser(string firstLine)
        {
            if (firstLine.StartsWith("HTTP")) {
                var split = firstLine.Split(' ');
                StatusCode = (HttpStatusCode)Int32.Parse(split[1]);
                Reason = split.Skip(2).Aggregate((l, r) => l + " " + r);
            }
        }

        public HttpMessageParser(byte[] data)
        {
            using (var reader = new StreamReader(new MemoryStream(data), Encoding.ASCII)) {
                var firstLine = reader.ReadLine();
                if (firstLine.StartsWith("HTTP")) {
                    var split = firstLine.Split(' ');
                    StatusCode = (HttpStatusCode)Int32.Parse(split[1]);
                    Reason = split[2];
                }

                while (!reader.EndOfStream) {
                    var line = reader.ReadLine();
                    var colonPos = line.IndexOf(':');
                    if (colonPos == -1) {
                        continue;
                    }

                    var headerKey = line.Substring(0, colonPos).ToLowerInvariant();
                    var headerValue = line.Substring(colonPos + 1).TrimStart();
                    _headers[headerKey] = headerValue;
                }
            }
        }

        public void Append(string line)
        {
            var colonPos = line.IndexOf(':');
            if (colonPos == -1) {
                return;
            }

            var headerKey = line.Substring(0, colonPos).ToLowerInvariant();
            var headerValue = line.Substring(colonPos + 1).TrimStart();
            _headers[headerKey] = headerValue;
        }
    }
}
