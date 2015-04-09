//
// SqlitePclStorageEngine.cs
//
// Author:
//     Zachary Gramana  <zack@couchbase.com>
//
// Copyright (c) 2014 Couchbase Inc.
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
using Couchbase.Lite.Storage;
using System.Threading;
using SQLitePCL;
using Couchbase.Lite.Util;
using System.Data;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Shared
{
    internal class RawStorageEngine : ISQLiteStorageEngine, IDisposable
    {
        static RawStorageEngine()
        {
            CurrentPlatform.Init(); // Prevent linking away.
        }

        private const String Tag = "MonoSQLiteStorageEngine";
        private SQLiteConnection Connection;
        private Boolean shouldCommit;

        #region implemented abstract members of SQLiteStorageEngine

        public bool Open (String path)
        {
            var result = true;
            try {
                shouldCommit = false;
                Connection = new SQLiteConnection(path);// (connectionString.ToString ());
                Connection.CreateFunction("JSON", 2, new Function(CouchbaseSqliteJsonUnicodeCollationFunction.Compare), true);
                Connection.CreateFunction("JSON_ASCII", 2, new Function(CouchbaseSqliteJsonAsciiCollationFunction.Compare), true);
                Connection.CreateFunction("JSON_RAW", 2, new Function(CouchbaseSqliteJsonRawCollationFunction.Compare), true);
                Connection.CreateFunction("REVID", 2, new Function(CouchbaseSqliteRevIdCollationFunction.Compare), true);
            } catch (Exception ex) {
                Log.E(Tag, "Error opening the Sqlite connection using connection String: {0}".Fmt(path), ex);
                result = false;    
            }

            return result;
        }

        public Int32 GetVersion()
        {
            var commandText = "PRAGMA user_version;";
            var statement = Connection.Prepare(commandText);

            var result = -1;
            try {
                var commandResult = statement.Step();
                if (commandResult == SQLiteResult.OK) {
                    Debug.Assert(statement.ColumnCount >= 1);
                    Debug.Assert(statement.DataCount >= 1);
                    result = (Int32)statement[0];
                }
            } catch (Exception e) {
                Log.E(Tag, "Error getting user version", e);
            }

            return result;
        }

        public void SetVersion(Int32 version)
        {
            var commandText = "PRAGMA user_version = @";
            var command = Connection.Prepare(commandText);

            command.Bind(1, version);

            SQLiteResult result;
            try {
                result = command.Step();
                if (result != SQLiteResult.OK)
                    throw new CouchbaseLiteException("Unable to set version to {0}".Fmt(version), StatusCode.DbError);
            } catch (Exception e) {
                Log.E(Tag, "Error getting user version", e);
            } finally {
                command.Dispose();
            }
            return;
        }

        public bool IsOpen
        {
            get { 
                return Connection != null;
            }
        }

        int transactionCount = 0;

        public void BeginTransaction ()
        {
            // NOTE.ZJG: Seems like we should really be using TO SAVEPOINT
            //           but this is how Android SqliteDatabase does it,
            //           so I'm matching that for now.
            var value = Interlocked.Increment(ref transactionCount);

            if (value == 1){
                using (var result = Connection.Prepare("BEGIN TRANSACTION"))
                {
                    if (result.Step() != SQLiteResult.OK)
                        throw new CouchbaseLiteException("Could not begin a new transaction", StatusCode.DbError);
                }
            }
        }

        public void EndTransaction ()
        {
            if (Connection == null)
                throw new InvalidOperationException("Database is not open.");

            var count = Interlocked.Decrement(ref transactionCount);
            if (count > 0)
                return;

            if (Connection == null) {
                if (shouldCommit)
                    throw new InvalidOperationException ("Transaction missing.");
                return;
            }
            if (shouldCommit) {
                Connection.Prepare("COMMIT");
                shouldCommit = false;
            } else {
                Connection.Prepare("ROLLBACK");
            }
        }

        public void SetTransactionSuccessful ()
        {
            shouldCommit = true;
        }

        public void ExecSQL (String sql, params Object[] paramArgs)
        {
            var command = BuildCommand (sql, paramArgs);

            try {
                var result = command.Step();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(command.Connection.ErrorMessage(), StatusCode.DbError);
            } catch (Exception e) {
                Log.E(Tag, "Error executing sql'{0}'".Fmt(sql), e);
            } finally {
                command.Dispose();
            }
        }

        public Cursor RawQuery (String sql, params Object[] paramArgs)
        {
            return RawQuery(sql, CommandBehavior.Default, paramArgs);
        }

        public Cursor RawQuery (String sql, CommandBehavior behavior, params Object[] paramArgs)
        {
            var command = BuildCommand (sql, paramArgs);

            Cursor cursor = null;
            try {
                Log.V(Tag, "RawQuery sql: {0}".Fmt(sql));
                var result = command.Step();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(command.Connection.ErrorMessage(), StatusCode.DbError);
                cursor = new Cursor(command);
            } catch (Exception e) {
                Log.E(Tag, "Error executing raw query '{0}'".Fmt(sql), e);
                throw;
            } finally {
                command.Dispose();
            }

            //Cursor can be null if there is an exception
            return cursor;
        }

        public long Insert (String table, String nullColumnHack, ContentValues values)
        {
            return InsertWithOnConflict(table, null, values, ConflictResolutionStrategy.None);
        }

        public long InsertWithOnConflict (String table, String nullColumnHack, ContentValues initialValues, ConflictResolutionStrategy conflictResolutionStrategy)
        {
            if (!String.IsNullOrWhiteSpace(nullColumnHack)) {
                var e = new InvalidOperationException("{0} does not support the 'nullColumnHack'.".Fmt(Tag));
                Log.E(Tag, "Unsupported use of nullColumnHack", e);
                throw e;
            }

            var command = GetInsertCommand(table, initialValues, conflictResolutionStrategy);

            var lastInsertedId = -1L;
            try {
                var result = command.Step();

                // Get the new row's id.
                // TODO.ZJG: This query should ultimately be replaced with a call to sqlite3_last_insert_rowid.
                var lastInsertedIndexCommand = Connection.Prepare("select last_insert_rowid()");
                var rowidResult =  lastInsertedIndexCommand.Step();
                if (rowidResult != SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(lastInsertedIndexCommand.Connection.ErrorMessage(), StatusCode.DbError);
                lastInsertedId = Convert.ToInt64(lastInsertedIndexCommand[0]);
                lastInsertedIndexCommand.Dispose();
                if (lastInsertedId == -1L) {
                    Log.E(Tag, "Error inserting " + initialValues + " using " + command);
                } else {
                    Log.V(Tag, "Inserting row " + lastInsertedId + " from " + initialValues + " using " + command);
                }
            } catch (Exception ex) {
                Log.E(Tag, "Error inserting into table " + table, ex);
            } finally {
                command.Dispose();
            }
            return lastInsertedId;
        }

        public int Update (String table, ContentValues values, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!String.IsNullOrWhiteSpace(table));
            Debug.Assert(values != null);

            var command = GetUpdateCommand(table, values, whereClause, whereArgs);
            sqlite3_stmt lastInsertedIndexCommand = null;

            var resultCount = -1;
            try {
                var result = command.Step();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(command.Connection.ErrorMessage(), StatusCode.DbError);
                // Get the new row's id.
                // TODO.ZJG: This query should ultimately be replaced with a call to sqlite3_last_insert_rowid.
                lastInsertedIndexCommand = Connection.Prepare("select changes()");
                result = lastInsertedIndexCommand.Step();
                if (result != SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(lastInsertedIndexCommand.Connection.ErrorMessage(), StatusCode.DbError);
                resultCount = Convert.ToInt32(lastInsertedIndexCommand[0]);
                if (resultCount == -1) {
                    throw new CouchbaseLiteException("Failed to update any records.", StatusCode.DbError);
                }
            } catch (Exception ex) {
                Log.E(Tag, "Error updating table " + table, ex);
            } finally {
                command.Dispose();
                if (lastInsertedIndexCommand != null)
                    lastInsertedIndexCommand.Dispose();
            }
            return resultCount;
        }

        public int Delete (String table, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!String.IsNullOrWhiteSpace(table));

            var command = GetDeleteCommand(table, whereClause, whereArgs);

            var resultCount = -1;
            try {
                var result = command.Step ();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException("Error deleting from table " + table, StatusCode.DbError);
                resultCount = Convert.ToInt32(command[0]);
            } catch (Exception ex) {
                Log.E(Tag, "Error deleting from table " + table, ex);
            } finally {
                command.Dispose();
            }
            return resultCount;
        }

        public void Close ()
        {
            Connection.Dispose();
        }

        #endregion

        #region Non-public Members

        sqlite3_stmt BuildCommand (string sql, object[] paramArgs)
        {
            var command = Connection.Prepare(sql);

            // Bind() uses 1-based indexes instead of zero based.
            if (paramArgs != null && paramArgs.Length > 0) {
                for (int i = 1; i <= paramArgs.Length; i++) {
                    var arg = paramArgs [i];
                    command.Bind(i, arg);
                }
            }


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
        sqlite3_stmt GetUpdateCommand (string table, ContentValues values, string whereClause, string[] whereArgs)
        {
            var builder = new StringBuilder("UPDATE ");

            builder.Append(table);
            builder.Append(" SET ");

            // Append our content column names and create our SQL parameters.
            var valueSet = values.ValueSet();
            var valueSetLength = valueSet.Count();

            var whereArgsLength = (whereArgs != null ? whereArgs.Length : 0);

            var index = 0;
            foreach(var column in valueSet)
            {
                if (index++ > 0) {
                    builder.Append(",");
                }
                builder.AppendFormat("{0} = @", column.Key);
            }

            if (!String.IsNullOrWhiteSpace(whereClause)) {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            var sql = builder.ToString();
            var command = Connection.Prepare(sql);
            for(var i = 1; i <= whereArgs.Length; i++)
            {
                command.Bind(i, whereArgs[i - 1]);
            }
            return command;
        }

        /// <summary>
        /// Avoids the additional database trip that using SqliteCommandBuilder requires.
        /// </summary>
        /// <returns>The insert command.</returns>
        /// <param name="table">Table.</param>
        /// <param name="values">Values.</param>
        /// <param name="conflictResolutionStrategy">Conflict resolution strategy.</param>
        sqlite3_stmt GetInsertCommand (String table, ContentValues values, ConflictResolutionStrategy conflictResolutionStrategy)
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
            var valueBuilder = new StringBuilder();
            var index = 0;

            foreach(var column in valueSet)
            {
                if (index > 0) {
                    builder.Append(",");
                    valueBuilder.Append(",");
                }

                builder.AppendFormat( "{0}", column.Key);
                valueBuilder.Append("@");
                index++;
            }

            builder.Append(") VALUES (");
            builder.Append(valueBuilder);
            builder.Append(")");

            var sql = builder.ToString();
            var command = Connection.Prepare(sql);

            index = 1;
            foreach(var val in valueSet)
            {
                var key = val.Key;
                command.Bind(index++, values[key]);
            }

            return command;
        }

        /// <summary>
        /// Avoids the additional database trip that using SqliteCommandBuilder requires.
        /// </summary>
        /// <returns>The delete command.</returns>
        /// <param name="table">Table.</param>
        /// <param name="whereClause">Where clause.</param>
        /// <param name="whereArgs">Where arguments.</param>
        sqlite3_stmt GetDeleteCommand (string table, string whereClause, string[] whereArgs)
        {
            var builder = new StringBuilder("DELETE FROM ");
            builder.Append(table);
            if (!String.IsNullOrWhiteSpace(whereClause)) {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            var command = Connection.Prepare(builder.ToString());
            for (int i = 1; i <= whereArgs.Length; i++) {
                command.Bind(i, whereArgs[i - 1]);
            }
            return command;
        }

        #endregion

        #region IDisposable implementation

        public void Dispose ()
        {
            throw new NotImplementedException ();
        }

        #endregion
    }
}

