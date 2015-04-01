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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Util;
using System.Diagnostics;
using System.Threading;

namespace Couchbase.Lite.Support
{
    public class MultiStreamWriter : IDisposable
    {
        private const int DEFAULT_BUFFER_SIZE = 32768;
        private const string TAG = "MultiStreamWriter";

        private IList _inputs = new ArrayList();
        private int _nextInputIndex;
        private Stream _currentInput;
        private Stream _output;
        private ManualResetEventSlim _mre;
        private bool _isDisposed;

        protected long _totalBytesWritten;

        public long Length { get; protected set; }

        public bool IsOpen { 
            get
            {
                return _mre != null && !_mre.IsSet && !_isDisposed;
            }
        }

        public void AddStream(Stream stream, long length)
        {
            AddInput(stream, length);
        }

        public void AddStream(Stream stream)
        {
            Log.D(TAG, "Adding stream of unknown length: {0}", stream);
            _inputs.Add(stream);
            Length = -1; // length is now unknown
        }

        public void AddData(IEnumerable<byte> data)
        {
            if (!data.Any()) {
                return;
            }

            AddInput(data, data.LongCount());
        }

        public bool AddFileUrl(Uri fileUrl)
        {
            FileInfo info;
            try {
                info = new FileInfo(fileUrl.AbsolutePath);
            } catch(Exception) {
                return false;
            }

            AddInput(fileUrl, info.Length);
            return true;
        }

        public bool AddFile(string path)
        {
            return AddFileUrl(new Uri(path));
        }

        public Stream OpenForInput()
        {
            if (_isDisposed) {
                throw new ObjectDisposedException("MultiStreamWriter");
            }

            if (_output != null) {
                return _output;
            }

            Debug.Assert(_output == null, "Already open");
            _output = new MemoryStream();
            Opened();
            return _output;
        }

        public void OpenForOutputTo(Stream output)
        {
            if (_isDisposed) {
                throw new ObjectDisposedException("MultiStreamWriter");
            }

            Debug.Assert(output != null);
            Debug.Assert(_output == null, "Already open");
            _output = output;
            Opened();
        }

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

        public IEnumerable<byte> AllOutput()
        {
            var ms = new MemoryStream();
            OpenForOutputTo(ms);
            if (!_mre.Wait(TimeSpan.FromSeconds(30))) {
                Log.W(TAG, "Unable to get output!");
                return null;
            }

            return ms.ToArray();
        }

        protected virtual void Opened()
        {
            _totalBytesWritten = 0;
            _mre = new ManualResetEventSlim();
            StartWriting();
        }

        protected virtual void AddInput(object input, long length)
        {
            _inputs.Add(input);
            Length += length;
        }

        private void StartWriting()
        {
            var gotInput = OpenNextInput();
            if (gotInput) {
                _currentInput.CopyToAsync(_output).ContinueWith(t => StartWriting());
            } else {
                _mre.Set();
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
                return new FileStream(fileUri.AbsolutePath, FileMode.Open, FileAccess.Read);
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

        public void Dispose()
        {
            Close();
        }
    }
}

