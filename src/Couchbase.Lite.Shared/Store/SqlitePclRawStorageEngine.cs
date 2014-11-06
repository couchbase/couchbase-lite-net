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
using System.Threading.Tasks;
using Couchbase.Lite.Storage;
using System.Threading;
using Sharpen;
using SQLitePCL;
using Couchbase.Lite.Util;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using SQLitePCL.Ugly;
using Couchbase.Lite.Store;
using System.IO;

namespace Couchbase.Lite.Shared
{
    internal sealed class SqlitePCLRawStorageEngine : ISQLiteStorageEngine, IDisposable
    {
        // NOTE: SqlitePCL.raw only defines a subset of the ones we want,
        // so we just redefine them here instead.
        private const int SQLITE_OPEN_FILEPROTECTION_COMPLETEUNLESSOPEN = 0x00200000;
        private const int SQLITE_OPEN_READONLY = 0x00000001;
        private const int SQLITE_OPEN_READWRITE = 0x00000002;
        private const int SQLITE_OPEN_CREATE = 0x00000004;
        private const int SQLITE_OPEN_FULLMUTEX = 0x00010000;
        private const int SQLITE_OPEN_NOMUTEX = 0x00008000;
        private const int SQLITE_OPEN_PRIVATECACHE = 0x00040000;
        private const int SQLITE_OPEN_SHAREDCACHE = 0x00020000;

        private const String Tag = "SqlitePCLRawStorageEngine";
        private sqlite3 _writeConnection;
        private sqlite3 _readConnection;

        private object dbReadLock = new Object();
        private object dbLock = new Object();

        private Boolean shouldCommit;

        private string Path { get; set; }
        private TaskFactory Scheduler { get; set; }


        #region implemented abstract members of SQLiteStorageEngine

        public bool Open(String path)
        {
            if (IsOpen)
                return true;

            Path = path;
            Scheduler = new TaskFactory(new SingleThreadTaskScheduler());

            var result = true;
            try
            {
                shouldCommit = false;
                const int writer_flags = SQLITE_OPEN_FILEPROTECTION_COMPLETEUNLESSOPEN | SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_FULLMUTEX;
                OpenSqliteConnection(writer_flags, out _writeConnection);

                const int reader_flags = SQLITE_OPEN_FILEPROTECTION_COMPLETEUNLESSOPEN | SQLITE_OPEN_READONLY | SQLITE_OPEN_FULLMUTEX;
                OpenSqliteConnection(reader_flags, out _readConnection);
            }
            catch (Exception ex)
            {
                Log.E(Tag, "Error opening the Sqlite connection using connection String: {0}".Fmt(path), ex);
                result = false;
            }

            return result;
        }

        void OpenSqliteConnection(int flags, out sqlite3 db)
        {
            var status = raw.sqlite3_open_v2(Path, out db, flags, null);
            if (status != raw.SQLITE_OK)
            {
                Path = null;
                var errMessage = "Cannot open Sqlite Database at pth {0}".Fmt(Path);
                throw new CouchbaseLiteException(errMessage, StatusCode.DbError);
            }
#if !__ANDROID__ && VERBOSE
                            var i = 0;
                var val = raw.sqlite3_compileoption_get(i);
                while (val != null)
                {
                    Log.V(Tag, "Sqlite Config: {0}".Fmt(val));
                    val = raw.sqlite3_compileoption_get(++i);
                }
#endif
            raw.sqlite3_create_collation(db, "JSON", null, CouchbaseSqliteJsonUnicodeCollationFunction.Compare);
            raw.sqlite3_create_collation(db, "JSON_ASCII", null, CouchbaseSqliteJsonAsciiCollationFunction.Compare);
            raw.sqlite3_create_collation(db, "JSON_RAW", null, CouchbaseSqliteJsonRawCollationFunction.Compare);
            raw.sqlite3_create_collation(db, "REVID", null, CouchbaseSqliteRevIdCollationFunction.Compare);
        }

        public Int32 GetVersion()
        {
            const string commandText = "PRAGMA user_version;";
            sqlite3_stmt statement;

            lock (dbLock) { statement = _writeConnection.prepare(commandText); }

            var result = -1;
            try
            {
                var commandResult = raw.sqlite3_step(statement);
                if (commandResult != raw.SQLITE_ERROR)
                {
                    Debug.Assert(commandResult == raw.SQLITE_ROW);
                    result = raw.sqlite3_column_int(statement, 0);
                }
            }
            catch (Exception e)
            {
                Log.E(Tag, "Error getting user version", e);
            }
            finally
            {
                statement.Dispose();
            }

            return result;
        }

