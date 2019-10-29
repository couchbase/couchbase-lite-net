// 
// NullThreadSafety.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.Support
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal sealed class NullThreadSafety : IThreadSafety
    {
        #region Constants

        [NotNull]
        public static readonly NullThreadSafety Instance = new NullThreadSafety();

        #endregion

        #region Constructors

        private NullThreadSafety()
        {
            
        }

        #endregion

        #region IThreadSafety

        public void DoLocked(Action a) => a();

        public T DoLocked<T>(Func<T> f) => f();

        #endregion
    }
}