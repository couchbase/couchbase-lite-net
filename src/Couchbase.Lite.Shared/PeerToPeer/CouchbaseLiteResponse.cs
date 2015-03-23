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
        private readonly HttpListenerContext _context;
        private bool _headersWritten;

        public int Status { get; set; }

        public bool Chunked { 
            get { return _context.Response.SendChunked; }
            set { _context.Response.SendChunked = value; }
        }

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

        public CouchbaseLiteResponse(HttpListenerContext context) {
            Headers = new Dictionary<string, string>();
            InternalStatus = StatusCode.Ok;
            _context = context;
        }

        public ICouchbaseResponseState AsDefaultState()
        {
            return new DefaultCouchbaseResponseState(this);
        }

        public void WriteToContext()
        {
            if (Chunked) {
                Log.E(TAG, "Attempt to send one-shot data when in chunked mode");
                return;
            }

            if (this.Body != null) {
                if (!Headers.ContainsKey("Content-Type")) {
                    this["Content-Type"] = "application/json";
                }

                _context.Response.ContentEncoding = Encoding.UTF8;
                var json = this.Body.GetJson().ToArray();
                _context.Response.ContentLength64 = json.Length;
                _context.Response.OutputStream.Write(json, 0, json.Length);
            }

            _context.Response.Close();
        }

        public void WriteData(IEnumerable<byte> data, bool finished)
        {
            if (!Chunked) {
                Log.E(TAG, "Attempt to send streaming data when not in chunked mode");
                return;
            }

            var array = data.ToArray();
            _context.Response.OutputStream.Write(array, array.Length);
            _context.Response.OutputStream.Flush();
            if (finished) {
                _context.Response.Close();
            }
        }

        public void WriteHeaders()
        {
            if (_headersWritten) {
                return;
            }

            _headersWritten = true;
            Validate();
            this["Server"] = "Couchbase Lite " + Manager.VersionString;
            if (this.Status == 200 && _context.Request.HttpMethod.Equals("GET") || _context.Request.HttpMethod.Equals("HEAD")) {
                if (!Headers.ContainsKey("Cache-Control")) {
                    this["Cache-Control"] = "must-revalidate";
                }
            }

            _context.Response.StatusCode = Status;
            _context.Response.StatusDescription = StatusMessage;

            foreach (var header in Headers) {
                _context.Response.AddHeader(header.Key, header.Value);
            }
        }

        private void Validate()
        {
            bool accept = false;
            foreach (var acceptType in _context.Request.AcceptTypes) {
                if (acceptType.IndexOf("*/*") != -1 || acceptType.Equals(BaseContentType)) {
                    accept = true;
                    break;
                }
            }

            if (!accept) {
                if (!_context.Request.AcceptTypes.Contains(BaseContentType)) {
                    Log.D(TAG, "Unacceptable type {0} (Valid: {1})", BaseContentType, _context.Request.AcceptTypes);
                    Reset();
                    InternalStatus = StatusCode.NotAcceptable;
                }
            }
        }

        public void Reset()
        {
            Headers.Clear();
            Body = null;
            _headersWritten = false;
        }

        public string this[string key]
        {
            get { return Headers == null ? null : Headers[key]; }
            set { Headers[key] = value; }
        }
    }
}

