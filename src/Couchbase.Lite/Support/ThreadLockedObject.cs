//
//  ThreadLockedObject.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Threading.Tasks;

namespace Couchbase.Lite.Support
{
    public interface IThreadLockedObject
    {
        bool IsSafeToUse { get; }

        object Copy();
    }

    internal abstract class ThreadLockedObject : IThreadLockedObject
    {
        private readonly int _owningThread = Environment.CurrentManagedThreadId;

        public bool IsSafeToUse
        {
            get {
                return _owningThread == Environment.CurrentManagedThreadId;
            }
        }

        protected void AssertCorrectThread()
        {
            if(!IsSafeToUse) {
                throw new InvalidOperationException($"An instance of {GetType().FullName} ({this}) was used on a different thread than it was created.  Please copy it first using the ThreadLocked class.");
            }
        }

        public abstract object Copy();
    }

    public static class ThreadLocked
    {
        public static T Copy<T>(T original) where T : class, IThreadLockedObject
        {
            return original.Copy() as T;
        }
    }
}
