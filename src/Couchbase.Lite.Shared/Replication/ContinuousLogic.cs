//
// ContinuousOrWebSocketLogic.cs
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

#if false
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Lite.Internal
{
    internal sealed class ContinuousLogic : IChangeTrackerResponseLogic
    {
        private bool _caughtUp;
        private ManualResetEventSlim _pauseWait = new ManualResetEventSlim(true);

        public TimeSpan Heartbeat { get; set; }

        public Action OnCaughtUp { get; set; }

        public Action<IDictionary<string, object>> OnChangeFound { get; set; }

        public Action<Exception> OnFinished { get; set; }

        public ChangeTrackerResponseCode ProcessResponseStream(Stream stream)
        {
            List<byte> parseBuffer = new List<byte>();
            try {
                var nextByte = stream.ReadByte();
                while(nextByte != -1) {
                    while(nextByte != (byte)'\n') {
                        parseBuffer.Add((byte)nextByte);
                        nextByte = stream.ReadByte();
                    }

                    if(parseBuffer.Count == 0) {
                        if(!_caughtUp && OnCaughtUp != null) {
                            OnCaughtUp();
                            _caughtUp = true;
                        }
                    } else {

                        var change = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(parseBuffer);
                        _pauseWait.Wait();
                        if(OnChangeFound != null) {
                            OnChangeFound(change);
                        }

                        parseBuffer.Clear();
                    }

                    nextByte = stream.ReadByte();
                }
            } catch(Exception e) {
                
                return ChangeTrackerResponseCode.Failed;
            }

            return ChangeTrackerResponseCode.Normal;
        }

        public void Pause()
        {
            _pauseWait.Reset();
        }

        public void Resume()
        {
            _pauseWait.Set();
        }

        public void Dispose()
        {
            _pauseWait.Dispose();
        }
    }
}
#endif
