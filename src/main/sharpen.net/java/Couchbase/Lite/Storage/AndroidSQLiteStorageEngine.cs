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

using System.Collections.Generic;
using Android.Content;
using Android.Database;
using Android.Database.Sqlite;
using Couchbase.Lite;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite.Storage
{
	public class AndroidSQLiteStorageEngine : SQLiteStorageEngine
	{
		public const string Tag = "AndroidSQLiteStorageEngine";

		private SQLiteDatabase database;

		public override bool Open(string path)
		{
			if (database != null && database.IsOpen())
			{
				return true;
			}
			try
			{
				database = SQLiteDatabase.OpenDatabase(path, null, SQLiteDatabase.CreateIfNecessary
					);
				TDCollateJSON.RegisterCustomCollators(database);
			}
			catch (SQLiteException e)
			{
				Log.E(Tag, "Error opening", e);
				if (database != null)
				{
					database.Close();
				}
				return false;
			}
			return database.IsOpen();
		}

		public override int GetVersion()
		{
			return database.GetVersion();
		}

		public override void SetVersion(int version)
		{
			database.SetVersion(version);
		}

		public override bool IsOpen()
		{
			return database.IsOpen();
		}

		public override void BeginTransaction()
		{
			database.BeginTransaction();
		}

		public override void EndTransaction()
		{
			database.EndTransaction();
		}

		public override void SetTransactionSuccessful()
		{
			database.SetTransactionSuccessful();
		}

		/// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
		public override void ExecSQL(string sql)
		{
			try
			{
				database.ExecSQL(sql);
			}
			catch (SQLException e)
			{
				throw new SQLException(e);
			}
		}

		/// <exception cref="Couchbase.Lite.Storage.SQLException"></exception>
		public override void ExecSQL(string sql, object[] bindArgs)
		{
			try
			{
				database.ExecSQL(sql, bindArgs);
			}
			catch (SQLException e)
			{
				throw new SQLException(e);
			}
		}

		public override Cursor RawQuery(string sql, string[] selectionArgs)
		{
			return new AndroidSQLiteStorageEngine.SQLiteCursorWrapper(this, database.RawQuery
				(sql, selectionArgs));
		}

		public override long Insert(string table, string nullColumnHack, ContentValues values
			)
		{
			return database.Insert(table, nullColumnHack, _toAndroidContentValues(values));
		}

		public override long InsertWithOnConflict(string table, string nullColumnHack, ContentValues
			 initialValues, int conflictAlgorithm)
		{
			return database.InsertWithOnConflict(table, nullColumnHack, _toAndroidContentValues
				(initialValues), conflictAlgorithm);
		}

		public override int Update(string table, ContentValues values, string whereClause
			, string[] whereArgs)
		{
			return database.Update(table, _toAndroidContentValues(values), whereClause, whereArgs
				);
		}

		public override int Delete(string table, string whereClause, string[] whereArgs)
		{
			return database.Delete(table, whereClause, whereArgs);
		}

		public override void Close()
		{
			database.Close();
		}

		private ContentValues _toAndroidContentValues(ContentValues values)
		{
			ContentValues contentValues = new ContentValues(values.Size());
			foreach (KeyValuePair<string, object> value in values.ValueSet())
			{
				if (value.Value == null)
				{
					contentValues.Put(value.Key, (string)null);
				}
				else
				{
					if (value.Value is string)
					{
						contentValues.Put(value.Key, (string)value.Value);
					}
					else
					{
						if (value.Value is int)
						{
							contentValues.Put(value.Key, (int)value.Value);
						}
						else
						{
							if (value.Value is long)
							{
								contentValues.Put(value.Key, (long)value.Value);
							}
							else
							{
								if (value.Value is bool)
								{
									contentValues.Put(value.Key, (bool)value.Value);
								}
								else
								{
									if (value.Value is byte[])
									{
										contentValues.Put(value.Key, (byte[])value.Value);
									}
								}
							}
						}
					}
				}
			}
			return contentValues;
		}

		private class SQLiteCursorWrapper : Cursor
		{
			private Cursor delegate_;

			public SQLiteCursorWrapper(AndroidSQLiteStorageEngine _enclosing, Cursor delegate_
				)
			{
				this._enclosing = _enclosing;
				this.delegate_ = delegate_;
			}

			public virtual bool MoveToNext()
			{
				return this.delegate_.MoveToNext();
			}

			public virtual bool IsAfterLast()
			{
				return this.delegate_.IsAfterLast();
			}

			public virtual string GetString(int columnIndex)
			{
				return this.delegate_.GetString(columnIndex);
			}

			public virtual int GetInt(int columnIndex)
			{
				return this.delegate_.GetInt(columnIndex);
			}

			public virtual long GetLong(int columnIndex)
			{
				return this.delegate_.GetLong(columnIndex);
			}

			public virtual byte[] GetBlob(int columnIndex)
			{
				return this.delegate_.GetBlob(columnIndex);
			}

			public virtual void Close()
			{
				this.delegate_.Close();
			}

			private readonly AndroidSQLiteStorageEngine _enclosing;
		}
	}
}
