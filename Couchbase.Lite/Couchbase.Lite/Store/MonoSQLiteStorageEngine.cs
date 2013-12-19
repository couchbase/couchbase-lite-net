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

namespace Couchbase.Lite.Storage
{

    internal class MonoSQLiteStorageEngine : SQLiteStorageEngine {
        #region implemented abstract members of SQLiteStorageEngine

        public override bool Open (string path)
        {
            throw new System.NotImplementedException ();
        }

        public override int GetVersion ()
        {
            throw new System.NotImplementedException ();
        }

        public override void SetVersion (int version)
        {
            throw new System.NotImplementedException ();
        }

        public override bool IsOpen ()
        {
            throw new System.NotImplementedException ();
        }

        public override void BeginTransaction ()
        {
            throw new System.NotImplementedException ();
        }

        public override void EndTransaction ()
        {
            throw new System.NotImplementedException ();
        }

        public override void SetTransactionSuccessful ()
        {
            throw new System.NotImplementedException ();
        }

        public override void ExecSQL (string sql)
        {
            throw new System.NotImplementedException ();
        }

        public override void ExecSQL (string sql, object[] bindArgs)
        {
            throw new System.NotImplementedException ();
        }

        public override Cursor RawQuery (string sql, IEnumerable<string> selectionArgs)
        {
            throw new System.NotImplementedException ();
        }

        public override long Insert (string table, string nullColumnHack, ContentValues values)
        {
            throw new System.NotImplementedException ();
        }

        public override long InsertWithOnConflict (string table, string nullColumnHack, ContentValues initialValues, int conflictAlgorithm)
        {
            throw new System.NotImplementedException ();
        }

        public override int Update (string table, ContentValues values, string whereClause, string[] whereArgs)
        {
            throw new System.NotImplementedException ();
        }

        public override int Delete (string table, string whereClause, string[] whereArgs)
        {
            throw new System.NotImplementedException ();
        }

        public override void Close ()
        {
            throw new System.NotImplementedException ();
        }

        #endregion
 }
}
