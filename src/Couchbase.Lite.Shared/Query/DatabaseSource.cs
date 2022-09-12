// 
//  DatabaseSource.cs
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

using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;
using Debug = System.Diagnostics.Debug;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class DatabaseSource : QueryDataSource, IDataSourceAs
    {
        #region Constants

        private const string Tag = nameof(DatabaseSource);

        #endregion

        #region Variables

        private string _as;
        private string _collection;
        private string _scope;

        #endregion

        #region Properties

        internal override string ColumnName
        {
            get {
                if (_as != null) {
                    return _as;
                }

                return Database?.Name;
            }
        }

        internal Database Database => Source as Database;

        internal Collection Collection => Source as Collection;

        #endregion

        #region Constructors

        internal DatabaseSource([NotNull] Collection collection, [NotNull] ThreadSafety threadSafety) : base(collection, threadSafety)
        {
            Debug.Assert(collection != null);
            _collection = collection.Name;
            _scope = collection.Scope.Name;
        }

        internal DatabaseSource([NotNull]Database database, [NotNull]ThreadSafety threadSafety) : base(database, threadSafety)
        {
            Debug.Assert(database != null);
        }

        #endregion

        #region Overrides

        public override object ToJSON()
        {
            Dictionary<string, object> dict = null;
            if (Collection != null) {
                dict = new Dictionary<string, object>
                {
                    ["SCOPE"] = _scope,
                    ["COLLECTION"] = _collection
                };
            }

            if (ColumnName != null) {
                if (dict == null)
                    dict = new Dictionary<string, object>();

                dict.Add("AS", ColumnName);
            }

            return dict;
        }

        #endregion

        #region IDataSourceAs

        public IDataSource As([NotNull]string alias)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(alias), alias);

            _as = alias;
            return this;
        }

        #endregion
    }
}
