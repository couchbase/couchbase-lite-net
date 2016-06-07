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
#elif CUSTOM_SQLITE
namespace Couchbase.Lite.Storage.CustomSQLite
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
        private const int TRANSACTION_MAX_RETRIES = 10;
        private const int TRANSACTION_MAX_RETRY_DELAY = 50; //milliseconds

        private const String TAG = "SqlitePCLRawStorageEngine";
        private sqlite3 _writeConnection;
        private sqlite3 _readConnection;
        private bool _readOnly; // Needed for issue with GetVersion()

        private string Path { get; set; }
        private TaskFactory Factory { get; set; }
        private CancellationTokenSource _cts = new CancellationTokenSource();

        #region ISQLiteStorageEngine

        public bool InTransaction 
        {
            get { return transactionCount > 0; }
        }

        public int LastErrorCode { get; private set; }

        // Returns true on success, false if encryption key is wrong, throws exception for other cases
        public bool Decrypt(SymmetricKey encryptionKey, sqlite3 connection)
        {
            #if !ENCRYPTION
            Log.To.Database.E(TAG, "Encryption not supported on this store, throwing...");
            throw new InvalidOperationException("Encryption not supported on this store");
            #else
            if (encryptionKey != null) {
                // http://sqlcipher.net/sqlcipher-api/#key
                var sql = String.Format("PRAGMA key = \"x'{0}'\"", encryptionKey.HexData);
                try {
                    ExecSQL(sql, connection);
                } catch(CouchbaseLiteException) {
                    Log.To.Database.E(TAG, "Decryption operation failed, rethrowing...");
                    throw;
                } catch(Exception e) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "Decryption operation failed");
                }
            }

            // Verify that encryption key is correct (or db is unencrypted, if no key given):
            var result = raw.sqlite3_exec(connection, "SELECT count(*) FROM sqlite_master");
            if (result != raw.SQLITE_OK) {
                if (result == raw.SQLITE_NOTADB) {
                    return false;
                } else {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG, "Cannot read from database ({0})", result);
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
                int readFlag = readOnly ? SQLITE_OPEN_READONLY : SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE;
                int writer_flags = SQLITE_OPEN_FILEPROTECTION_COMPLETEUNLESSOPEN | readFlag | SQLITE_OPEN_NOMUTEX;
                OpenSqliteConnection(writer_flags, encryptionKey, out _writeConnection);

                #if ENCRYPTION
                if (!Decrypt(encryptionKey, _writeConnection)) {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Unauthorized, TAG,
                        "Decryption of database failed");
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
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Unauthorized, TAG,
                        "Decryption of database failed");
                }
                #endif
            } catch(CouchbaseLiteException) {
                Log.To.Database.W(TAG, "Error opening SQLite storage engine, rethrowing...");
                throw;
            } catch (Exception ex) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, ex, TAG, "Failed to open SQLite storage engine");
            }

            return true;
        }

        void OpenSqliteConnection(int flags, SymmetricKey encryptionKey, out sqlite3 db)
        {
            LastErrorCode = raw.sqlite3_open_v2(Path, out db, flags, null);
            if (LastErrorCode != raw.SQLITE_OK) {
                Path = null;
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                    "Failed to open SQLite storage engine at path {0} ({1})", Path, LastErrorCode);
            }

#if !__ANDROID__ && !NET_3_5 && VERBOSE
                var i = 0;
                var val = raw.sqlite3_compileoption_get(i);
                while (val != null)
                {
                    Log.To.Database.V(TAG, String.Format("Sqlite Config: {0}", val));
                    val = raw.sqlite3_compileoption_get(++i);
                }