        public void SetVersion(Int32 version)
        {
            var errMessage = "Unable to set version to {0}".Fmt(version);
            var commandText = "PRAGMA user_version = ?";

            sqlite3_stmt statement;
            lock (dbLock) { 
                statement = _writeConnection.prepare (commandText);

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
            }
            return;
        }

        public bool IsOpen
        {
            get
            {
                return _writeConnection != null;
            }
        }

        int transactionCount = 0;

        public void BeginTransaction()
        {
            if (!IsOpen)
            {
                Open(Path);
            }
            // NOTE.ZJG: Seems like we should really be using TO SAVEPOINT
            //           but this is how Android SqliteDatabase does it,
            //           so I'm matching that for now.
            var value = Interlocked.Increment(ref transactionCount);

            if (value == 1)
            {
                lock (dbLock)
                {
                    using (var statement = _writeConnection.prepare("BEGIN TRANSACTION"))
                    {
                        statement.step_done();
                    }
                }
            }
        }

        public void EndTransaction()
        {
            if (_writeConnection == null)
                throw new InvalidOperationException("Database is not open.");

            var count = Interlocked.Decrement(ref transactionCount);
            if (count > 0)
                return;

            if (_writeConnection == null)
            {
                if (shouldCommit)
                    throw new InvalidOperationException("Transaction missing.");
                return;
            }
            lock (dbLock)
            {
                if (shouldCommit)
                {
                    using (var stmt = _writeConnection.prepare("COMMIT"))
                    {
                        stmt.step_done();
                    }
                    shouldCommit = false;
                }
                else
                {
                    using (var stmt = _writeConnection.prepare("ROLLBACK"))
                    {
                        stmt.step_done();
                    }
                }
            }
        }

        public void SetTransactionSuccessful()
        {
            shouldCommit = true;
        }

        /// <summary>
        /// Execute any SQL that changes the database.
        /// </summary>
        /// <param name="sql">Sql.</param>
        /// <param name="paramArgs">Parameter arguments.</param>
        public void ExecSQL(String sql, params Object[] paramArgs)
        {
            lock (dbLock)
            {
                RegisterCollationFunctions(_writeConnection);
                var command = BuildCommand(_writeConnection, sql, paramArgs);

                try
                {
                    var result = command.step();
                    if (result == SQLiteResult.ERROR)
                        throw new CouchbaseLiteException(raw.sqlite3_errmsg(_writeConnection), StatusCode.DbError);
                }
                catch (ugly.sqlite3_exception e)
                {
                    Log.E(Tag, "Error {0}, {1} executing sql '{2}'".Fmt(e.errcode, _writeConnection.extended_errcode(), sql), e);
                    throw;
                }
                finally
                {
                    command.Dispose();
                }
            }
        }

        /// <summary>
        /// Executes only read-only SQL.
        /// </summary>
        /// <returns>The query.</returns>
        /// <param name="sql">Sql.</param>
        /// <param name="paramArgs">Parameter arguments.</param>
        public Cursor RawQuery(String sql, params Object[] paramArgs)
        {
            if (!IsOpen)
            {
                Open(Path);
            }

            Cursor cursor = null;
            lock (dbReadLock) 
            {
                var command = BuildCommand (_readConnection, sql, paramArgs);
                try 
                {
                    Log.V(Tag, "RawQuery sql: {0} ({1})", sql, String.Join(", ", paramArgs));
                    cursor = new Cursor(command, dbLock);
                } 
                catch (Exception e) 
                {
                    if (command != null) 
                    {
                        command.Dispose();
                    }
                    Log.E(Tag, "Error executing raw query '{0}'".Fmt(sql), e);
                    throw;
                }
            }
            return cursor;
        }

        public long Insert(String table, String nullColumnHack, ContentValues values)
        {
            return InsertWithOnConflict(table, null, values, ConflictResolutionStrategy.None);
        }

