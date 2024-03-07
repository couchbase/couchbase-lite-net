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
using System.Linq;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query
{
    internal sealed class DatabaseSource : QueryDataSource, IDataSourceAs
    {
        #region Constants

        private const string Tag = nameof(DatabaseSource);

        #endregion

        #region Variables

        private string? _as;
        private string _collection;
        private string _scope;
        private bool _legacyColumn;

        #endregion

        #region Properties

        internal override string? ColumnName
        {
            get {
                if (_as != null) {
                    return _as;
                }

                return _legacyColumn ? Collection?.Database?.Name : null;
            }
        }

        internal Collection? Collection => Source as Collection;

        #endregion

        #region Constructors

        internal DatabaseSource(Collection collection, ThreadSafety threadSafety) : base(collection, threadSafety)
        {
            _collection = collection.Name;
            _scope = collection.Scope.Name;
        }

        internal DatabaseSource(Database database, ThreadSafety threadSafety)
            : this(database.GetDefaultCollection(), threadSafety)
        {
            _legacyColumn = true;
        }

        #endregion

        #region Overrides

        public override object ToJSON()
        {
            Dictionary<string, object?> dict = new();
            if (Collection != null) {
                dict.Add("SCOPE", _scope);
                dict.Add("COLLECTION", _collection);
            }

            if (ColumnName != null) {
                dict.Add("AS", ColumnName);
            }

            Debug.Assert(dict.Any());
            return dict;
        }

        #endregion

        #region IDataSourceAs

        public IDataSource As(string alias)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(alias), alias);

            _as = alias;
            return this;
        }

        #endregion
    }
}
