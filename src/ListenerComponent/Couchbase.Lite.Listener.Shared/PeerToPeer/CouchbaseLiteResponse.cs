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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Couchbase.Lite.Replicator;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using Sharpen;


namespace Couchbase.Lite.Listener
{
    /// <summary>
    /// A class encapsulating all the information needed to write a response
    /// to a client in various ways
    /// </summary>
    public sealed class CouchbaseLiteResponse
    {

        #region Constants

        private const string TAG = "CouchbaseLiteResponse";
        private static readonly Regex RANGE_HEADER_REGEX = new Regex("^bytes=(\\d+)?-(\\d+)?$");

        #endregion

        #region Member Variables

        private readonly ICouchbaseResponseWriter _responseWriter;
        private readonly NameValueCollection _requestHeaders;
        private readonly string _requestMethod;
        private bool _headersWritten;

        #endregion

        #region Properties

        /// <summary>
        /// The status the response will contain
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// Whether or not the response is written piece by piece or all at once
        /// </summary>
        public bool Chunked { 
            get { return _responseWriter.IsChunked; }
            set { 
                if (_headersWritten) {
                    Log.E(TAG, "Attempting to changed Chunked after headers written, ignoring");
                    return;
                }

                _responseWriter.IsChunked = value; 
            }
        }

        /// <summary>
        /// Sets the Couchbase Lite status of the response object.  This will
        /// implicitly set the <see cref="Status"/> property to an appropriate
        /// value, as well as the <see cref="JsonBody"/>, if needed. 
        /// </summary>
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
        private StatusCode _internalStatus;

        /// <summary>
        /// The message corresponding to the status of the response
        /// </summary>
        public string StatusMessage { get; private set; }

        /// <summary>
        /// The reason for the status of the status, if known
        /// </summary>
        /// <value>The status reason.</value>
        public string StatusReason { get; set; }

        /// <summary>
        /// The headers of the response
        /// </summary>
        /// <value>The headers.</value>
        public IDictionary<string, string> Headers { get; set; }

        /// <summary>
        /// The body of the response, as JSON (will clear
        /// the values of the other body objects)
        /// </summary>
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

        /// <summary>
        /// The body of the response, as raw binary (will clear
        /// the values of the other body objects)
        /// </summary>
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

        /// <summary>
        /// The multipart writer to use to write the 
        /// response body (will clear the JSON and binary
        /// bodies)
        /// </summary>
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

