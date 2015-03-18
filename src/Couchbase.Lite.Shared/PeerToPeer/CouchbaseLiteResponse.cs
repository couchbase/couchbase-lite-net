//
//  CouchbaseLiteResponse.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Linq;
using System.Net;
using System.Collections.Generic;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Util;
using System.Text;

namespace Couchbase.Lite.PeerToPeer
{
    internal sealed class CouchbaseLiteResponse
    {
        private const string TAG = "CouchbaseLiteResponse";

        public int Status { get; set; }

        private StatusCode _internalStatus;
        public StatusCode InternalStatus { 
            get { return _internalStatus; }
            set {
                _internalStatus = value;
                var httpStatus = Couchbase.Lite.Status.ToHttpStatus(_internalStatus);
                Status = httpStatus.Item1;
                StatusMessage = httpStatus.Item2;
                if (Status < 300) {
                    if (Body == null && !Headers.ContainsKey("Content-Type")) {
                        Body = new Body(Encoding.UTF8.GetBytes("{\"ok\":true}"));
                    }
                } else {
                    Body = new Body(new Dictionary<string, object> {
                        { "status", Status },
                        { "error", StatusMessage },
                    });
                }
            }
        }

        public string StatusMessage { get; private set; }

        public string StatusReason { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public Body Body { get; set; }

        public string BaseContentType { get; set; }

        public CouchbaseLiteResponse() {
            Status = 200;
            StatusMessage = "Ok";
            Headers = new Dictionary<string, string>();
        }

        public void WriteToContext(HttpListenerContext ctx)
        {
            this["Server"] = "Couchbase Lite " + Manager.VersionString;
            bool accept = false;
            foreach (var acceptType in ctx.Request.AcceptTypes) {
                if (acceptType.IndexOf("*/*") != -1 || acceptType.Equals(BaseContentType)) {
                    accept = true;
                    break;
                }
            }

            if (!accept) {
                if (!ctx.Request.AcceptTypes.Contains(BaseContentType)) {
                    Log.D(TAG, "Unacceptable type {0} (Valid: {1})", BaseContentType, ctx.Request.AcceptTypes);
                    Reset();
                    InternalStatus = StatusCode.NotAcceptable;
                }
            }

            if (this.Status == 200 && ctx.Request.HttpMethod.Equals("GET") || ctx.Request.HttpMethod.Equals("HEAD")) {
                if (!Headers.ContainsKey("Cache-Control")) {
                    this["Cache-Control"] = "must-revalidate";
                }
            }

            ctx.Response.StatusCode = Status;
            ctx.Response.StatusDescription = StatusMessage;

            foreach (var header in Headers) {
                ctx.Response.AddHeader(header.Key, header.Value);
            }

            if (this.Body != null) {
                if (!Headers.ContainsKey("Content-Type")) {
                    this["Content-Type"] = "application/json";
                }

                ctx.Response.ContentEncoding = Encoding.UTF8;
                var json = this.Body.GetJson().ToArray();
                ctx.Response.ContentLength64 = json.Length;
                ctx.Response.OutputStream.Write(json, 0, json.Length);


            }
        }

        public void Reset()
        {
            Headers.Clear();
            Body = null;
        }

        public string this[string key]
        {
            get { return Headers[key]; }
            set { Headers[key] = value; }
        }
    }
}