#endif

            Log.To.Database.I(TAG, "Open {0} (flags={1}{2})", Path, flags, (encryptionKey != null ? ", encryption key given" : ""));

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
                Log.To.Database.E(TAG, "Error getting user version", e);
            } finally {
                statement.Dispose();
            }

            return result;
        }

        public void SetVersion(int version)
        {
            if (_readOnly) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Attempting to write to a readonly database");
            }

            const string commandText = "PRAGMA user_version = ?";

            Log.To.TaskScheduling.V(TAG, "Scheduling SetVersion({0})", version);
            Factory.StartNew(() =>
            {
                Log.To.TaskScheduling.V(TAG, "Running SetVersion({0})", version);
                sqlite3_stmt statement = BuildCommand(_writeConnection, commandText, null);

                if ((LastErrorCode = raw.sqlite3_bind_int(statement, 1, version)) == raw.SQLITE_ERROR)
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                        "Unable to set version to {0} ({1})", version, LastErrorCode);

                try {
                    LastErrorCode = statement.step();
                    if (LastErrorCode != raw.SQLITE_OK) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                            "Unable to set version to {0} ({1})", version, LastErrorCode);
                    }
                } catch (Exception e) {
                    Log.To.Database.W(TAG, "Error getting user version, recording error...", e);
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

        public bool BeginTransaction()
        {
            if (_readOnly) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Transactions not allowed on a readonly database");
            }

            if (!IsOpen) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "BeginTransaction called on closed database");
            }

            // NOTE.ZJG: Seems like we should really be using TO SAVEPOINT
            //           but this is how Android SqliteDatabase does it,
            //           so I'm matching that for now.
            var value = Interlocked.Increment(ref transactionCount);

            if (value == 1)
            {
                Log.To.TaskScheduling.V(TAG, "Scheduling BeginTransaction()...");
                var t = Factory.StartNew(() =>
                {
                    Log.To.TaskScheduling.V(TAG, "Running BeginTransaction()...");
                    try {
                        using (var statement = BuildCommand(_writeConnection, "BEGIN IMMEDIATE TRANSACTION", null)) {
                            statement.step_done();
                            return true;
                        }
                    } catch (Exception e) {
                        LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                        Log.To.Database.E(TAG, "Error beginning transaction, recording error...", e);
                        Interlocked.Decrement(ref transactionCount);
                        return false;
                    }
                });
                return t.Result;
            } else {
                Log.To.TaskScheduling.V(TAG, "Scheduling begin SAVEPOINT...");
                var t = Factory.StartNew(() =>
                {
                    Log.To.TaskScheduling.V(TAG, "Running begin SAVEPOINT()...");
                    try {
                        var sql = String.Format("SAVEPOINT cbl_{0}", value - 1);
                        using (var statement = BuildCommand(_writeConnection, sql, null)) {
                            
                            statement.step_done();
                            return true;
                        }
                    } catch (Exception e) {
                        LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                        Log.To.Database.E(TAG, "Error beginning transaction, recording error...", e);
                        Interlocked.Decrement(ref transactionCount);
                        return false;
                    }
                });
                return t.Result;
            }

            return true;
        }

        public bool EndTransaction(bool successful)
        {
            if (_readOnly) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Transactions not allowed on a readonly database");
            }

            if (!IsOpen) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "EndTransaction called on closed database");
            }

            var count = Interlocked.Decrement(ref transactionCount);
            if (count > 0) {
                Log.To.TaskScheduling.V(TAG, "Scheduling end SAVEPOINT");
                var t = Factory.StartNew(() =>
                {
                    Log.To.TaskScheduling.V(TAG, "Running end SAVEPOINT");
                    try {
                        if (successful) {
                            var sql = String.Format("RELEASE SAVEPOINT cbl_{0}", count);
                            using (var statement = BuildCommand(_writeConnection, sql, null)) {
                                statement.step_done();
                                return true;
                            }
                        }
                        else {
                            var sql = String.Format("ROLLBACK TO SAVEPOINT cbl_{0}", count);
                            using (var statement = BuildCommand(_writeConnection, sql, null)) {
                                statement.step_done();
                                return true;
                            }
                        }
                    } catch (Exception e) {
                        Log.To.Database.E(TAG, "Error ending transaction, recording error...", e);
                        LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                        Interlocked.Increment(ref transactionCount);
                        return false;
                    }
                });
                return t.Result;
            } else {
                Log.To.TaskScheduling.V(TAG, "Scheduling EndTransaction()");
                var t = Factory.StartNew(() =>
                {
                    Log.To.TaskScheduling.V(TAG, "Running EndTransaction()");
                    try {
                        if (successful) {
                            using (var statement = BuildCommand(_writeConnection, "COMMIT", null)) {
                                statement.step_done();
                                return true;
                            }
                        }
                        else {
                            using (var statement = BuildCommand(_writeConnection, "ROLLBACK", null)) {
                                statement.step_done();
                                return true;
                            }
                        }
                    } catch (Exception e) {
                        Log.To.Database.E(TAG, "Error ending transaction, recording error...", e);
                        LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                        Interlocked.Increment(ref transactionCount);
                        return false;
                    }
                });
                return t.Result;
            }
        }

        public bool RunInTransaction(RunInTransactionDelegate block)
        {
            var status = false;
            var t = Factory.StartNew(() =>
            {
                var keepGoing = false;
                int retries = 0;
                do {
                    keepGoing = false;
                    if (!BeginTransaction()) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                            "Error beginning begin transaction");
                    }

                    try {
                        status = block();
                    } catch (CouchbaseLiteException e) {
                        if (e.Code == StatusCode.DbBusy) {
                            // retry if locked out
                            if (transactionCount > 1) {
                                break;
                            }

                            if (++retries > TRANSACTION_MAX_RETRIES) {
                                Log.To.Database.E(TAG, "Db busy, too many retries, giving up");
                                break;
                            }

                            Log.To.Database.I(TAG, "Db busy, retrying transaction ({0})", retries);
                            Thread.Sleep(TRANSACTION_MAX_RETRY_DELAY);
                            keepGoing = true;
                        } else {
                            Log.To.Database.E(TAG, "Failed to run transaction, rethrowing...");
                            status = false;
                            throw;
                        }
                    } catch (Exception e) {
                        status = false;
                        throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG, "Error running transaction");
                    } finally {
                        EndTransaction(status);
                    }
                } while(keepGoing);
            });
            try {
                t.Wait();
            } catch(Exception e) {
                throw Misc.UnwrapAggregate(e);
            }

            return status;
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "IntransactionRawQuery called on closed database");
            }

            if (transactionCount == 0) 
            {
                return RawQuery(sql, paramArgs);
            }

            Log.To.TaskScheduling.V(TAG, "Scheduling InTransactionRawQuery");
            var t = Factory.StartNew(() =>
            {
                Log.To.TaskScheduling.V(TAG, "Running InTransactionRawQuery");
                Cursor cursor = null;
                sqlite3_stmt command = null;
                try {
                    Log.To.Database.V(TAG, "RawQuery sql: {0} ({1})", sql, String.Join(", ", paramArgs.ToStringArray()));
                    command = BuildCommand (_writeConnection, sql, paramArgs);
                    cursor = new Cursor(command);
                } catch (Exception e) {
                    if (command != null) {
                        command.Dispose();
                    }

                    Log.To.Database.E(TAG, String.Format("Error executing raw query '{0}', rethrowing...", sql), e);
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "RawQuery called on closed database");
            }

            Cursor cursor = null;
            sqlite3_stmt command = null;

            Log.To.TaskScheduling.V(TAG, "Scheduling RawQuery");
            var t = Factory.StartNew (() => 
            {
                Log.To.TaskScheduling.V(TAG, "Running RawQuery");
                try {
                    Log.To.Database.V (TAG, "RawQuery sql: {0} ({1})", sql, String.Join (", ", paramArgs.ToStringArray ()));
                    command = BuildCommand (_readConnection, sql, paramArgs);
                    cursor = new Cursor (command);
                } catch (Exception e) {
                    if (command != null) {
                        command.Dispose ();
                    }

                    var args = new SecureLogJsonString(paramArgs, LogMessageSensitivity.PotentiallyInsecure);
                    Log.To.Database.E (TAG, String.Format("Error executing raw query '{0}' with values '{1}', rethrowing...", 
                        sql, paramArgs == null ? (object)String.Empty : 
                        new SecureLogJsonString(args, LogMessageSensitivity.PotentiallyInsecure)), e);
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Attempting to write to a readonly database");
            }

            if (!StringEx.IsNullOrWhiteSpace(nullColumnHack)) {
                throw new InvalidOperationException("Don't use nullColumnHack");
            }

            Log.To.TaskScheduling.V(TAG, "Scheduling InsertWithOnConflict");
            var t = Factory.StartNew(() =>
            {
                Log.To.TaskScheduling.V(TAG, "Running InsertWithOnConflict");
                var lastInsertedId = -1L;
                var command = GetInsertCommand(table, initialValues, conflictResolutionStrategy);

                try {
                    LastErrorCode = command.step();
                    command.Dispose();
                    if (LastErrorCode == raw.SQLITE_ERROR) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                            raw.sqlite3_errmsg(_writeConnection));
                    }

                    int changes = _writeConnection.changes();
                    if (changes > 0)
                    {
                        lastInsertedId = _writeConnection.last_insert_rowid();
                    }

                    if (lastInsertedId == -1L) {
                        if(conflictResolutionStrategy != ConflictResolutionStrategy.Ignore) {
                            throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                                "Error inserting {0} using {1}", initialValues, command);
                        }
                    } else {
                        Log.To.Database.V(TAG, "Inserting row {0} into {1} with values {2}", lastInsertedId, table, initialValues);
                    }

                } 
                catch(CouchbaseLiteException) {
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    Log.To.Database.E(TAG, "Error inserting into table {0}, rethrowing...", table);
                    throw;
                } catch (Exception ex) {
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    throw Misc.CreateExceptionAndLog(Log.To.Database, ex, TAG, 
                        "Error inserting into table {0}", table);
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Attempting to write to a readonly database");
            }

            Debug.Assert(!StringEx.IsNullOrWhiteSpace(table));
            Debug.Assert(values != null);

            Log.To.TaskScheduling.V(TAG, "Scheduling Update");
            var t = Factory.StartNew(() =>
            {
                Log.To.TaskScheduling.V(TAG, "Running Update");
                var resultCount = 0;
                var command = GetUpdateCommand(table, values, whereClause, whereArgs);
                try
                {
                    LastErrorCode = command.step();
                    if (LastErrorCode == raw.SQLITE_ERROR)
                        throw new CouchbaseLiteException(raw.sqlite3_errmsg(_writeConnection),
                            StatusCode.DbError);
                }
                catch (ugly.sqlite3_exception ex)
                {
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    var msg = raw.sqlite3_extended_errcode(_writeConnection);
                    Log.To.Database.E(TAG, String.Format("Error {0}: \"{1}\" while updating table {2}", ex.errcode, msg, table), ex);
                }

                resultCount = _writeConnection.changes();
                if (resultCount < 0)
                {
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                        "Error updating {0} with command '{1}'", values, command);
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.Forbidden, TAG,
                    "Attempting to write to a readonly database");
            }

            Debug.Assert(!StringEx.IsNullOrWhiteSpace(table));

            Log.To.TaskScheduling.V(TAG, "Scheduling Delete");
            var t = Factory.StartNew(() =>
            {
                Log.To.TaskScheduling.V(TAG, "Running Delete");
                var resultCount = -1;
                var command = GetDeleteCommand(table, whereClause, whereArgs);
                try
                {
                    var result = command.step();
                    if (result == raw.SQLITE_ERROR) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                            "Error deleting from table {0} ({1})", table, result);
                    }

                    resultCount = _writeConnection.changes();
                    if (resultCount < 0)
                    {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                            "Failed to delete the requested records");
                    }

                }
                catch (Exception ex)
                {
                    LastErrorCode = raw.sqlite3_errcode(_writeConnection);
                    Log.To.Database.E(TAG, String.Format("Error {0} when deleting from table {1}, rethrowing...", _writeConnection.extended_errcode(), table), ex);
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
                while ((next = dbCopy.next_stmt(next))!= null) {
                    next.Dispose();
                } 

                dbCopy.close();
            } catch (KeyNotFoundException ex) {
                // Appears to be a bug in sqlite3.find_stmt. Concurrency issue in static dictionary?
                // Assuming we're done.
                Log.To.Database.W(TAG, "Abandoning database close.", ex);
            } catch (ugly.sqlite3_exception ex) {
                Log.To.Database.I(TAG, "Retrying database close due to exception.", ex);
                // Assuming a basic retry fixes this.
                Thread.Sleep(5000);
                dbCopy.close();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            try {
                dbCopy.Dispose();
            } catch (Exception ex) {
                Log.To.Database.W(TAG, "Error while closing database, continuing...", ex);
            }
        }

        #endregion

        #region Non-public Members

        private sqlite3_stmt BuildCommand(sqlite3 db, string sql, object[] paramArgs)
        {
            if (db == null) {
                Log.To.Database.E(TAG, "db cannot be null in BuildCommand, throwing...");
                throw new ArgumentNullException("db");
            }

            if (!IsOpen) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "BuildCommand called on closed database");
            }

            sqlite3_stmt command = null;
            try {
                lock(Cursor.StmtDisposeLock) {
                    LastErrorCode = raw.sqlite3_prepare_v2(db, sql, out command);
                }

                if (LastErrorCode != raw.SQLITE_OK || command == null) {
                    Log.To.Database.E(TAG, "sqlite3_prepare_v2: {0}", LastErrorCode);
                }

                if (paramArgs != null && paramArgs.Length > 0 && command != null && LastErrorCode != raw.SQLITE_ERROR) {
                    command.bind(paramArgs);
                }
            } catch (CouchbaseLiteException) {
                Log.To.Database.E(TAG, "Error when building sql '{0}' with params {1}, rethrowing...", sql, 
                    new SecureLogJsonString(paramArgs, LogMessageSensitivity.PotentiallyInsecure));
                throw;
            } catch (Exception e) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, e, TAG,
                    "Error when building sql '{0}' with params {1}", sql, 
                    new SecureLogJsonString(paramArgs, LogMessageSensitivity.PotentiallyInsecure));
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "GetUpdateCommand called on closed database");
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "GetInsertCommand called on closed database");
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
            if (args != null) {
                Log.To.Database.V(TAG, "Preparing statement: '{0}' with values: {1}", sql, new SecureLogJsonString(args, LogMessageSensitivity.PotentiallyInsecure));
            } else {
                Log.To.Database.V(TAG, "Preparing statement: '{0}'", sql);
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
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.BadRequest, TAG,
                    "GetDeleteCommand called on closed database");
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
            Log.To.TaskScheduling.V(TAG, "Scheduling ExecSQL");
            var t = Factory.StartNew(()=>
            {
                Log.To.TaskScheduling.V(TAG, "Running ExecSQL");
                sqlite3_stmt command = null;

                try {
                    command = BuildCommand(db, sql, paramArgs);
                    LastErrorCode = command.step();
                    if (LastErrorCode == raw.SQLITE_ERROR) {
                        throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                            "SQLite error in ExecSQL: {0}", raw.sqlite3_errmsg(db));
                    }
                } catch (ugly.sqlite3_exception e) {
                    Log.To.Database.E(TAG, String.Format("Error {0}, {1} ({2}) executing sql '{3}'", e.errcode, db.extended_errcode(), raw.sqlite3_errmsg(db), sql), e);
                    LastErrorCode = e.errcode;
                    throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.DbError, TAG,
                        "Error {0}, {1} ({2}) executing sql '{3}'", 
                        e.errcode, db.extended_errcode(), raw.sqlite3_errmsg(db), sql);
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
                Log.To.Database.I(TAG, "StorageEngine closed, canceling operation");
                return 0;
            }

            if (t.Status != TaskStatus.RanToCompletion) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, StatusCode.InternalServerError, TAG,
                    "ExecSQL timed out waiting for Task #{0}", t.Id);
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