        public long InsertWithOnConflict(String table, String nullColumnHack, ContentValues initialValues, ConflictResolutionStrategy conflictResolutionStrategy)
        {
            if (!String.IsNullOrWhiteSpace(nullColumnHack))
            {
                var e = new InvalidOperationException("{0} does not support the 'nullColumnHack'.".Fmt(Tag));
                Log.E(Tag, "Unsupported use of nullColumnHack", e);
                throw e;
            }

            var lastInsertedId = -1L;
            lock (dbLock)
            {

                var command = GetInsertCommand(table, initialValues, conflictResolutionStrategy);

                try
                {
                    int result;

                    result = command.step();
               
                    if (result == SQLiteResult.ERROR)
                        throw new CouchbaseLiteException(raw.sqlite3_errmsg(_writeConnection), StatusCode.DbError);

                    int changes = changes = _writeConnection.changes();
                    if (changes > 0)
                    {
                        lastInsertedId = _writeConnection.last_insert_rowid();
                    }

                    if (lastInsertedId == -1L)
                    {
                        Log.E(Tag, "Error inserting " + initialValues + " using " + command);
                        throw new CouchbaseLiteException("Error inserting " + initialValues + " using " + command, StatusCode.DbError);
                    } else
                    {
                        Log.V(Tag, "Inserting row {0} into {1} with values {2}", lastInsertedId, table, initialValues);
                    }

                } 
                catch (Exception ex)
                {
                    Log.E(Tag, "Error inserting into table " + table, ex);
                    throw;
                } 
                finally
                {
                    command.Dispose();
                }
            }
            return lastInsertedId;
        }

        [Conditional("MSFT")]
        internal void RegisterCollationFunctions(sqlite3 db)
        {
            lock (dbLock)
            {
                var c1 = raw.sqlite3_create_collation(db, "JSON", null, CouchbaseSqliteJsonUnicodeCollationFunction.Compare);

                var c2 = raw.sqlite3_create_collation(db, "JSON_ASCII", null, CouchbaseSqliteJsonAsciiCollationFunction.Compare);

                var c3 = raw.sqlite3_create_collation(db, "JSON_RAW", null, CouchbaseSqliteJsonRawCollationFunction.Compare);

                var c4 = raw.sqlite3_create_collation(db, "REVID", null, CouchbaseSqliteRevIdCollationFunction.Compare);
            }
        }

        public int Update(String table, ContentValues values, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!String.IsNullOrWhiteSpace(table));
            Debug.Assert(values != null);

            var t = Scheduler.StartNew(() =>
            {
                var resultCount = 0;
                var command = GetUpdateCommand(table, values, whereClause, whereArgs);
                try
                {
                    var result = command.step();
                    if (result == SQLiteResult.ERROR)
                        throw new CouchbaseLiteException(raw.sqlite3_errmsg(_writeConnection),
                            StatusCode.DbError);
                }
                catch (ugly.sqlite3_exception ex)
                {
                    var msg = ex.errmsg ?? raw.sqlite3_extended_errcode(_writeConnection).ToString();
                    Log.E(Tag, "Error {0}: \"{1}\" while updating table {2}", ex.errcode, msg, table, ex);
                }

                resultCount = _writeConnection.changes();
                if (resultCount < 0)
                {
                    Log.E(Tag, "Error updating " + values + " using " + command);
                    throw new CouchbaseLiteException("Failed to update any records.", StatusCode.DbError);
                }
                command.Dispose();
                return resultCount;
            }, CancellationToken.None);

            // NOTE.ZJG: Just a sketch here. Needs better error handling, etc.
            var r = t.GetAwaiter().GetResult();
            if (t.Exception != null)
                throw t.Exception;
            return r;
        }

        public int Delete(String table, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!String.IsNullOrWhiteSpace(table));

