//
//  MultipartWriter.cs
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
using System.Text;

namespace Couchbase.Lite.Support
{

    /// <summary>
    /// An object that can write multipart HTTP responses
    /// </summary>
    public sealed class MultipartWriter : MultiStreamWriter
    {

        #region Constants

        private const int MIN_DATA_LENGTH_TO_COMPRESS = 100;

        #endregion

        #region Variables

        private IEnumerable<byte> _finalBoundary;
        private SortedDictionary<string, string> _nextPartsHeaders;
        private string _contentType;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the content-type for use in the multipart headers
        /// </summary>
        public string ContentType { 
            get {
                return String.Format("{0}; boundary=\"{1}\"", _contentType, Boundary);
            }
        }

        /// <summary>
        /// Gets the boundary ID of this multipart response
        /// </summary>
        public string Boundary { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="contentType">The content type of the multipart response</param>
        /// <param name="boundary">The boundary ID to use between parts</param>
        public MultipartWriter(string contentType, string boundary)
        {
            _contentType = contentType;
            Boundary = boundary ?? Misc.CreateGUID();

            // Account for the final boundary to be written by -opened. Add its length now, because the
            // client is probably going to ask for my .length *before* it calls -open.
            string finalBoundaryString = String.Format("\r\n--{0}--", Boundary);
            _finalBoundary = Encoding.UTF8.GetBytes(finalBoundaryString);
            Length += _finalBoundary.Count();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Sets the headers for the next part in the response
        /// </summary>
        /// <param name="nextPartHeaders">The next headers</param>
        public void SetNextPartHeaders(IDictionary<string, string> nextPartHeaders)
        {
            _nextPartsHeaders = nextPartHeaders != null ? new SortedDictionary<string, string>(nextPartHeaders) : null;
        }

        /// <summary>
        /// GZips data and adds it to the response
        /// </summary>
        /// <param name="data">The uncompressed data</param>
        public void AddGZippedData(IEnumerable<byte> data)
        {
            var realized = data.ToArray();
            if (realized.Length >= MIN_DATA_LENGTH_TO_COMPRESS) {
                var compressed = realized.Compress();
                if (compressed.Count() < realized.Length) {
                    data = compressed;
                    _nextPartsHeaders["Content-Encoding"] = "gzip";
                }
            }
            AddData(data);
        }

        #endregion

        #region Overrides
        #pragma warning disable 1591

        // MultiStreamWriter
        protected override void Opened()
        {
            if (_finalBoundary != null) {
                // Append the final boundary
                base.AddInput(_finalBoundary, 0);
                // _length was already adjusted for this in constructor
                _finalBoundary = null;
            }

            base.Opened();
        }

        // MultiStreamWriter
        protected override void AddInput(object input, long length)
        {
            StringBuilder headers = new StringBuilder(String.Format("\r\n--{0}\r\n", Boundary));
            headers.AppendFormat("Content-Length: {0}\r\n", length);
            if (_nextPartsHeaders != null) {
                foreach (var entry in _nextPartsHeaders) {
                    // Strip any CR or LF in the header value. This isn't real quoting, just enough to ensure
                    // a spoofer can't add bogus headers by putting CRLF into a header value!
                    headers.AppendFormat("{0}: {1}\r\n", entry.Key, entry.Value.Replace("\r", string.Empty).Replace("\n", string.Empty));
                }
            }

            headers.Append("\r\n");
            var separator = Encoding.UTF8.GetBytes(headers.ToString());
            SetNextPartHeaders(null);

            base.AddInput(separator, separator.LongLength);
            base.AddInput(input, length);
        }

        #pragma warning restore 1591
        #endregion
    }
}

