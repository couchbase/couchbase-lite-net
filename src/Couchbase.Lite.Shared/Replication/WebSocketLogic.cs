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
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal
{
    internal sealed class WebSocketLogic : IChangeTrackerResponseLogic
    {
        private static readonly string Tag = typeof(WebSocketLogic).Name;
        private ManualResetEventSlim _pauseWait = new ManualResetEventSlim(true);
        private bool _caughtUp;
        private ChunkedChanges _changeProcessor;

        public TimeSpan Heartbeat { get; set; }
        public Action OnCaughtUp { get; set; }
        public Action<IDictionary<string, object>> OnChangeFound { get; set; }
        public Action<Exception> OnFinished { get; set; }

        private ChangeTrackerResponseCode ProcessResponseStream(Stream stream, CancellationToken token, bool compressed)
        {
            if (_changeProcessor == null) {
                _changeProcessor = new ChunkedChanges(compressed, token, _pauseWait);
                SetupChangeProcessorCallback();
            }

            _changeProcessor.AddData(stream);
            return ChangeTrackerResponseCode.Normal;
        }


        private void SetupChangeProcessorCallback()
        {
            _changeProcessor.ChunkFound += (sender, args) => 
            {
                OnChangeFound?.Invoke(args);
            };

            _changeProcessor.Finished += (sender, args) => 
            {
                OnFinished?.Invoke(args);
            };

            _changeProcessor.OnCaughtUp += (sender, args) =>
            {
                if(!_caughtUp) {
                    _caughtUp = true;
                    OnCaughtUp?.Invoke();
                }
            };
        }

        #region IChangeTrackerResponseLogic

        public ChangeTrackerResponseCode ProcessResponseStream(Stream stream, CancellationToken token)
        {
            _pauseWait.Wait();
            var type = (ChangeTrackerMessageType)stream.ReadByte();
            if (type == ChangeTrackerMessageType.Plaintext || type == ChangeTrackerMessageType.GZip) {
                return ProcessResponseStream(stream, token, type == ChangeTrackerMessageType.GZip);
            } else if (type == ChangeTrackerMessageType.EOF) {
                if(_changeProcessor == null) {
                    if(!_caughtUp) {
                        _caughtUp = true;
                        OnCaughtUp?.Invoke();
                    }
                } else {
                    _changeProcessor.ScheduleCaughtUp();
                }

                return ChangeTrackerResponseCode.Normal;
            }

            Log.To.Sync.E(Tag, "Unknown response code {0}, returning failed status", type);
            return ChangeTrackerResponseCode.Failed;
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
            Misc.SafeDispose(ref _pauseWait);
            Misc.SafeDispose(ref _changeProcessor);
        }

        #endregion
        
    }
}

