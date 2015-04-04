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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

using Couchbase.Lite.Internal;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;
using System.IO;
using Couchbase.Lite.Replicator;

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
                    if (JsonBody == null && !Headers.ContainsKey("Content-Type")) {
                        JsonBody = new Body(Encoding.UTF8.GetBytes("{\"ok\":true}"));
                    }
                } else {
                    JsonBody = new Body(new NonNullDictionary<string, object> {
                        { "status", Status },
                        { "error", StatusMessage },
                        { "reason", StatusReason }
                    });
                    this["Content-Type"] = "application/json";
                }
            }
        }

        public string StatusMessage { get; private set; }

        public string StatusReason { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public Body JsonBody { 
            get {
                return _jsonBody;
            }
            set {
                _binaryBody = null;
                _jsonBody = value;
                _multipartWriter = null;
            }
        }
        private Body _jsonBody;

        public IEnumerable<byte> BinaryBody {
            get {
                return _binaryBody;
            }
            set { 
                _binaryBody = value;
                _jsonBody = null;
                _multipartWriter = null;
            }
        }
        private IEnumerable<byte> _binaryBody;

        public MultipartWriter MultipartWriter
        {
            get {
                return _multipartWriter;
            }
            set { 
                _binaryBody = null;
                _jsonBody = null;
                _multipartWriter = value;
                Headers["Content-Type"] = value.ContentType;
            }
        }
        private MultipartWriter _multipartWriter;

        public string BaseContentType { 
            get {
                string type = Headers.Get("Content-Type");
                if (type == null) {
                    return null;
                }

                return type.Split(';')[0];
            }
        }

        public CouchbaseLiteResponse(ICouchbaseListenerContext context) {
            Headers = new Dictionary<string, string>();
            InternalStatus = StatusCode.Ok;
            _context = context.HttpContext;
        }

        public ICouchbaseResponseState AsDefaultState()
        {
            return new DefaultCouchbaseResponseState(this);
        }

        public bool WriteToContext()
        {
            if (Chunked) {
                Log.E(TAG, "Attempt to send one-shot data when in chunked mode");
                return false;
            }

            bool syncWrite = true;
            if (JsonBody != null) {
                if (!Headers.ContainsKey("Content-Type")) {
                    var accept = _context.Request.Headers["Accept"];
                    if (accept != null) {
                        if (accept.Contains("*/*") || accept.Contains("application/json")) {
                            _context.Response.AddHeader("Content-Type", "application/json");
                        } else if (accept.Contains("text/plain")) {
                            _context.Response.AddHeader("Content-Type", "text/plain; charset=utf-8");
                        } else {
                            Reset();
                            _context.Response.AddHeader("Content-Type", "application/json");
                            InternalStatus = StatusCode.NotAcceptable;
                        }
                    }
                }
                    
                _context.Response.ContentEncoding = Encoding.UTF8;
                var json = JsonBody.GetJson().ToArray();
                _context.Response.ContentLength64 = json.Length;
                if(!WriteToStream(json)) {
                    return false;
                }
            } else if (BinaryBody != null) {
                this["Content-Type"] = BaseContentType;
                _context.Response.ContentEncoding = Encoding.UTF8;
                var data = BinaryBody.ToArray();
                _context.Response.ContentLength64 = data.LongLength;
                if (!WriteToStream(data)) {
                    return false;
                }
            } else if (MultipartWriter != null) {
                MultipartWriter.WriteAsync(_context.Response.OutputStream).ContinueWith(t =>
                {
                    if(t.IsCompleted && t.Result) {
                        TryClose();
                    } else {
                        Log.E(TAG, "Multipart async write did not finish properly");
                    }
                });
                syncWrite = false;
            }

            if (syncWrite) {
                TryClose();
            }

            return true;
        }

        public bool WriteData(IEnumerable<byte> data, bool finished)
        {
            if (!Chunked) {
                Log.E(TAG, "Attempt to send streaming data when not in chunked mode");
                return false;
            }

            if (!WriteToStream(data.ToArray())) {
                return false;
            }

            if (finished) {
                TryClose();
            }

            return true;
        }

        // Send a JSON object followed by a newline without closing the connection.
        // Used by the continuous mode of _changes and _active_tasks.
        public bool SendContinuousLine(IDictionary<string, object> changesDict, ChangesFeedMode mode)
        {
            if (!Chunked) {
                Log.E(TAG, "Attempt to send streaming data when not in chunked mode");
                return false;
            }

            var json = Manager.GetObjectMapper().WriteValueAsBytes(changesDict).ToList();
            if (mode == ChangesFeedMode.EventSource) {
                // https://developer.mozilla.org/en-US/docs/Server-sent_events/Using_server-sent_events#Event_stream_format
                json.InsertRange(0, Encoding.UTF8.GetBytes("data: "));
                json.AddRange(Encoding.UTF8.GetBytes("\n\n"));
            } else {
                json.AddRange(Encoding.UTF8.GetBytes("\n"));
            }

            bool written = WriteData(json, false);
            return written;
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

        public void SetMultipartBody(IList<object> parts, string type)
        {
            var mp = new MultipartWriter(type, null);
            object nextPart;
            foreach (var part in parts) {
                if (!(part is IEnumerable<byte>)) {
                    nextPart = Manager.GetObjectMapper().WriteValueAsBytes(part);
                    mp.SetNextPartHeaders(new Dictionary<string, string> { { "Content-Type", "application/json" } });
                } else {
                    nextPart = part;
                }

                mp.AddData((IEnumerable<byte>)nextPart);
            }

            MultipartWriter = mp;
        }

        private bool Validate()
        {
            var accept = _context.Request.Headers["Accept"];
            if (accept != null && !accept.Contains("*/*")) {
                var responseType = BaseContentType;
                if (responseType != null && !accept.Contains(responseType)) {
                    Log.D(TAG, "Unacceptable type {0} (Valid: {1})", BaseContentType, _context.Request.AcceptTypes);
                    Reset();
                    InternalStatus = StatusCode.NotAcceptable;
                    return false;
                }
            }

            return true;
        }

        private bool WriteToStream(byte[] data) {
            try {
                _context.Response.OutputStream.Write(data, 0, data.Length);
                _context.Response.OutputStream.Flush();
                return true;
            } catch(IOException) {
                Log.W(TAG, "Error writing to HTTP response stream");
                return false;
            }

            return true;
        }

        private void TryClose() {
            try {
                _context.Response.Close();
            } catch(IOException) {
                Log.W(TAG, "Error closing HTTP response stream");
            }
        }

        public void Reset()
        {
            _context.Response.
            Headers.Clear();
            _context.Response.Headers.Clear();
            JsonBody = null;
            BinaryBody = null;
            _headersWritten = false;
        }

        public string this[string key]
        {
            get { return Headers == null ? null : Headers[key]; }
            set { Headers[key] = value; }
        }
    }
}

