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
using System;
using System.Data;

namespace Couchbase.Lite.Storage
{
	public abstract class SQLiteStorageEngine
	{
        public abstract bool Open(String path);

        public abstract Int32 GetVersion();

        public abstract void SetVersion(Int32 version);

        public abstract bool IsOpen { get; }

		public abstract void BeginTransaction();

        public abstract void BeginTransaction(IsolationLevel isolationLevel);

		public abstract void EndTransaction();

		public abstract void SetTransactionSuccessful();

		/// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
        public abstract void ExecSQL(string sql, params Object[] bindArgs);

        public abstract Cursor RawQuery(string sql, params String[] selectionArgs);

        public abstract Cursor RawQuery(string sql, CommandBehavior behavior, params String[] selectionArgs);

		public abstract long Insert(string table, string nullColumnHack, ContentValues values);

        public abstract long InsertWithOnConflict(string table, string nullColumnHack, ContentValues initialValues, ConflictResolutionStrategy conflictResolutionStrategy);

        public abstract int Update(string table, ContentValues values, string whereClause, params String[] whereArgs);

        public abstract int Delete(string table, string whereClause, params String[] whereArgs);

		public abstract void Close();
	}

}
