// 
// QueryIndex.cs
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
using System.Globalization;
using System.Linq;
using Couchbase.Lite.Query;
using Couchbase.Lite.Util;
using JetBrains.Annotations;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Query
{
    internal abstract class QueryIndexBase : IndexDescriptor, IFullTextIndex, IValueIndex
    {
        #region Variables

        private readonly IFullTextIndexItem[] _ftsItems;
        private readonly IValueIndexItem[] _valueItems;

#if COUCHBASE_ENTERPRISE
        private readonly string[] _predictiveItems;
#endif

        #endregion

        #region Internal Methods

        internal virtual object ToJSON()
        {
            object jsonObj = null;
            if (_ftsItems != null)
            {
                jsonObj = QueryExpression.EncodeToJSON(_ftsItems.OfType<QueryIndexItem>().Select(x => x.Expression)
                    .ToList());
            }
            else if (_valueItems != null)
            {
                jsonObj = QueryExpression.EncodeToJSON(_valueItems.OfType<QueryIndexItem>().Select(x => x.Expression)
                    .ToList());
            }

            return jsonObj;
        }

        #endregion

        #region Constructors

        protected QueryIndexBase(params IFullTextIndexItem[] items)
            : this(C4IndexType.FullTextIndex)
        {
            _ftsItems = items;
        }

        protected QueryIndexBase(params IValueIndexItem[] items)
            : this(C4IndexType.ValueIndex)
        {
            _valueItems = items;
        }

        protected QueryIndexBase(C4IndexType indexType)
            : base(indexType, C4QueryLanguage.N1QLQuery)
        {

        }

        #endregion

        #region IFullTextIndex

        public new IFullTextIndex IgnoreAccents(bool ignoreAccents)
        {
            base.IgnoreAccents(ignoreAccents);
            return this;
        }

        public new IFullTextIndex SetLanguage(string language)
        {
            base.SetLanguage(language);
            return this;
        }

        #endregion
    }

#if !COUCHBASE_ENTERPRISE

    internal sealed class QueryIndex : QueryIndexBase
    {
    #region Constructors

        internal QueryIndex([ItemNotNull]params IFullTextIndexItem[] items)
            : base(items)
        {
            Debug.Assert(items.All(x => x != null));
        }

        internal QueryIndex([ItemNotNull]params IValueIndexItem[] items)
            :base(items)
        {
            Debug.Assert(items.All(x => x != null));
        }

    #endregion
    }

#endif
}