        /// <summary>
        /// Gets the content type of the response, as set
        /// in the headers
        /// </summary>
        public string BaseContentType { 
            get {
                string type = Headers.Get("Content-Type");
                if (type == null) {
                    return null;
                }

                return type.Split(';')[0];
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="requestMethod">The method used in the request</param>
        /// <param name="requestHeaders">The headers sent with the request</param>
        /// <param name="responseWriter">The object responsible for writing the response to the client</param>
        public CouchbaseLiteResponse(string requestMethod, NameValueCollection requestHeaders, ICouchbaseResponseWriter responseWriter) {
            Headers = new Dictionary<string, string>();
            InternalStatus = StatusCode.Ok;
            _requestHeaders = requestHeaders;
            _responseWriter = responseWriter;
            _requestMethod = requestMethod;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Converts the response to the default state object (most of the time
        /// this is used, but sometimes a request is async or needs to be continuous)
        /// </summary>
        /// <returns>The converted response</returns>
        public ICouchbaseResponseState AsDefaultState()
        {
            return new DefaultCouchbaseResponseState(this);
        }

        /// <summary>
        /// Writes the response in one-step, and then closes the response stream
        /// </summary>
        /// <returns><c>true</c>, if written successfully, <c>false</c> otherwise.</returns>
        public bool WriteToContext()
        {
            bool syncWrite = true;
            if (JsonBody != null) {
                if (!Chunked && !Headers.ContainsKey("Content-Type")) {
                    var accept = _requestHeaders["Accept"];
                    if (accept != null) {
                        if (accept.Contains("*/*") || accept.Contains("application/json")) {
                            _responseWriter.AddHeader("Content-Type", "application/json");
                        } else if (accept.Contains("text/plain")) {
                            _responseWriter.AddHeader("Content-Type", "text/plain; charset=utf-8");
                        } else {
                            Reset();
                            _responseWriter.AddHeader("Content-Type", "application/json");
                            InternalStatus = StatusCode.NotAcceptable;
                        }
                    }
                }
                    
                var json = JsonBody.AsJson().ToArray();
                if (!Chunked) {
                    _responseWriter.ContentEncoding = Encoding.UTF8;
                    _responseWriter.ContentLength = json.Length;
                }

                if(!WriteToStream(json)) {
                    return false;
                }
            } else if (BinaryBody != null) {
                this["Content-Type"] = BaseContentType;
                _responseWriter.ContentEncoding = Encoding.UTF8;
                var data = BinaryBody.ToArray();
                if (!Chunked) {
                    _responseWriter.ContentLength = data.LongLength;
                }

                if (!WriteToStream(data)) {
                    return false;
                }
            } else if (MultipartWriter != null) {
                MultipartWriter.WriteAsync(_responseWriter.OutputStream).ContinueWith(t =>
                {
                    if(t.IsCompleted && t.Result) {
                        TryClose();
                    } else {
                        Log.I(TAG, "Multipart async write did not finish properly");
                    }
                });
                syncWrite = false;
            }

            if (syncWrite) {
                TryClose();
            }

            return true;
        }

        /// <summary>
        /// Write a piece of data to the response, and optionally close it
        /// </summary>
        /// <returns><c>true</c>, if data was written, <c>false</c> otherwise.</returns>
        /// <param name="data">The data to write</param>
        /// <param name="finished">If set to <c>true</c> close the response</param>
        public bool WriteData(IEnumerable<byte> data, bool finished)
        {
            if (!Chunked) {
                Log.W(TAG, "Attempt to send streaming data when not in chunked mode");
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

        /// <summary>
        /// Send a JSON object followed by a newline without closing the connection.
        /// Used by the continuous mode of _changes and _active_tasks.
        /// </summary>
        /// <returns><c>true</c>, if data was written, <c>false</c> otherwise.</returns>
        /// <param name="changesDict">The metadata dictionary to write</param>
        /// <param name="mode">The mode of the response (event source vs continuous)</param>
        public bool SendContinuousLine(IDictionary<string, object> changesDict, ChangesFeedMode mode)
        {
            if (!Chunked) {
                Log.W(TAG, "Attempt to send streaming data when not in chunked mode");
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

        /// <summary>
        /// Processes the Content-Range header of the request and if possible sends the
        /// corresponding data
        /// </summary>
        /// <remarks>
        /// Range Request: http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.35
        /// Content-Range: http://www.w3.org/Protocols/rfc2616/rfc2616-sec14.html#sec14.16
        /// </remarks>
        public void ProcessRequestRanges()
        {
            if (_responseWriter.StatusCode != 200 || (_requestMethod != "GET" 
                && _requestMethod != "HEAD") || _binaryBody == null) {
                return;
            }

            Headers["Accept-Range"] = "bytes";

            int bodyLength = _binaryBody.Count();
            if (bodyLength == 0) {
                return;
            }
                
            var rangeHeader = _requestHeaders["Range"];
            if (rangeHeader == null) {
                return;
            }

            var match = RANGE_HEADER_REGEX.Match(rangeHeader);
            if (match == null) {
                Log.W(TAG, "Invalid request Range header value: '{0}'", rangeHeader);
                return;
            }

            var fromStr = match.Groups[1].Value;
            var toStr = match.Groups[2].Value;

            int start = 0, end = 0;
            // Now convert those into the integer offsets (remember that 'to' is inclusive):
            if (fromStr.Length > 0) {
                Int32.TryParse(fromStr, out start);
                if (toStr.Length > 0) {
                    Int32.TryParse(toStr, out end);
                    end = Math.Min(bodyLength - 1, end);
                } else {
                    end = bodyLength - 1;
                }

                if (end < start) {
                    return; //Invalid range
                }
            } else if (toStr.Length > 0) {
                end = bodyLength - 1;
                Int32.TryParse(toStr, out start);
                start = bodyLength - Math.Min(start, bodyLength);
            } else {
                return; // "-" is an invalid range
            }
             
            if (end >= bodyLength || end < start) {
                Status = 416; // Requested Range Not Satisfiable
                var contentRangeStr = String.Format("bytes */{0}", bodyLength);
                Headers["Content-Range"] = contentRangeStr;
                _binaryBody = null;
                return;
            }

            if (start == 0 && end == bodyLength - 1) {
                return; // No-op; entire body still causes a 200 response
            }

            var data = _binaryBody.Skip(start).Take(end - start + 1).ToArray();
            _binaryBody = data;

            var contentRange = String.Format("bytes {0}-{1}/{2}", start, end, bodyLength);
            Headers["Content-Range"] = contentRange;
            Status = 206; // Partial Content
            Log.D(TAG, "Content-Range: {0}", contentRange);
        }

        /// <summary>
        /// Write the headers to the HTTP response
        /// </summary>
        public void WriteHeaders()
        {
            if (_headersWritten) {
                return;
            }

            _headersWritten = true;
            Validate();
            this["Server"] = "Couchbase Lite " + Manager.VersionString;
            if (this.Status == 200 && _requestMethod.Equals("GET") || _requestMethod.Equals("HEAD")) {
                if (!Headers.ContainsKey("Cache-Control")) {
                    this["Cache-Control"] = "must-revalidate";
                }
            }

            _responseWriter.StatusCode = Status;
            _responseWriter.StatusDescription = StatusMessage;

            foreach (var header in Headers) {
                _responseWriter.AddHeader(header.Key, header.Value);
            }
        }

        /// <summary>
        /// Creates and sets a multipart writer given the list of data to send
        /// </summary>
        /// <param name="parts">The list of data to transmit</param>
        /// <param name="type">The base type of the multipart writer</param>
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

        /// <summary>
        /// Resets the response to its initial state
        /// </summary>
        public void Reset()
        {
            _responseWriter.ClearHeaders();
            JsonBody = null;
            BinaryBody = null;
            MultipartWriter = null;
            _headersWritten = false;
            InternalStatus = StatusCode.Ok;
        }

        /// <summary>
        /// Gets or sets the header using the specified name
        /// </summary>
        /// <param name="key">The header name</param>
        public string this[string key]
        {
            get { return Headers == null ? null : Headers[key]; }
            set { Headers[key] = value; }
        }

        #endregion

        #region Private Methods

        //Makes sure that the request includes a valid content type in its Accept header
        private bool Validate()
        {
            var accept = _requestHeaders["Accept"];
            if (accept != null && !accept.Contains("*/*")) {
                var responseType = BaseContentType;
                if (responseType != null && !accept.Contains(responseType)) {
                    Log.D(TAG, "Unacceptable type {0} (Valid: {1})", BaseContentType, _requestHeaders["Accept"]);
                    Reset();
                    InternalStatus = StatusCode.NotAcceptable;
                    return false;
                }
            }

            return true;
        }

        //Attempt to write to the response stream
        private bool WriteToStream(byte[] data) {
            try {
                _responseWriter.OutputStream.Write(data, 0, data.Length);
                _responseWriter.OutputStream.Flush();
                return true;
            } catch(IOException) {
                Log.W(TAG, "Error writing to HTTP response stream");
                return false;
            } catch(ObjectDisposedException) {
                Log.I(TAG, "Data written after disposal"); // This is normal for hanging connections who write until the client disconnects
                return false;
            }
        }

        //Attempt to close the response
        private void TryClose() {
            try {
                _responseWriter.Close();
            } catch(IOException) {
                Log.W(TAG, "Error closing HTTP response stream");
            } catch(ObjectDisposedException) {
                //swallow (already closed)
            }
        }

        #endregion
    }
}

