using System;
using Mono.Data.Sqlite;
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
    public static class SqliteExtensions
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

        public static SqliteParameter[] ToSqliteParameters(this Object[] args)
        {
            var paramArgs = new SqliteParameter[args.LongLength];
            for(var i = 0L; i < args.LongLength; i++)
            {
                var a = args[i];
                paramArgs[i] = new SqliteParameter(a.GetType().ToDbType(), a) { ParameterName = "@" + i };
            }
            return paramArgs;
        }

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

