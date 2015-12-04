//
// MultipartReader.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Couchbase.Lite.Support;
using Sharpen;

namespace Couchbase.Lite.Support
{
    internal class MultipartReader
    {

        #region Enums

        private enum MultipartReaderState
        {
            Uninitialized,
            AtStart,
            InPrologue,
            InBody,
            InHeaders,
            AtEnd,
            Failed
        }

        #endregion

        #region Member Variables

        private static readonly byte[] CRLF_CRLF = Encoding.UTF8.GetBytes("\r\n\r\n");
        private static readonly byte[] EOM_BYTES = Encoding.UTF8.GetBytes("--");

        private MultipartReader.MultipartReaderState state;
        private List<byte> _buffer;
        private readonly string _contentType;
        private byte[] _boundary;
        private IMultipartReaderDelegate _readerDelegate;
        private IDictionary<string, string> _headers;

        #endregion

        #region Properties

        public IEnumerable<byte> Boundary
        {
            get {
                return _boundary;
            }
        }

        public IEnumerable<byte> TrimmedBoundary
        {
            get {
                return new Couchbase.Lite.Util.ArraySegment<Byte>(_boundary, 2, _boundary.Length - 2);
            }
        }

        public bool Finished
        {
            get {
                return state == MultipartReader.MultipartReaderState.AtEnd;
            }
        }

        #endregion

        #region Constructors

        public MultipartReader(string contentType, IMultipartReaderDelegate readerDelegate)
        {
            _contentType = contentType;
            _readerDelegate = readerDelegate;
            _buffer = new List<Byte>(1024);
            state = MultipartReader.MultipartReaderState.AtStart;
            ParseContentType();
        }

        #endregion

        #region Public Methods

        public Range SearchFor(byte[] pattern, int start)
        {
            var searcher = new KMPMatch();
            var matchIndex = searcher.IndexOf(_buffer.ToArray(), pattern, start);
            return matchIndex != -1 
                ? new Range (matchIndex, pattern.Length) 
                    : new Range (matchIndex, 0);
        }

        public void AppendData(IEnumerable<byte> newData)
        {
            if (newData == null) {
                return;
            }

            var data = newData.ToArray();
            if (_buffer == null || data.Length == 0) {
                return;
            }

            _buffer.AddRange(data);

            MultipartReader.MultipartReaderState nextState;
            do {
                nextState = MultipartReader.MultipartReaderState.Uninitialized;
                var bufLen = _buffer.Count;
                switch (state) {
                    case MultipartReader.MultipartReaderState.AtStart:
                        // The entire message might start with a boundary without a leading CRLF.
                        var boundaryWithoutLeadingCRLF = TrimmedBoundary.ToArray();
                        if (bufLen >= boundaryWithoutLeadingCRLF.Length) {
                            // if (Arrays.equals(buffer.toByteArray(), boundaryWithoutLeadingCRLF)) {
                            if (Memcmp(_buffer.ToArray(), boundaryWithoutLeadingCRLF, boundaryWithoutLeadingCRLF.Length)) {
                                DeleteUpThrough(boundaryWithoutLeadingCRLF.Length);
                                nextState = MultipartReader.MultipartReaderState.InHeaders;
                            } else {
                                nextState = MultipartReader.MultipartReaderState.InPrologue;
                            }
                        }
                        break;
                    case MultipartReader.MultipartReaderState.InPrologue:
                    case MultipartReader.MultipartReaderState.InBody:
                        // Look for the next part boundary in the data we just added and the ending bytes of
                        // the previous data (in case the boundary string is split across calls)
                        if (bufLen < _boundary.Length) {
                            break;
                        }

                        var start = Math.Max(0, bufLen - data.Length - _boundary.Length);
                        var r = SearchFor(_boundary, start);

                        if (r.Length > 0) {
                            if (state == MultipartReader.MultipartReaderState.InBody) {
                                var dataToAppend = new byte[r.Location];
                                Array.Copy(_buffer.ToArray(), 0, dataToAppend, 0, dataToAppend.Length);
                                _readerDelegate.AppendToPart(dataToAppend);
                                _readerDelegate.FinishedPart();
                            }

                            DeleteUpThrough(r.Location + r.Length);
                            nextState = MultipartReader.MultipartReaderState.InHeaders;
                        } else {
                            TrimBuffer();
                        }
                        break;
                    case MultipartReader.MultipartReaderState.InHeaders:
                        // First check for the end-of-message string ("--" after separator):
                        if (bufLen >= 2 && Memcmp(_buffer.ToArray(), EOM_BYTES, 2)) {
                            state = MultipartReader.MultipartReaderState.AtEnd;
                            Close();
                            return;
                        }
                        // Otherwise look for two CRLFs that delimit the end of the headers:
                        var headerEnd = SearchFor(CRLF_CRLF, 0);
                        if (headerEnd.Length > 0) {
                            var headersBytes = new Couchbase.Lite.Util.ArraySegment<Byte>(_buffer.ToArray(), 0, headerEnd.Location); // <-- better?
                            var headersString = Encoding.UTF8.GetString(headersBytes.ToArray());
                            ParseHeaders(headersString);
                            DeleteUpThrough(headerEnd.Location + headerEnd.Length);
                            _readerDelegate.StartedPart(_headers);
                            nextState = MultipartReader.MultipartReaderState.InBody;
                        }
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected data after end of MIME body");

                }

                if (nextState != MultipartReader.MultipartReaderState.Uninitialized) {
                    state = nextState;
                }
            } while (nextState != MultipartReader.MultipartReaderState.Uninitialized && _buffer.Count > 0);
        }

