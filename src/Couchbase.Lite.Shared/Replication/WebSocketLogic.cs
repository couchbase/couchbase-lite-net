//
// WebSocketLogic.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace Couchbase.Lite.Internal
{
    internal sealed class WebSocketLogic : IChangeTrackerResponseLogic
    {
        private ManualResetEventSlim _pauseWait = new ManualResetEventSlim(true);
        private bool _caughtUp;
        private ChunkedChanges _changeProcessor;

        public TimeSpan Heartbeat { get; set; }
        public Action OnCaughtUp { get; set; }
        public Action<IDictionary<string, object>> OnChangeFound { get; set; }
        public Action<Exception> OnFinished { get; set; }

        private ChangeTrackerResponseCode ProcessGzippedStream(Stream stream)
        {
            if (_changeProcessor == null) {
                _changeProcessor = new ChunkedChanges(ChunkStyle.ByArray, true);
                SetupChangeProcessorCallback();
            }

            _changeProcessor.AddData(stream.ReadAllBytes());
            return ChangeTrackerResponseCode.Normal;
        }

        private ChangeTrackerResponseCode ProcessRegularStream(Stream stream)
        {
            if (_changeProcessor == null) {
                _changeProcessor = new ChunkedChanges(ChunkStyle.ByArray, false);
                SetupChangeProcessorCallback();
            }

            if(stream.Length == 3) { // +1 for the first type ID
                if(!_caughtUp) {
                    _caughtUp = true;
                    if (OnCaughtUp != null) {
                        OnCaughtUp();
                    }

                    return ChangeTrackerResponseCode.Normal;
                }
            }

            _changeProcessor.AddData(stream.ReadAllBytes());

            return ChangeTrackerResponseCode.Normal;
        }

        private void SetupChangeProcessorCallback()
        {
            _changeProcessor.ChunkFound += (sender, args) => 
            {
                if(OnChangeFound != null) {
                    OnChangeFound(args);
                }
            };

            _changeProcessor.Finished += (sender, args) => 
            {
                if(OnFinished != null) {
                    OnFinished(args);
                }
            };
        }

        #region IChangeTrackerResponseLogic

        public ChangeTrackerResponseCode ProcessResponseStream(Stream stream)
        {
            var type = stream.ReadByte();
            if(type == 1) {
                return ProcessRegularStream(stream);
            } else {
                return ProcessGzippedStream(stream);
            }
        }

        public void Pause()
        {
            _pauseWait.Reset();
        }

        public void Resume()
        {
            _pauseWait.Set();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _pauseWait.Dispose();
        }

        #endregion
        
    }
}

