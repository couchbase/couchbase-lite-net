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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Storage;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;
using SQLitePCL;
using SQLitePCL.Ugly;

#if !NET_3_5
using StringEx = System.String;
#endif

#if SQLITE
namespace Couchbase.Lite.Storage.SystemSQLite
#else
namespace Couchbase.Lite.Storage.SQLCipher
#endif
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

        private const String TAG = "SqlitePCLRawStorageEngine";
        private sqlite3 _writeConnection;
        private sqlite3 _readConnection;
        private bool _readOnly; // Needed for issue with GetVersion()

        private Boolean shouldCommit;

        private string Path { get; set; }
        private TaskFactory Factory { get; set; }
        private CancellationTokenSource _cts = new CancellationTokenSource();

        #region ISQLiteStorageEngine

        public int LastErrorCode { get; private set; }

        // Returns true on success, false if encryption key is wrong, throws exception for other cases
        public bool Decrypt(SymmetricKey encryptionKey, sqlite3 connection)
        {
            #if !ENCRYPTION
            throw new InvalidOperationException("Encryption not supported on this store");
            #else
            if (encryptionKey != null) {
                // http://sqlcipher.net/sqlcipher-api/#key
                var sql = String.Format("PRAGMA key = \"x'{0}'\"", encryptionKey.HexData);
                try {
                    ExecSQL(sql, connection);
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Decryption operation failed");
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException("Decryption operation failed", e);
                }
            }

            // Verify that encryption key is correct (or db is unencrypted, if no key given):
            var result = raw.sqlite3_exec(connection, "SELECT count(*) FROM sqlite_master");
            if (result != raw.SQLITE_OK) {
                if (result == raw.SQLITE_NOTADB) {
                    return false;
                } else {
                    throw new CouchbaseLiteException(String.Format("Cannot read from database ({0})", result), StatusCode.DbError);
                }
            }

            return true;
            #endif
        }

        public bool Open(String path, bool readOnly, string schema, SymmetricKey encryptionKey)
        {
            if (IsOpen)
                return true;

            Path = path;
            _readOnly = readOnly;
            Factory = new TaskFactory(new SingleThreadScheduler());

            try {
                shouldCommit = false;
                int readFlag = readOnly ? SQLITE_OPEN_READONLY : SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE;
                int writer_flags = SQLITE_OPEN_FILEPROTECTION_COMPLETEUNLESSOPEN | readFlag | SQLITE_OPEN_FULLMUTEX;
                OpenSqliteConnection(writer_flags, encryptionKey, out _writeConnection);

                #if ENCRYPTION
                if (!Decrypt(encryptionKey, _writeConnection)) {
                    throw new CouchbaseLiteException(StatusCode.Unauthorized);
                }
                #endif

				if(schema != null && GetVersion() == 0) {
					foreach (var statement in schema.Split(';')) {
						ExecSQL(statement);
					}
				}

				const int reader_flags = SQLITE_OPEN_FILEPROTECTION_COMPLETEUNLESSOPEN | SQLITE_OPEN_READONLY | SQLITE_OPEN_FULLMUTEX;
				OpenSqliteConnection(reader_flags, encryptionKey, out _readConnection);

                #if ENCRYPTION
				if(!Decrypt(encryptionKey, _readConnection)) {
					throw new CouchbaseLiteException(StatusCode.Unauthorized);
				}
                #endif
            } catch(CouchbaseLiteException) {
                Log.W(TAG, "Error opening SQLite storage engine");
                throw;
            } catch (Exception ex) {
                throw new CouchbaseLiteException("Failed to open SQLite storage engine", ex) { Code = StatusCode.Exception };
            }

            return true;
        }

        void OpenSqliteConnection(int flags, SymmetricKey encryptionKey, out sqlite3 db)
        {
            LastErrorCode = raw.sqlite3_open_v2(Path, out db, flags, null);
            if (LastErrorCode != raw.SQLITE_OK) {
                Path = null;
                var errMessage = "Failed to open SQLite storage engine at path {0}".Fmt(Path);
                throw new CouchbaseLiteException(errMessage, StatusCode.DbError);
            }

#if !__ANDROID__ && !NET_3_5 && VERBOSE
                var i = 0;
                var val = raw.sqlite3_compileoption_get(i);
                while (val != null)
                {
                    Log.V(TAG, "Sqlite Config: {0}".Fmt(val));
                    val = raw.sqlite3_compileoption_get(++i);
                }
#endif

            Log.D(TAG, "Open {0} (flags={1}{2})", Path, flags, (encryptionKey != null ? ", encryption key given" : ""));

            raw.sqlite3_create_collation(db, "JSON", null, CouchbaseSqliteJsonUnicodeCollationFunction.Compare);
            raw.sqlite3_create_collation(db, "JSON_ASCII", null, CouchbaseSqliteJsonAsciiCollationFunction.Compare);
            raw.sqlite3_create_collation(db, "JSON_RAW", null, CouchbaseSqliteJsonRawCollationFunction.Compare);
            raw.sqlite3_create_collation(db, "REVID", null, CouchbaseSqliteRevIdCollationFunction.Compare);
        }

        public int GetVersion()
        {
            const string commandText = "PRAGMA user_version;";
            sqlite3_stmt statement;

            //NOTE.JHB Even though this is a read, iOS doesn't return the correct value on the read connection
            //but someone should try again when the version goes beyond 3.7.13

            statement = BuildCommand (_writeConnection, commandText, null);

            var result = -1;
            try {
                LastErrorCode = raw.sqlite3_step(statement);
                if (LastErrorCode != raw.SQLITE_ERROR) {
                    Debug.Assert(LastErrorCode == raw.SQLITE_ROW);
                    result = raw.sqlite3_column_int(statement, 0);
                }
            } catch (Exception e) {
                Log.E(TAG, "Error getting user version", e);
            } finally {
                statement.Dispose();
            }

            return result;
        }

        public void SetVersion(int version)
        {
            if (_readOnly) {
                throw new CouchbaseLiteException("Attempting to write to a readonly database", StatusCode.Forbidden);
            }

            var errMessage = "Unable to set version to {0}".Fmt(version);
            const string commandText = "PRAGMA user_version = ?";

            Factory.StartNew(() =>
            {
                sqlite3_stmt statement = BuildCommand(_writeConnection, commandText, null);

                if ((LastErrorCode = raw.sqlite3_bind_int(statement, 1, version)) == raw.SQLITE_ERROR)
                    throw new CouchbaseLiteException(errMessage, StatusCode.DbError);

                try {
                    LastErrorCode = statement.step();
                    if (LastErrorCode != SQLiteResult.OK)
                        throw new CouchbaseLiteException(errMessage, StatusCode.DbError);
                } catch (Exception e) {
                    Log.E(TAG, "Error getting user version", e);
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                } finally {
                    statement.Dispose();
                }
            });
        }

        public bool IsOpen
        {
            get
            {
                return _writeConnection != null;
            }
        }

        int transactionCount = 0;

        public int BeginTransaction()
        {
            if (_readOnly) {
                throw new CouchbaseLiteException("Transactions not allowed on a readonly database", StatusCode.Forbidden);
            }

            if (!IsOpen) {
                throw new CouchbaseLiteException("BeginTransaction called on closed database", StatusCode.BadRequest);
            }

            // NOTE.ZJG: Seems like we should really be using TO SAVEPOINT
            //           but this is how Android SqliteDatabase does it,
            //           so I'm matching that for now.
            var value = Interlocked.Increment(ref transactionCount);

            if (value == 1)
            {
                var t = Factory.StartNew(() =>
                {
                    try {
                        using (var statement = BuildCommand(_writeConnection, "BEGIN IMMEDIATE TRANSACTION", null))
                        {
                            statement.step_done();
                        }
                    } catch (Exception e) {
                        LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                        Log.E(TAG, "Error BeginTransaction", e);
                    }
                });
                t.Wait();
            }

            return value;
        }

        public int EndTransaction()
        {
            if (_readOnly) {
                throw new CouchbaseLiteException("Transactions not allowed on a readonly database", StatusCode.Forbidden);
            }

            if (!IsOpen) {
                throw new CouchbaseLiteException("EndTransaction called on closed database", StatusCode.BadRequest);
            }

            var count = Interlocked.Decrement(ref transactionCount);
            if (count > 0)
                return count;

            var t = Factory.StartNew(() =>
            {
                try {
                    if (shouldCommit)
                    {
                        using (var statement = BuildCommand(_writeConnection, "COMMIT", null))
                        {
                            statement.step_done();
                        }

                        shouldCommit = false;
                    }
                    else
                    {
                        using (var statement = BuildCommand(_writeConnection, "ROLLBACK", null))
                        {
                            statement.step_done();
                        }
                    }
                } catch (Exception e) {
                    Log.E(TAG, "Error EndTransaction", e);
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                }
            });
            t.Wait();

            return 0;
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
        public int ExecSQL(String sql, params Object[] paramArgs)
        {  
            return ExecSQL(sql, _writeConnection, paramArgs);
        }

        /// <summary>
        /// Executes only read-only SQL.
        /// </summary>
        /// <returns>The query.</returns>
        /// <param name="sql">Sql.</param>
        /// <param name="paramArgs">Parameter arguments.</param>
        public Cursor IntransactionRawQuery(String sql, params Object[] paramArgs)
        {
            if (!IsOpen) {
                throw new CouchbaseLiteException("InTransactionRawQuery called on closed database", StatusCode.BadRequest);
            }

            if (transactionCount == 0) 
            {
                return RawQuery(sql, paramArgs);
            }

            var t = Factory.StartNew(() =>
            {
                Cursor cursor = null;
                sqlite3_stmt command = null;
                try 
                {
                    Log.V(TAG, "RawQuery sql: {0} ({1})", sql, String.Join(", ", paramArgs.ToStringArray()));
                    command = BuildCommand (_writeConnection, sql, paramArgs);
                    cursor = new Cursor(command);
                }
                catch (Exception e) 
                {
                    if (command != null) 
                    {
                        command.Dispose();
                    }
                    Log.E(TAG, "Error executing raw query '{0}'".Fmt(sql), e);
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    throw;
                }
                return cursor;
            });

            return t.Result;
        }


        /// <summary>
        /// Executes only read-only SQL.
        /// </summary>
        /// <returns>The query.</returns>
        /// <param name="sql">Sql.</param>
        /// <param name="paramArgs">Parameter arguments.</param>
        public Cursor RawQuery(String sql, params Object[] paramArgs)
        {
            if (!IsOpen) {
                throw new CouchbaseLiteException("RawQuery called on closed database", StatusCode.BadRequest);
            }

            Cursor cursor = null;
            sqlite3_stmt command = null;

            var t = Factory.StartNew (() => 
            {
                try {
                    Log.V (TAG, "RawQuery sql: {0} ({1})", sql, String.Join (", ", paramArgs.ToStringArray ()));
                    command = BuildCommand (_readConnection, sql, paramArgs);
                    cursor = new Cursor (command);
                } catch (Exception e) {
                    if (command != null) {
                        command.Dispose ();
                    }
                    var args = paramArgs == null 
                    ? String.Empty 
                    : String.Join (",", paramArgs.ToStringArray ());
                    Log.E (TAG, "Error executing raw query '{0}' is values '{1}' {2}".Fmt (sql, args, _readConnection.errmsg ()), e);
                    LastErrorCode = raw.sqlite3_errcode(_readConnection);
                    throw;
                }
                return cursor;
            });

            return t.Result;
        }

        public long Insert(String table, String nullColumnHack, ContentValues values)
        {
            return InsertWithOnConflict(table, null, values, ConflictResolutionStrategy.None);
        }

        public long InsertWithOnConflict(String table, String nullColumnHack, ContentValues initialValues, ConflictResolutionStrategy conflictResolutionStrategy)
        {
            if (_readOnly) {
                throw new CouchbaseLiteException("Attempting to write to a readonly database", StatusCode.Forbidden);
            }

            if (!StringEx.IsNullOrWhiteSpace(nullColumnHack))
            {
                var e = new InvalidOperationException("{0} does not support the 'nullColumnHack'.".Fmt(TAG));
                Log.E(TAG, "Unsupported use of nullColumnHack", e);
                throw e;
            }

            var t = Factory.StartNew(() =>
            {
                var lastInsertedId = -1L;
                var command = GetInsertCommand(table, initialValues, conflictResolutionStrategy);

                try
                {
                    LastErrorCode = command.step();
                    command.Dispose();
                    if (LastErrorCode == SQLiteResult.ERROR)
                        throw new CouchbaseLiteException(raw.sqlite3_errmsg(_writeConnection), StatusCode.DbError);

                    int changes = _writeConnection.changes();
                    if (changes > 0)
                    {
                        lastInsertedId = _writeConnection.last_insert_rowid();
                    }

                    if (lastInsertedId == -1L)
                    {
                        if(conflictResolutionStrategy != ConflictResolutionStrategy.Ignore) {
                            Log.E(TAG, "Error inserting " + initialValues + " using " + command);
                            throw new CouchbaseLiteException("Error inserting " + initialValues + " using " + command, StatusCode.DbError);
                        }
                    } else
                    {
                        Log.V(TAG, "Inserting row {0} into {1} with values {2}", lastInsertedId, table, initialValues);
                    }

                } 
                catch(CouchbaseLiteException) {
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    Log.E(TAG, "Error inserting into table " + table);
                    throw;
                }
                catch (Exception ex)
                {
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    throw new CouchbaseLiteException("Error inserting into table " + table, ex) { Code = StatusCode.DbError };
                }
                return lastInsertedId;
            });

            var r = t.ConfigureAwait(false).GetAwaiter().GetResult();
            if (t.Exception != null)
                throw t.Exception;

            return r;
        }

        public int Update(String table, ContentValues values, String whereClause, params String[] whereArgs)
        {
            if (_readOnly) {
                throw new CouchbaseLiteException("Attempting to write to a readonly database", StatusCode.Forbidden);
            }

            Debug.Assert(!StringEx.IsNullOrWhiteSpace(table));
            Debug.Assert(values != null);

            var t = Factory.StartNew(() =>
            {
                var resultCount = 0;
                var command = GetUpdateCommand(table, values, whereClause, whereArgs);
                try
                {
                    LastErrorCode = command.step();
                    if (LastErrorCode == SQLiteResult.ERROR)
                        throw new CouchbaseLiteException(raw.sqlite3_errmsg(_writeConnection),
                            StatusCode.DbError);
                }
                catch (ugly.sqlite3_exception ex)
                {
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    var msg = raw.sqlite3_extended_errcode(_writeConnection);
                    Log.E(TAG, "Error {0}: \"{1}\" while updating table {2}\r\n{3}", ex.errcode, msg, table, ex);
                }

                resultCount = _writeConnection.changes();
                if (resultCount < 0)
                {
                    Log.E(TAG, "Error updating " + values + " using " + command);
                    throw new CouchbaseLiteException("Failed to update any records.", StatusCode.DbError);
                }
                command.Dispose();
                return resultCount;
            }, CancellationToken.None);

            // NOTE.ZJG: Just a sketch here. Needs better error handling, etc.

            //doesn't look good
            var r = t.ConfigureAwait(false).GetAwaiter().GetResult();
            if (t.Exception != null)
                throw t.Exception;
            
            return r;
        }

        public int Delete(String table, String whereClause, params String[] whereArgs)
        {
            if (_readOnly) {
                throw new CouchbaseLiteException("Attempting to write to a readonly database", StatusCode.Forbidden);
            }

            Debug.Assert(!StringEx.IsNullOrWhiteSpace(table));

            var t = Factory.StartNew(() =>
            {
                var resultCount = -1;
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
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    Log.E(TAG, "Error {0} when deleting from table {1}".Fmt(_writeConnection.extended_errcode(), table), ex);
                    throw;
                }
                finally
                {
                    command.Dispose();
                }
                return resultCount;
            });

            // NOTE.ZJG: Just a sketch here. Needs better error handling, etc.
            var r = t.GetAwaiter().GetResult();
            if (t.Exception != null) 
            {
                //this is bad: should not arbitrarily crash the app
                throw t.Exception;
            }
                
            return r;
        }

        public void Close()
        {
            _cts.Cancel();
            ((SingleThreadScheduler)Factory.Scheduler).Dispose();
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

            var dbCopy = db;
            db = null;

            try
            {
                // Close any open statements, otherwise the
                // sqlite connection won't actually close.
                sqlite3_stmt next = null;
                while ((next = dbCopy.next_stmt(next))!= null)
                {
                    next.Dispose();
                } 
                dbCopy.close();
            }
            catch (KeyNotFoundException ex)
            {
                // Appears to be a bug in sqlite3.find_stmt. Concurrency issue in static dictionary?
                // Assuming we're done.
                Log.W(TAG, "Abandoning database close.", ex);
            }
            catch (ugly.sqlite3_exception ex)
            {
                Log.E(TAG, "Retrying database close.", ex);
                // Assuming a basic retry fixes this.
                Thread.Sleep(5000);
                dbCopy.close();
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                dbCopy.Dispose();
            }
            catch (Exception ex)
            {
                Log.E(TAG, "Error while closing database.", ex);
            }
        }



        #endregion

        #region Non-public Members

        private sqlite3_stmt BuildCommand(sqlite3 db, string sql, object[] paramArgs)
        {
            if (db == null) {
                throw new ArgumentNullException("db");
            }

            if (!IsOpen) {
                throw new CouchbaseLiteException("BuildCommand called on closed database", StatusCode.BadRequest);
            }

            sqlite3_stmt command = null;
            try {
                lock(Cursor.StmtDisposeLock) {
                    LastErrorCode = raw.sqlite3_prepare_v2(db, sql, out command);
                }

                if (LastErrorCode != raw.SQLITE_OK || command == null) {
                    Log.E(TAG, "sqlite3_prepare_v2: " + LastErrorCode);
                }

                if (paramArgs != null && paramArgs.Length > 0 && command != null && LastErrorCode != raw.SQLITE_ERROR) {
                    command.bind(paramArgs);
                }
            } catch (CouchbaseLiteException) {
                Log.E(TAG, "Error when building sql '{0}' with params {1}", sql, Manager.GetObjectMapper().WriteValueAsString(paramArgs));
                throw;
            } catch (Exception e) {
                throw new CouchbaseLiteException(String.Format("Error when building sql '{0}' with params {1}", sql, 
                    Manager.GetObjectMapper().WriteValueAsString(paramArgs)), e);
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
            if (!IsOpen) {
                throw new CouchbaseLiteException("GetUpdateCommand called on closed database", StatusCode.BadRequest);
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

            if (!StringEx.IsNullOrWhiteSpace(whereClause))
            {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            if (whereArgs != null)
            {
                paramList.AddRange(whereArgs);
            }

            var sql = builder.ToString();
            var command = BuildCommand(_writeConnection, sql, paramList.ToArray<object>());

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
            if (!IsOpen) {
                throw new CouchbaseLiteException("GetInsertCommand called on closed database", StatusCode.BadRequest);
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
            sqlite3_stmt command = null;
            if (args != null)
            {
                Log.D(TAG, "Preparing statement: '{0}' with values: {1}", sql, String.Join(", ", args.Select(o => o == null ? "null" : o.ToString()).ToArray()));
            }
            else
            {
                Log.D(TAG, "Preparing statement: '{0}'", sql);
            }

            command = BuildCommand(_writeConnection, sql, args);

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
            if (!IsOpen) {
                throw new CouchbaseLiteException("GetDeleteCommand called on closed database", StatusCode.BadRequest);
            }

            var builder = new StringBuilder("DELETE FROM ");
            builder.Append(table);
            if (!StringEx.IsNullOrWhiteSpace(whereClause))
            {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            sqlite3_stmt command;

            command = BuildCommand(_writeConnection, builder.ToString(), whereArgs);

            return command;
        }

        private int ExecSQL(string sql, sqlite3 db, params object[] paramArgs)
        {
            var t = Factory.StartNew(()=>
            {
                sqlite3_stmt command = null;

                try {
                    command = BuildCommand(db, sql, paramArgs);
                    LastErrorCode = command.step();
                    if (LastErrorCode == SQLiteResult.ERROR) {
                        throw new CouchbaseLiteException("SQLite error: " + raw.sqlite3_errmsg(db), StatusCode.DbError);
                    }
                } catch (ugly.sqlite3_exception e) {
                    Log.E(TAG, "Error {0}, {1} ({2}) executing sql '{3}'".Fmt(e.errcode, db.extended_errcode(), raw.sqlite3_errmsg(db), sql), e);
                    LastErrorCode = e.errcode;
                    throw new CouchbaseLiteException(String.Format("Error executing sql '{0}'", sql), e) { Code = StatusCode.DbError };
                } finally {
                    if(command != null) {
                        command.Dispose();
                    }
                }
            }, _cts.Token);

            try
            {
                //FIXME.JHB:  This wait should be optional (API change)
                t.Wait(30000, _cts.Token);
            }
            catch (AggregateException ex)
            {
                throw ex.InnerException;
            }
            catch (OperationCanceledException)
            {
                //Closing the storage engine will cause the factory to stop processing, but still
                //accept new jobs into the scheduler.  If execution has gotten here, it means that
                //ExecSQL was called after Close, and the job will be ignored.  Might consider
                //subclassing the factory to avoid this awkward behavior
                Log.D(TAG, "StorageEngine closed, canceling operation");
                return 0;
            }

            if (t.Status != TaskStatus.RanToCompletion) {
                Log.E(TAG, "ExecSQL timed out waiting for Task #{0}", t.Id);
                throw new CouchbaseLiteException("ExecSQL timed out", StatusCode.InternalServerError);
            }

            return db.changes();
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