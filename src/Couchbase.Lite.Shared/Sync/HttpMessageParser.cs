// 
//  HttpMessageParser.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 
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
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public HttpStatusCode StatusCode { get; }

        public string Reason { get; }

        public IReadOnlyDictionary<string, string> Headers => _headers;

        public HttpMessageParser(string firstLine)
        {
            if (firstLine?.StartsWith("HTTP") == true) {
                var split = firstLine.Split(' ');
                StatusCode = (HttpStatusCode)Int32.Parse(split[1]);
                Reason = split.Skip(2).Aggregate((l, r) => String.Concat(l, " ", r));
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

                    var headerKey = line.Substring(0, colonPos);
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

            var headerKey = line.Substring(0, colonPos);
            var headerValue = line.Substring(colonPos + 1).TrimStart();
            _headers[headerKey] = headerValue;
        }
    }
}
