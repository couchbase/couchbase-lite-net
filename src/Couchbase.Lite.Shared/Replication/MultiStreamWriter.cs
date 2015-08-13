//
//  MultiStreamWriter.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

using Couchbase.Lite.Util;
using System.Threading.Tasks;

#if NET_3_5
using Rackspace.Threading;
#endif

namespace Couchbase.Lite.Support
{

    /// <summary>
    /// An object that can write from multiple sources into one stream
    /// </summary>
    public class MultiStreamWriter : IDisposable
    {

        #region Constants

        private const int DEFAULT_BUFFER_SIZE = 32768;
        private const string TAG = "MultiStreamWriter";

        #endregion

        #region Variables

        private IList _inputs = new ArrayList();
        private int _nextInputIndex;
        private Stream _currentInput;
        private Stream _output;
        private ManualResetEventSlim _mre;
        private bool _isDisposed;

        /// <summary>
        /// The total bytes written so far.
        /// </summary>
        protected long _totalBytesWritten;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the total length of the data, if known
        /// </summary>
        /// <value>The length.</value>
        public long Length { get; protected set; }

        /// <summary>
        /// Gets whether or not the writer is open
        /// </summary>
        /// <value><c>true</c> if this instance is open; otherwise, <c>false</c>.</value>
        public bool IsOpen { 
            get {
                return _mre != null && !_mre.IsSet && !_isDisposed;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a stream with a known length to be processed
        /// </summary>
        /// <param name="stream">The stream to be processed.</param>
        /// <param name="length">The length of the stream.</param>
        public void AddStream(Stream stream, long length)
        {
            AddInput(stream, length);
        }

        /// <summary>
        /// Adds a stream with an unknown length to be processed
        /// </summary>
        /// <param name="stream">The stream to be processed.</param>
        public void AddStream(Stream stream)
        {
            Log.D(TAG, "Adding stream of unknown length: {0}", stream);
            _inputs.Add(stream);
            Length = -1; // length is now unknown
        }

        /// <summary>
        /// Adds a blob to be processed
        /// </summary>
        /// <param name="data">The blob to be processed</param>
        public void AddData(IEnumerable<byte> data)
        {
            if (!data.Any()) {
                return;
            }

            AddInput(data, data.LongCount());
        }

        /// <summary>
        /// Adds a file URL to be processed
        /// </summary>
        /// <returns><c>true</c>, if file URL was added, <c>false</c> otherwise.</returns>
        /// <param name="fileUrl">The file URL to read data from</param>
        public bool AddFileUrl(Uri fileUrl)
        {
            FileInfo info;
            try {
                info = new FileInfo(Uri.UnescapeDataString(fileUrl.AbsolutePath));
            } catch(Exception) {
                return false;
            }

            AddInput(fileUrl, info.Length);
            return true;
        }

        /// <summary>
        /// Adds a file path to be processed
        /// </summary>
        /// <returns><c>true</c>, if the file was added, <c>false</c> otherwise.</returns>
        /// <param name="path">The path of the file to read data from.</param>
        public bool AddFile(string path)
        {
            return AddFileUrl(new Uri(path));
        }

        /// <summary>
        /// Asynchronously writes the accumulated data to an output stream
        /// </summary>
        /// <returns>An awaitable task whose result indicates success or failure</returns>
        /// <param name="output">The output stream to write to</param>
        public Task<bool> WriteAsync(Stream output)
        {
            if (_isDisposed) {
                throw new ObjectDisposedException("MultiStreamWriter");
            }

            Debug.Assert(output != null);
            Debug.Assert(_output == null, "Already open");
            _output = output;
            Opened();

            if (_mre == null) {
                _mre = new ManualResetEventSlim();
            }

            var tcs = new TaskCompletionSource<bool>();
            ThreadPool.RegisterWaitForSingleObject(_mre.WaitHandle, (o, timeout) => tcs.SetResult(!timeout),
                null, TimeSpan.FromSeconds(30), true);

            return tcs.Task;
        }

        /// <summary>
        /// Closes the output stream and stops writing
        /// </summary>
        public void Close()
        {
            if (_isDisposed) {
                return;
            }

            _isDisposed = true;
            Log.D(TAG, "Closed");
            if (_output != null) {
                _output.Close();
                _output = null;
            }

            if (_currentInput != null) {
                _currentInput.Close();
                _currentInput = null;
            }

            for (int i = _nextInputIndex; i < _inputs.Count; i++) {
                var nextStream = _inputs[i] as Stream;
                if (nextStream != null) {
                    nextStream.Close();
                }
            }

            _nextInputIndex = 0;
        }

        /// <summary>
        /// Synchronously retrieves all of the accumulated data as a blob
        /// </summary>
        /// <returns>All the accumulated data</returns>
        public IEnumerable<byte> AllOutput()
        {
            using (var ms = new MemoryStream()) {
                if (!WriteAsync(ms).Wait(TimeSpan.FromSeconds(30))) {
                    Log.W(TAG, "Unable to get output!");
                    return null;
                }

                return ms.ToArray();
            }
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Called when the output stream is opened
        /// </summary>
        protected virtual void Opened()
        {
            _totalBytesWritten = 0;
            _mre = new ManualResetEventSlim();
            StartWriting();
        }

        /// <summary>
        /// Add a piece of input and length to the accumulated data
        /// </summary>
        /// <param name="input">The input to add.</param>
        /// <param name="length">The length of the input.</param>
        protected virtual void AddInput(object input, long length)
        {
            _inputs.Add(input);
            Length += length;
        }

        #endregion

        #region Private Methods

        private void StartWriting()
        {
            var gotInput = OpenNextInput();
            if (gotInput) {
                _currentInput.CopyToAsync(_output).ContinueWith(t => StartWriting());
            } else {
                _mre.Set();
                _mre.Dispose();
                _mre = null;
            }
        }

        private Stream StreamForInput(object input)
        {
            var data = input as IEnumerable<byte>;
            if (data != null) {
                return new MemoryStream(data.ToArray());
            }

            var fileUri = input as Uri;
            if (fileUri != null && fileUri.IsFile) {
                return new FileStream(Uri.UnescapeDataString(fileUri.AbsolutePath), FileMode.Open, FileAccess.Read);
            }

            var stream = input as Stream;
            if (stream != null) {
                return stream;
            }

            Debug.Assert(false, String.Format("Invalid input class {0} for MultiStreamWriter", input.GetType()));
            return null;
        }

        private bool OpenNextInput()
        {
            if (_currentInput != null) {
                _currentInput.Close();
                _currentInput = null;
            }

            if (_nextInputIndex < _inputs.Count) {
                _currentInput = StreamForInput(_inputs[_nextInputIndex]);
                _nextInputIndex++;
                return true;
            }

            return false;
        }

        #endregion

        #region IDisposable
        #pragma warning disable 1591

        public void Dispose()
        {
            Close();
        }

        #pragma warning restore 1591
        #endregion

    }
}

