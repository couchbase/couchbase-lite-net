// 
// FilteredEvent.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Couchbase.Lite.Util
{
    internal sealed class FilteredEvent<TFilterType, TEventType> where TEventType : EventArgs
    {
        #region Variables

        private ConcurrentDictionary<TFilterType, HashSet<EventHandler<TEventType>>> _eventMap =
            new ConcurrentDictionary<TFilterType, HashSet<EventHandler<TEventType>>>();

        #endregion

        #region Public Methods

        public int Add(TFilterType key, EventHandler<TEventType> method)
        {
            var collection = _eventMap.GetOrAdd(key, new HashSet<EventHandler<TEventType>>());
            var count = collection.Count;
            collection.Add(method);
            return count;
        }

        public int Remove(TFilterType key, EventHandler<TEventType> method)
        {
            var collection = _eventMap.GetOrAdd(key, new HashSet<EventHandler<TEventType>>());
            collection.Remove(method);
            return collection.Count;
        }

        #endregion

        #region Internal Methods

        internal void Fire(TFilterType key, object sender, TEventType args)
        {
            var collection = _eventMap.GetOrAdd(key, new HashSet<EventHandler<TEventType>>());
            foreach (var method in collection) {
                method.Invoke(sender, args);
            }
        }

        #endregion
    }
}
