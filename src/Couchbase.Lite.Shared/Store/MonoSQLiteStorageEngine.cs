//
// MonoSQLiteStorageEngine.cs
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

using Couchbase.Lite.Storage;
using Sharpen;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using System.Data.Common;
using System.IO;
using Couchbase.Lite.Util;
using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace Couchbase.Lite.Storage
{

    internal sealed class MonoSQLiteStorageEngine : SQLiteStorageEngine, IDisposable
    {
        static MonoSQLiteStorageEngine()
        {
            // Ensure Sqlite provider uses our custom collation function
            // that works directly on JSON encoded functions.
            SqliteFunction.RegisterFunction(typeof(CouchbaseSqliteJsonUnicodeCollationFunction));
            SqliteFunction.RegisterFunction(typeof(CouchbaseSqliteJsonAsciiCollationFunction));
            SqliteFunction.RegisterFunction(typeof(CouchbaseSqliteJsonRawCollationFunction));
            SqliteFunction.RegisterFunction(typeof(CouchbaseSqliteRevIdCollationFunction));
        }

        private const String Tag = "MonoSQLiteStorageEngine";

        static readonly IsolationLevel DefaultIsolationLevel = IsolationLevel.ReadCommitted;

        private SqliteConnection Connection;
        private SqliteTransaction currentTransaction;
        private Boolean shouldCommit;

        #region implemented abstract members of SQLiteStorageEngine

        public override bool Open (String path)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Version = 3,
                SyncMode = SynchronizationModes.Full
            };

            var result = true;
            try {
                shouldCommit = false;
                Connection = new SqliteConnection (connectionString.ToString ());
                Connection.Open();
            } catch (Exception ex) {
                Log.E(Tag, "Error opening the Sqlite connection using connection String: {0}".Fmt(connectionString.ToString()), ex);
                result = false;    
            }

            return result;
        }

        public override Int32 GetVersion()
        {
            var command = Connection.CreateCommand();
            command.CommandText = "PRAGMA user_version;";

            var result = -1;
            try {
                var commandResult = command.ExecuteScalar();
                if (commandResult is Int32) {
                    result = (Int32)commandResult;
                }
            } catch (Exception e) {
                Log.E(Tag, "Error getting user version", e);
            } finally {
                command.Dispose();
            }
            return result;
        }

        public override void SetVersion(Int32 version)
        {
            var command = Connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = @";
            command.Parameters[0].Value = version;

            try {
                command.ExecuteNonQuery();
            } catch (Exception e) {
                Log.E(Tag, "Error getting user version", e);
            } finally {
                command.Dispose();
            }
            return;
        }

        public override bool IsOpen
        {
            get { 
                return Connection.State == ConnectionState.Open; 
            }
        }

        public override void BeginTransaction ()
        {
            BeginTransaction(DefaultIsolationLevel);
        }

        int transactionCount = 0;

        public override void BeginTransaction (IsolationLevel isolationLevel)
        {
            // NOTE.ZJG: Seems like we should really be using TO SAVEPOINT
            //           but this is how Android SqliteDatabase does it,
            //           so I'm matching that for now.
            Interlocked.Increment(ref transactionCount);
            currentTransaction = Connection.BeginTransaction(isolationLevel);
        }

        public override void EndTransaction ()
        {
            if (Connection.State != ConnectionState.Open)
                throw new InvalidOperationException("Database is not open.");

            if (Interlocked.Decrement(ref transactionCount) > 0)
                return;

            if (currentTransaction == null) {
                if (shouldCommit)
                    throw new InvalidOperationException ("Transaction missing.");
                return;
            }
            if (shouldCommit) {
                currentTransaction.Commit();
                shouldCommit = false;
            } else {
                currentTransaction.Rollback();
            }
            currentTransaction.Dispose();
            currentTransaction = null;
        }

        public override void SetTransactionSuccessful ()
        {
            shouldCommit = true;
        }

        public override void ExecSQL (String sql, params Object[] paramArgs)
        {
            var command = BuildCommand (sql, paramArgs);

            try {
                command.ExecuteNonQuery();
            } catch (Exception e) {
                Log.E(Tag, "Error executing sql'{0}'".Fmt(sql), e);
            } finally {
                command.Dispose();
            }
        }

        public override Cursor RawQuery (String sql, params Object[] paramArgs)
        {
            return RawQuery(sql, CommandBehavior.Default, paramArgs);
        }

        public override Cursor RawQuery (String sql, CommandBehavior behavior, params Object[] paramArgs)
        {
            var command = BuildCommand (sql, paramArgs);

            Cursor cursor = null;
            try {
                Log.V(Tag, "RawQuery sql: {0}".Fmt(sql));
                var reader = command.ExecuteReader(behavior);
                cursor = new Cursor(reader);
            } catch (Exception e) {
                Log.E(Tag, "Error executing raw query '{0}'".Fmt(sql), e);
                throw;
            } finally {
                command.Dispose();
            }

            return cursor;
        }

        public override long Insert (String table, String nullColumnHack, ContentValues values)
        {
            return InsertWithOnConflict(table, null, values, ConflictResolutionStrategy.None);
        }

        public override long InsertWithOnConflict (String table, String nullColumnHack, ContentValues initialValues, ConflictResolutionStrategy conflictResolutionStrategy)
        {
            if (!String.IsNullOrWhiteSpace(nullColumnHack)) {
                var e = new InvalidOperationException("{0} does not support the 'nullColumnHack'.".Fmt(Tag));
                Log.E(Tag, "Unsupported use of nullColumnHack", e);
                throw e;
            }

            var command = GetInsertCommand(table, initialValues, conflictResolutionStrategy);

            var lastInsertedId = -1L;
            try {
                command.ExecuteNonQuery();

                // Get the new row's id.
                // TODO.ZJG: This query should ultimately be replaced with a call to sqlite3_last_insert_rowid.
                var lastInsertedIndexCommand = new SqliteCommand("select last_insert_rowid()", Connection, currentTransaction);
                lastInsertedId = (Int64)lastInsertedIndexCommand.ExecuteScalar();
                lastInsertedIndexCommand.Dispose();
                if (lastInsertedId == -1L) {
                    Log.E(Tag, "Error inserting " + initialValues + " using " + command.CommandText);
                } else {
                    Log.V(Tag, "Inserting row " + lastInsertedId + " from " + initialValues + " using " + command.CommandText);
                }
            } catch (Exception ex) {
                Log.E(Tag, "Error inserting into table " + table, ex);
            } finally {
                command.Dispose();
            }
            return lastInsertedId;
        }

        public override int Update (String table, ContentValues values, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!table.IsEmpty());
            Debug.Assert(values != null);

            var builder = new SqliteCommandBuilder();
            builder.SetAllValues = false;

            var command = GetUpdateCommand(table, values, whereClause, whereArgs);

            var resultCount = -1;
            try {
                resultCount = (Int32)command.ExecuteNonQuery ();
            } catch (Exception ex) {
                Log.E(Tag, "Error updating table " + table, ex);
            }
            return resultCount;
        }

        public override int Delete (String table, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!table.IsEmpty());

            var command = GetDeleteCommand(table, whereClause, whereArgs);

            var resultCount = -1;
            try {
                resultCount = command.ExecuteNonQuery ();
            } catch (Exception ex) {
                Log.E(Tag, "Error deleting from table " + table, ex);
            } finally {
                command.Dispose();
            }
            return resultCount;
        }

        public override void Close ()
        {
            Connection.Close();
        }

        #endregion

        #region Non-public Members

        SqliteCommand BuildCommand (string sql, object[] paramArgs)
        {
            var command = Connection.CreateCommand ();
            command.CommandText = sql.ReplacePositionalParams ();

            if (currentTransaction != null)
                command.Transaction = currentTransaction;

            if (paramArgs != null && paramArgs.Length > 0)
                command.Parameters.AddRange (paramArgs.ToSqliteParameters ());
            
            return command;
        }

        /// <summary>
        /// Avoids the additional database trip that using SqliteCommandBuilder requires.
        /// </summary>
        /// <returns>The update command.</returns>
        /// <param name="table">Table.</param>
        /// <param name="values">Values.</param>
        /// <param name="whereClause">Where clause.</param>
        /// <param name="whereArgs">Where arguments.</param>
        SqliteCommand GetUpdateCommand (string table, ContentValues values, string whereClause, string[] whereArgs)
        {
            var builder = new StringBuilder("UPDATE ");

            builder.Append(table);
            builder.Append(" SET ");

            // Append our content column names and create our SQL parameters.
            var valueSet = values.ValueSet();
            var valueSetLength = valueSet.Count();

            var whereArgsLength = (whereArgs != null ? whereArgs.Length : 0);
            var sqlParams = new List<SqliteParameter>(valueSetLength + whereArgsLength);

            foreach(var column in valueSet)
            {
                if (sqlParams.Count > 0) {
                    builder.Append(",");
                }
                builder.AppendFormat( "{0} = @{0}", column.Key);
                sqlParams.Add(new SqliteParameter(column.Key, column.Value));
            }

            if (!whereClause.IsEmpty()) {
                builder.Append(" WHERE ");
                builder.Append(whereClause.ReplacePositionalParams());
            }

            if (whereArgsLength > 0)
                sqlParams.AddRange(whereArgs.ToSqliteParameters());

            var sql = builder.ToString();
            var command = new SqliteCommand(sql, Connection, currentTransaction);
            command.Parameters.Clear();
            command.Parameters.AddRange(sqlParams.ToArray());

            return command;
        }

        /// <summary>
        /// Avoids the additional database trip that using SqliteCommandBuilder requires.
        /// </summary>
        /// <returns>The insert command.</returns>
        /// <param name="table">Table.</param>
        /// <param name="values">Values.</param>
        /// <param name="conflictResolutionStrategy">Conflict resolution strategy.</param>
        SqliteCommand GetInsertCommand (String table, ContentValues values, ConflictResolutionStrategy conflictResolutionStrategy)
        {
            var builder = new StringBuilder("INSERT");

            if (conflictResolutionStrategy != ConflictResolutionStrategy.None) {
                builder.Append(" OR ");
                builder.Append(conflictResolutionStrategy);
            }

            builder.Append(" INTO ");
            builder.Append(table);
            builder.Append(" (");

            // Append our content column names and create our SQL parameters.
            var valueSet = values.ValueSet();
            var sqlParams = new SqliteParameter[valueSet.LongCount()];
            var valueBuilder = new StringBuilder();
            var index = 0L;

            foreach(var column in valueSet)
            {
                if (index > 0) {
                         builder.Append(",");
                    valueBuilder.Append(",");
                }

                     builder.AppendFormat( "{0}", column.Key);
                valueBuilder.AppendFormat("@{0}", column.Key);

                sqlParams[index++] = new SqliteParameter(column.Key, column.Value);
            }

            builder.Append(") VALUES (");
            builder.Append(valueBuilder);
            builder.Append(")");

            var sql = builder.ToString();
            var command = new SqliteCommand(sql, Connection, currentTransaction);
            command.Parameters.Clear();
            command.Parameters.AddRange(sqlParams);

            return command;
        }

        /// <summary>
        /// Avoids the additional database trip that using SqliteCommandBuilder requires.
        /// </summary>
        /// <returns>The delete command.</returns>
        /// <param name="table">Table.</param>
        /// <param name="whereClause">Where clause.</param>
        /// <param name="whereArgs">Where arguments.</param>
        SqliteCommand GetDeleteCommand (string table, string whereClause, string[] whereArgs)
        {
            var builder = new StringBuilder("DELETE FROM ");
            builder.Append(table);
            if (!whereClause.IsEmpty()) {
                builder.Append(" WHERE ");
                builder.Append(whereClause.ReplacePositionalParams());
            }

            var command = new SqliteCommand(builder.ToString(), Connection, currentTransaction);
            command.Parameters.Clear();
            command.Parameters.AddRange(whereArgs.ToSqliteParameters());

            return command;
        }

        #endregion

        #region IDisposable implementation

        public void Dispose ()
        {
            if (Connection != null && Connection.State != ConnectionState.Closed)
                Connection.Close();
        }

        #endregion
 }
}