        #endregion

        #region Private Methods

        private bool Memcmp(byte[] array1, byte[] array2, int len)
        {
            for (int i = 0; i < len; i++) {
                if (array1[i] != array2[i]) {
                    return false;
                }
            }

            return true;
        }

        private void ParseHeaders(string headersStr)
        {
            _headers = new Dictionary<String, String>();
            if (!string.IsNullOrEmpty (headersStr)) {
                headersStr = headersStr.Trim ();
                var tokenizer = headersStr.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var header in tokenizer) {
                    if (!header.Contains (":")) {
                        throw new ArgumentException ("Missing ':' in header line: " + header);
                    }
                    var headerTokenizer = header.Split(':');
                    var key = headerTokenizer[0].Trim ();
                    var value = headerTokenizer[1].Trim ();
                    _headers.Put (key, value);
                }
            }
        }

        private void DeleteUpThrough(int location)
        {
            var srcBuffer = _buffer.ToArray();
            var newBuffer = new byte[srcBuffer.Length - location];
            Array.Copy(srcBuffer, location, newBuffer, 0, newBuffer.Length);

            _buffer.Clear();
            _buffer.AddRange(newBuffer);
        }

        private void TrimBuffer()
        {
            int bufLen = _buffer.Count;
            int boundaryLen = _boundary.Length;
            if (bufLen > boundaryLen)
            {
                // Leave enough bytes in _buffer that we can find an incomplete boundary string
                var dataToAppend = new byte[bufLen - boundaryLen];
                Array.Copy(_buffer.ToArray(), 0, dataToAppend, 0, dataToAppend.Length);
                _readerDelegate.AppendToPart(dataToAppend);
                DeleteUpThrough(bufLen - boundaryLen);
            }
        }

        private void Close()
        {
            _buffer = null;
            _boundary = null;
        }

        private void ParseContentType()
        {
            // ContentType will look like "multipart/foo; boundary=bar"
            // But there may be other ';'-separated params, and the boundary string may be quoted.
            // This is really not a full MIME type parser, but should work well enough for our needs.
            var tokenizer = _contentType.Split(';');
            bool first = true;
            foreach (var token in tokenizer) {
                string param = token.Trim();
                if (first) {
                    if (!param.StartsWith("multipart/", StringComparison.InvariantCultureIgnoreCase)) {
                        throw new ArgumentException(_contentType + " does not start with multipart/");
                    }

                    first = false;
                }  else {
                    if (param.StartsWith("boundary=", StringComparison.InvariantCultureIgnoreCase)) {
                        var tempBoundary = param.Substring(9);
                        if (tempBoundary.StartsWith("\"", StringComparison.InvariantCultureIgnoreCase)) {
                            if (tempBoundary.Length < 2 || !tempBoundary.EndsWith("\"", StringComparison.InvariantCultureIgnoreCase)) {
                                throw new ArgumentException(_contentType + " is not valid");
                            }

                            tempBoundary = tempBoundary.Substring(1, tempBoundary.Length - 2);
                        }

                        if (tempBoundary.Length < 1) {
                            throw new ArgumentException(_contentType + " has zero-length boundary");
                        }

                        tempBoundary = string.Format("\r\n--{0}", tempBoundary);
                        _boundary = Encoding.UTF8.GetBytes(tempBoundary);
                        break;
                    }
                }
            }

            #endregion
        }
    }

    internal class KMPMatch
    {
        public int IndexOf(byte[] data, byte[] pattern, int dataOffset)
        {
            int[] failure = ComputeFailure(pattern);
            int j = 0;
            if (data.Length == 0) {
                return -1;
            }

            int dataLength = data.Length;
            int patternLength = pattern.Length;
            for (int i = dataOffset; i < dataLength; i++) {
                while (j > 0 && pattern[j] != data[i]) {
                    j = failure[j - 1];
                }

                if (pattern[j] == data[i]) {
                    j++;
                }

                if (j == patternLength) {
                    return i - patternLength + 1;
                }
            }

            return -1;
        }

        // Computes the failure function using a boot-strapping process,
        // where the pattern is matched against itself.
        private int[] ComputeFailure(byte[] pattern)
        {
            int[] failure = new int[pattern.Length];
            int j = 0;
            for (int i = 1; i < pattern.Length; i++) {
                while (j > 0 && pattern[j] != pattern[i]) {
                    j = failure[j - 1];
                }

                if (pattern[j] == pattern[i]) {
                    j++;
                }

                failure[i] = j;
            }

            return failure;
        }
    }
}