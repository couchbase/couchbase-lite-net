// 
// DataSource.cs
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

using System.Diagnostics;

using Couchbase.Lite.Query;
using Couchbase.Lite.Support;

using JetBrains.Annotations;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryDataSource : IDataSource
    {
        #region Properties

        internal virtual string ColumnName => null;

        internal object Source { get; }

        [NotNull]
        internal ThreadSafety ThreadSafety { get; }

        #endregion

        #region Constructors

        protected QueryDataSource(object source, [NotNull]ThreadSafety threadSafety)
        {
            Debug.Assert(threadSafety != null);

            Source = source;
            ThreadSafety = threadSafety;
        }

        #endregion

        #region Public Methods

        public abstract object ToJSON();

        #endregion
    }
}
