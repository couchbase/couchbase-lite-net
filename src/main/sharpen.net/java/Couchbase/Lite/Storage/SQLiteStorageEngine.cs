/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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

namespace Couchbase.Lite.Storage
{
	public abstract class SQLiteStorageEngine
	{
		public const int ConflictNone = 0;

		public const int ConflictIgnore = 4;

		public const int ConflictReplace = 5;

		public abstract bool Open(string path);

		public abstract int GetVersion();

		public abstract void SetVersion(int version);

		public abstract bool IsOpen();

		public abstract void BeginTransaction();

		public abstract void EndTransaction();

		public abstract void SetTransactionSuccessful();

		/// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
		public abstract void ExecSQL(string sql);

		/// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
		public abstract void ExecSQL(string sql, object[] bindArgs);

		public abstract Cursor RawQuery(string sql, string[] selectionArgs);

		public abstract long Insert(string table, string nullColumnHack, ContentValues values
			);

		public abstract long InsertWithOnConflict(string table, string nullColumnHack, ContentValues
			 initialValues, int conflictAlgorithm);

		public abstract int Update(string table, ContentValues values, string whereClause
			, string[] whereArgs);

		public abstract int Delete(string table, string whereClause, string[] whereArgs);

		public abstract void Close();
	}
}