            var resultCount = -1;
            lock (dbLock)
            {
                RegisterCollationFunctions(_writeConnection);
                var command = GetDeleteCommand(table, whereClause, whereArgs);
                try
                {
                    var result = command.step();
                    if (result == SQLiteResult.ERROR)
                        throw new CouchbaseLiteException("Error deleting from table " + table, StatusCode.DbError);

                    resultCount = _writeConnection.changes();
                    if (resultCount < 0)
                    {
                        throw new CouchbaseLiteException("Failed to delete the records.", StatusCode.DbError);
                    }

                }
                catch (Exception ex)
                {
                    Log.E(Tag, "Error {0} when deleting from table {1}".Fmt(_writeConnection.extended_errcode(), table), ex);
                    throw;
                }
                finally
                {
                    command.Dispose();
                }
            }
            return resultCount;
        }

        public void Close()
        {
            Close(ref _readConnection);
            Close(ref _writeConnection);
            Path = null;
        }

        static void Close(ref sqlite3 db)
        {
            if (db == null)
            {
                return;
            }
            try
            {
                // Close any open statements, otherwise the
                // sqlite connection won't actually close.
                sqlite3_stmt next = null;
                while ((next = db.next_stmt(next))!= null)
                {
                    next.Dispose();
                } 
                db.close();
            }
            catch (ugly.sqlite3_exception ex)
            {
                Log.E(Tag, "Retrying database close.", ex);
                // Assuming a basic retry fixes this.
                Thread.Sleep(5000);
                db.close();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            db.Dispose();
            db = null;
        }

        #endregion

        #region Non-public Members
        private sqlite3_stmt BuildCommand(sqlite3 db, string sql, object[] paramArgs)
        {
            sqlite3_stmt command = null;
            try
            {
                if (!IsOpen)
                {
                    Open(Path);
                }

                lock(dbLock) {
                    raw.sqlite3_prepare_v2(db, sql, out command);
                    if (paramArgs.Length > 0)
                    {
                        command.bind(paramArgs);
                    }
                }
            }
            catch (Exception e)
            {
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
        sqlite3_stmt GetUpdateCommand(string table, ContentValues values, string whereClause, string[] whereArgs)
        {
            if (!IsOpen)
            {
                Open(Path);
            }
            var builder = new StringBuilder("UPDATE ");

            builder.Append(table);
            builder.Append(" SET ");

            // Append our content column names and create our SQL parameters.
            var valueSet = values.ValueSet();

            var paramList = new List<object>();

            var index = 0;
            foreach (var column in valueSet)
            {
                if (index++ > 0)
                {
                    builder.Append(",");
                }
                builder.AppendFormat("{0} = ?", column.Key);
                paramList.Add(column.Value);
            }

            if (!String.IsNullOrWhiteSpace(whereClause))
            {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            if (whereArgs != null)
            {
                paramList.AddRange(whereArgs);
            }

            var sql = builder.ToString();
            var command = _writeConnection.prepare(sql);
            command.bind(paramList.ToArray<object>());

            return command;
        }

        /// <summary>
        /// Avoids the additional database trip that using SqliteCommandBuilder requires.
        /// </summary>
        /// <returns>The insert command.</returns>
        /// <param name="table">Table.</param>
        /// <param name="values">Values.</param>
        /// <param name="conflictResolutionStrategy">Conflict resolution strategy.</param>
        sqlite3_stmt GetInsertCommand(String table, ContentValues values, ConflictResolutionStrategy conflictResolutionStrategy)
        {
            if (!IsOpen)
            {
                Open(Path);
            }
            var builder = new StringBuilder("INSERT");

            if (conflictResolutionStrategy != ConflictResolutionStrategy.None)
            {
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

            foreach (var column in valueSet)
            {
                if (index > 0)
                {
                    builder.Append(",");
                    valueBuilder.Append(",");
                }

                builder.AppendFormat("{0}", column.Key);
                valueBuilder.Append("?");

                args[index] = column.Value;

                index++;
            }

            builder.Append(") VALUES (");
            builder.Append(valueBuilder);
            builder.Append(")");

            var sql = builder.ToString();
            sqlite3_stmt command;
            lock (dbLock)
            {
                if (args != null)
                {
                    Log.D(Tag, "Preparing statement: '{0}' with values: {1}", sql, String.Join(", ", args.Select(o => o == null ? "null" : o.ToString())));
                }
                else
                {
                    Log.D(Tag, "Preparing statement: '{0}'", sql);
                }
                command = _writeConnection.prepare(sql);
                command.bind(args);
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
        sqlite3_stmt GetDeleteCommand(string table, string whereClause, string[] whereArgs)
        {
            if (!IsOpen)
            {
                Open(Path);
            }
            var builder = new StringBuilder("DELETE FROM ");
            builder.Append(table);
            if (!String.IsNullOrWhiteSpace(whereClause))
            {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            sqlite3_stmt command;
            lock (dbLock)
            {
                command = _writeConnection.prepare(builder.ToString());
                command.bind(whereArgs);
            }

            return command;
        }

        #endregion

        #region IDisposable implementation

        public void Dispose()
        {
            Close();
        }

        #endregion
    }
}
