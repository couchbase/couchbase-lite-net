//
// IChangeTrackerResponseLogic.cs
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
using System.Collections.Generic;
using System.Threading;

namespace Couchbase.Lite.Internal
{
    internal enum ChangeTrackerResponseCode
    {
        Normal,
        ChangeHeartbeat,
        Failed
    }

    internal interface IChangeTrackerResponseLogic : IDisposable
    {
        TimeSpan Heartbeat { get; set; }

        Action OnCaughtUp { get; set; }

        Action<IDictionary<string, object>> OnChangeFound { get; set; }

        Action<Exception> OnFinished { get; set; }

        ChangeTrackerResponseCode ProcessResponseStream(Stream stream, CancellationToken token);

        void Pause();

        void Resume();
    }
}

