//
// SqlitePclRawStorageEngine.cs
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
using SQLitePCL.Ugly;
using Couchbase.Lite.Store;

namespace Couchbase.Lite.Shared
{
    internal class SqlitePCLRawStorageEngine : ISQLiteStorageEngine, IDisposable
    {
        private const String Tag = "SqlitePCLRawStorageEngine";
        private sqlite3 db;
        private Boolean shouldCommit;

        #region implemented abstract members of SQLiteStorageEngine

        public bool Open (String path)
        {
            var errMessage = "Cannot open Sqlite Database at pth {0}".Fmt(path);

            var result = true;
            try {
                shouldCommit = false;
                const int flags = 0x00200000; // #define SQLITE_OPEN_FILEPROTECTION_COMPLETEUNLESSOPEN 0x00200000
                //var status = raw.sqlite3_open_v2(path, out db, flags, null);
                var status = raw.sqlite3_open(path, out db);
                if (status != raw.SQLITE_OK)
                {
                    throw new CouchbaseLiteException(errMessage, StatusCode.DbError);
                }

                raw.sqlite3_create_collation(db, "JSON", null, CouchbaseSqliteJsonUnicodeCollationFunction.Compare);
                raw.sqlite3_create_collation(db, "JSON_ASCII", null, CouchbaseSqliteJsonAsciiCollationFunction.Compare);
                raw.sqlite3_create_collation(db, "JSON_RAW", null, CouchbaseSqliteJsonRawCollationFunction.Compare);
                raw.sqlite3_create_collation(db, "REVID", null, CouchbaseSqliteRevIdCollationFunction.Compare);
            } catch (Exception ex) {
                Log.E(Tag, "Error opening the Sqlite connection using connection String: {0}".Fmt(path), ex);
                result = false;
            }

            return result;
        }

        public Int32 GetVersion()
        {
            var commandText = "PRAGMA user_version;";
            sqlite3_stmt statement = db.prepare(commandText);

            var result = -1;
            try {
                var commandResult = raw.sqlite3_step(statement);
                if (commandResult != raw.SQLITE_ERROR) {
                    Debug.Assert(commandResult == raw.SQLITE_ROW);
                    result = raw.sqlite3_column_int(statement, 0);
                }
            } catch (Exception e) {
                Log.E(Tag, "Error getting user version", e);
            }

            return result;
        }

        public void SetVersion(Int32 version)
        {
            var errMessage = "Unable to set version to {0}".Fmt(version);
            var commandText = "PRAGMA user_version = ?";

            sqlite3_stmt statement = db.prepare(commandText);

            if (raw.sqlite3_bind_int(statement, 1, version) == raw.SQLITE_ERROR)
                throw new CouchbaseLiteException(errMessage, StatusCode.DbError);

            int result;
            try {
                result = statement.step();
                if (result != SQLiteResult.OK)
                    throw new CouchbaseLiteException(errMessage, StatusCode.DbError);
            } catch (Exception e) {
                Log.E(Tag, "Error getting user version", e);
            } finally {
                statement.Dispose();
            }
            return;
        }

        public bool IsOpen
        {
            get { 
                return db != null;
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
                using (var statement = db.prepare("BEGIN TRANSACTION"))
                {
                    statement.step_done();
                }
            }
        }

        public void EndTransaction ()
        {
            if (db == null)
                throw new InvalidOperationException("Database is not open.");

            var count = Interlocked.Decrement(ref transactionCount);
            if (count > 0)
                return;

            if (db == null) {
                if (shouldCommit)
                    throw new InvalidOperationException ("Transaction missing.");
                return;
            }
            if (shouldCommit) {
                using (var stmt = db.prepare("COMMIT")) {
                    stmt.step_done();
                }
                shouldCommit = false;
            } else {
                using (var stmt = db.prepare("ROLLBACK")) {
                    stmt.step_done();
                }
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
                var result = command.step();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(raw.sqlite3_errmsg(db), StatusCode.DbError);
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
                cursor = new Cursor(command);
            } catch (Exception e) {
                if (command != null) {
                    command.Dispose();
                }

                Log.E(Tag, "Error executing raw query '{0}'".Fmt(sql), e);
                throw;
            } 

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
                var result = command.step();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(raw.sqlite3_errmsg(db), StatusCode.DbError);

                int changes = db.changes();
                if (changes > 0) 
                {
                    lastInsertedId = db.last_insert_rowid();
                }

                if (lastInsertedId == -1L) {
                    Log.E(Tag, "Error inserting " + initialValues + " using " + command);
                    throw new CouchbaseLiteException("Error inserting " + initialValues + " using " + command, StatusCode.DbError);
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
            var resultCount = 0;
            try {
                var result = command.step();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException(raw.sqlite3_errmsg(db), StatusCode.DbError);

                resultCount = db.changes();
                if (resultCount < 0) 
                {
                    Log.E(Tag, "Error updating " + values + " using " + command);
                    throw new CouchbaseLiteException("Failed to update any records.", StatusCode.DbError);
                }
            } catch (Exception ex) {
                Log.E(Tag, "Error updating table " + table, ex);
            } finally {
                command.Dispose();
            }
            return resultCount;
        }

        public int Delete (String table, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!String.IsNullOrWhiteSpace(table));

            var command = GetDeleteCommand(table, whereClause, whereArgs);
            var resultCount = 0;
            try {
                var result = command.step();
                if (result == SQLiteResult.ERROR)
                    throw new CouchbaseLiteException("Error deleting from table " + table, StatusCode.DbError);

                resultCount = db.changes();
                if (resultCount == 0) 
                {
                    throw new CouchbaseLiteException("Failed to delete the records.", StatusCode.DbError);
                }

            } catch (Exception ex) {
                Log.E(Tag, "Error deleting from table " + table, ex);
            } finally {
                command.Dispose();
            }
            return resultCount;
        }

        public void Close ()
        {
            db.Dispose();
        }

        #endregion

        #region Non-public Members

        sqlite3_stmt BuildCommand (string sql, object[] paramArgs)
        {
            sqlite3_stmt command = null;
            try {
                //Log.D(Tag, "Build Command : " + sql + " with params " + paramArgs);
                command = db.prepare(sql);
                ugly.bind(command, paramArgs);
            } catch (Exception e) {
                Log.E(Tag, "Error when build a sql " + sql + " with params " + paramArgs, e);
                throw;
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

            var paramList = new List<object>();

            var index = 0;
            foreach(var column in valueSet)
            {
                if (index++ > 0) {
                    builder.Append(",");
                }
                builder.AppendFormat("{0} = ?", column.Key);
                paramList.Add(column.Value);
            }

            if (!String.IsNullOrWhiteSpace(whereClause)) {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            if (whereArgs != null)
            {
                foreach(var arg in whereArgs)
                {
                    paramList.Add(arg);
                }
            }

            var sql = builder.ToString();
            var command = db.prepare(sql);
            ugly.bind(command, paramList.ToArray<object>());

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

            var args = new object[valueSet.Count];

            foreach(var column in valueSet)
            {
                if (index > 0) {
                    builder.Append(",");
                    valueBuilder.Append(",");
                }

                builder.AppendFormat( "{0}", column.Key);
                valueBuilder.Append("?");

                args[index] = column.Value;

                index++;
            }

            builder.Append(") VALUES (");
            builder.Append(valueBuilder);
            builder.Append(")");

            var sql = builder.ToString();
            var command = db.prepare(sql);
            ugly.bind(command, args);

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

            var command = db.prepare(builder.ToString());
            ugly.bind(command, whereArgs);

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

