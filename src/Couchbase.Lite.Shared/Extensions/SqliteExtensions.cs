//
// SqliteExtensions.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;

using System.Linq;
using System.Data;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Lite.Util;
using Mono.Security.X509;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Couchbase.Lite
{
    internal static class SqliteExtensions
    {
        static readonly IDictionary<Type, DbType> TypeMap;

        static readonly Regex paramPattern;

        static SqliteExtensions()
        {
            paramPattern = new Regex("@\\w?");

            TypeMap = new Dictionary<Type, DbType>
            {
                { typeof(String), DbType.String },
                { typeof(Int32), DbType.Int32 },
                { typeof(Int64), DbType.Int64 },
                { typeof(byte[]), DbType.Binary },
                { typeof(Boolean), DbType.Boolean },
            };
        }

        public static String ReplacePositionalParams(this String sql)
        {
            var newSql = new StringBuilder(sql);

            var matches = paramPattern.Matches(sql);
            var offset = 0; // Track changes to string length due to prior transforms.
            for (var i = 0; i < matches.Count; i++) 
            {
                var match = matches [i];
                var name = i.ToString();
                newSql.Replace(match.Value, "@" + name, match.Index + offset, match.Length);
                offset += name.Length;
            }
            return newSql.ToString();
        }

//        public static SQLiteParameter[] ToSqliteParameters(this Object[] args)
//        {
//            var paramArgs = new SqliteParameter[args.LongLength];
//            for(var i = 0L; i < args.LongLength; i++)
//            {
//                var a = args[i];
//                paramArgs[i] = new SqliteParameter(a.GetType().ToDbType(), a) { ParameterName = "@" + i };
//            }
//            return paramArgs;
//        }

        public static DbType ToDbType(this Type type)
        {
            DbType dbType;
            var success = TypeMap.TryGetValue(type, out dbType);
            if (!success)
            {
                var message = "Failed to determine database type for query param of type {0}".Fmt(type.Name);
                Log.E("SqliteExtensions", message);
                throw new ArgumentException(message, "type");
            }
            return dbType;
        }
    }
}

