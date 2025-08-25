// 
//  QueryCollation.cs
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
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal.Query;

internal sealed class QueryCollation : QueryExpression, IASCIICollation, IUnicodeCollation
{
    private const string Tag = nameof(QueryCollation);
    
    private readonly Dictionary<string, object> _collation = new();
    private List<object>? _json;

    // Copy constructor.
    public QueryCollation(QueryCollation collationCopy)
    {
        _collation = new Dictionary<string, object>(collationCopy._collation);
    }

    public QueryCollation(bool unicodeAware)
    {
        if (unicodeAware) {
            _collation["UNICODE"] = true;
            _collation["LOCALE"] = Collation.DefaultLocale;
        }
    }

    public void SetOperand(QueryExpression op)
    {
        var opJson = CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(op), op.ConvertToJSON());
        _json = ["COLLATE", _collation, opJson];
    }

    protected override object ToJSON()
    {
        Debug.Assert(_json != null);
        return _json!;
    }

    IASCIICollation IASCIICollation.IgnoreCase(bool ignoreCase)
    {
        _collation["CASE"] = !ignoreCase;
        return this;
    }

    public IUnicodeCollation IgnoreAccents(bool ignoreAccents)
    {
        _collation["DIAC"] = !ignoreAccents;
        return this;
    }

    IUnicodeCollation IUnicodeCollation.IgnoreCase(bool ignoreCase)
    {
        _collation["CASE"] = !ignoreCase;
        return this;
    }

    public IUnicodeCollation Locale(string locale)
    {
        _collation["LOCALE"] = locale;
        return this;
    }
}