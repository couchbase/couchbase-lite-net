//
// LogGroup.cs
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
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Util
{
    internal sealed class LogGroup : IDomainLogging, IEnumerable<IDomainLogging>
    {
        private readonly IEnumerable<IDomainLogging> _components;

        public string Domain { get; }

        public Log.LogLevel Level
        {
            get {
                var tmp = _components.FirstOrDefault();
                return tmp == null ? Log.LogLevel.None : tmp.Level;
            }
            set { 
                foreach (var component in _components) {
                    component.Level = value;
                }
            }
        }

        internal LogGroup(params IDomainLogging[] components)
        {
            _components = components;
            Domain = components.Aggregate(String.Empty, (l, r) => $"{l},{r.Domain}");
        }
        
        #region IEnumerable implementation

        public IEnumerator<IDomainLogging> GetEnumerator()
        {
            return _components.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}

