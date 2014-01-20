/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

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

namespace Couchbase.Lite.Storage
{

    internal class MonoSQLiteStorageEngine : SQLiteStorageEngine, IDisposable
    {
        static MonoSQLiteStorageEngine()
        {
            // Ensure Sqlite provider uses our custom collation function
            // that works directly on JSON encoded functions.
            SqliteFunction.RegisterFunction(typeof(CouchbaseSqliteCollationFunction));
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
                result = (Int32)command.ExecuteScalar();
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
            Connection.BeginTransaction(DefaultIsolationLevel);
        }

        public override void BeginTransaction (IsolationLevel isolationLevel)
        {
            currentTransaction = Connection.BeginTransaction(isolationLevel);
        }

        public override void EndTransaction ()
        {
            if (shouldCommit) {
                currentTransaction.Commit();
                shouldCommit = false;
            } else {
                currentTransaction.Rollback();
            }
            currentTransaction.Dispose();
        }

        public override void SetTransactionSuccessful ()
        {
            shouldCommit = true;
        }

        public override void ExecSQL (String sql, params Object[] bindArgs)
        {
            var command = Connection.CreateCommand();
            command.CommandText = sql;

            if (currentTransaction != null)
                command.Transaction = currentTransaction;

            var expectedCount = command.Parameters.Count;
            var foundCount = bindArgs.Length;

            if (foundCount != expectedCount){
                var message = "Incorrect number of SQL parameters: expected {0}, found {1}.".Fmt(foundCount, expectedCount);
                var err = new CouchbaseLiteException(message);
                Log.E(Tag, message, err);
                throw err;
            }

            for (int i = 0; i < expectedCount; i++) 
            {
                var param = command.Parameters [i];
                param.Value = bindArgs[i];
            }

            try {
                command.ExecuteNonQuery();
            } catch (Exception e) {
                Log.E(Tag, "Error executing sql'{0}'".Fmt(sql), e);
            } finally {
                command.Dispose();
            }
        }

        public override Cursor RawQuery (String sql, params String[] selectionArgs)
        {
            return RawQuery(sql, CommandBehavior.Default, selectionArgs);
        }

        public override Cursor RawQuery (String sql, CommandBehavior behavior, params String[] queryArgs)
        {
            var command = Connection.CreateCommand();
            command.CommandText = sql;

            if (currentTransaction != null)
                command.Transaction = currentTransaction;

            var expectedCount = command.Parameters.Count;
            var foundCount = queryArgs != null ? queryArgs.Length : 0;

            if (foundCount != expectedCount){
                var message = "Incorrect number of SQL parameters: expected {0}, found {1}.".Fmt(foundCount, expectedCount);
                var err = new CouchbaseLiteException(message);
                Log.E(Tag, message, err);
                throw err;
            }

            for (int i = 0; i < expectedCount; i++) 
            {
                var param = command.Parameters [i];
                param.Value = queryArgs[i];
            }

            Cursor cursor = null;
            try {
                var reader = command.ExecuteReader(behavior);
                cursor = new Cursor(reader);
            } catch (Exception e) {
                Log.E(Tag, "Error executing raw query '{0}'".Fmt(sql), e);
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

            var resultCount = -1L;
            try {
                resultCount = (Int64)command.ExecuteScalar ();
            } catch (Exception ex) {
                Log.E(Tag, "Error inserting into table " + table, ex);
            }
            return resultCount;
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
                resultCount = (Int32)command.ExecuteScalar ();
            } catch (Exception ex) {
                Log.E(Tag, "Error updating table " + table, ex);
            }
            return resultCount;
        }

        public override int Delete (String table, String whereClause, params String[] whereArgs)
        {
            Debug.Assert(!table.IsEmpty());

            var builder = new SqliteCommandBuilder();
            builder.SetAllValues = false;

            var command = GetDeleteCommand(table, whereClause, whereArgs);

            var resultCount = -1;
            try {
                resultCount = (Int32)command.ExecuteScalar ();
            } catch (Exception ex) {
                Log.E(Tag, "Error deleting from table " + table, ex);
            }
            return resultCount;
        }

        public override void Close ()
        {
            Connection.Close();
        }

        #endregion

        #region Non-public Members

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
            var valueSetLength = valueSet.LongCount();

            var sqlParams = new SqlParameter[valueSetLength + whereArgs.LongLength];
            var index = 0L;

            foreach(var column in valueSet)
            {
                if (index > 0) {
                    builder.Append(",");
                }
                builder.AppendFormat( "{0} = @{0}", column.Key);
                sqlParams[index++] = new SqlParameter(column.Key, column.Value);
            }

            if (!whereClause.IsEmpty()) {
                builder.Append(" WHERE ");
                builder.Append(whereClause);
            }

            for(; index < sqlParams.LongLength; index++) {
                sqlParams[index] = new SqlParameter(String.Empty, whereArgs[index - valueSetLength]);
            }

            var command = new SqliteCommand(builder.ToString(), Connection, currentTransaction);

            if (command.Parameters.Count != sqlParams.Length) {
                var e = new CouchbaseLiteException("{0} does not support the 'nullColumnHack'.".Fmt(Tag));
                Log.E(Tag, "SQL parameter count does not match the count of parameters provided.", e);
                throw e;
            }

            command.Parameters.Clear();
            command.Parameters.AddRange(sqlParams);

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
            var sqlParams = new SqlParameter[valueSet.LongCount()];
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

                sqlParams[index++] = new SqlParameter(column.Key, column.Value);
            }

            builder.Append(") VALUES (");
            builder.Append(valueBuilder);
            builder.Append(")");

            var command = new SqliteCommand(builder.ToString(), Connection, currentTransaction);
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
                builder.Append(whereClause);
            }

            var sqlParams = whereArgs.Select(arg=>new SqliteParameter(String.Empty, arg)).ToArray();
            var command = new SqliteCommand(builder.ToString(), Connection, currentTransaction);

            if (command.Parameters.Count != sqlParams.Length) {
                var e = new CouchbaseLiteException("{0} does not support the 'nullColumnHack'.".Fmt(Tag));
                Log.E(Tag, "SQL parameter count does not match the count of parameters provided.", e);
                throw e;
            }

            command.Parameters.Clear();
            command.Parameters.AddRange(sqlParams);

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
