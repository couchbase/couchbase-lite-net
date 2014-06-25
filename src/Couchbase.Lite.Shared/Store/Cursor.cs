//
// Cursor.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
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

using SQLitePCL;
using Couchbase.Lite.Store;
using SQLitePCL.Ugly;

namespace Couchbase.Lite
{
    public class Cursor : IDisposable
    {
        const Int32 DefaultChunkSize = 8192;

        private sqlite3_stmt statement;
        private object dbLock;

        private Int32 currentStep = -1;

        Boolean HasRows {
            get {
                return currentStep == SQLiteResult.ROW; 
            }
        }

        Int64 currentRow;

        internal Cursor (sqlite3_stmt stmt, object dbLock)
        {
            this.dbLock = dbLock;
            this.statement = stmt;
            currentRow = -1;
            lock (dbLock) { currentStep = statement.step(); }
        }

        public bool MoveToNext ()
        {
            if (currentRow >= 0)
            {
                lock (dbLock) { currentStep = statement.step(); }
            }

            if (HasRows) currentRow++;
            return HasRows;
        }

        public int GetInt (int columnIndex)
        {
            lock (dbLock) { return statement.column_int(columnIndex); }
        }

        public long GetLong (int columnIndex)
        {
            lock (dbLock) { return statement.column_int64(columnIndex); }
        }

        public string GetString (int columnIndex)
        {
            lock (dbLock) { return statement.column_text(columnIndex); }
        }

        // TODO: Refactor this to return IEnumerable<byte>.
        public byte[] GetBlob (int columnIndex)
        {
            lock (dbLock) { return statement.column_blob(columnIndex); }
        }

//        public byte[] GetBlob (int columnIndex, int chunkSize)
//        {
//            SQLitePCL.SQLiteConnection
//
//            var result = statement[columnIndex];
//            if (result == null) return new byte[2]; // NOTE.ZJG: Database.AppendDictToJSON assumes an empty json doc has a for a length of two.
//            var r = statement;
//
//            var chunkBuffer = new byte[chunkSize];
//            var blob = new List<Byte>(chunkSize); // We know we'll be reading at least 1 chunk, so pre-allocate now to avoid an immediate resize.
//
//            long bytesRead;
//            do
//            {
//                chunkBuffer.Initialize(); // Resets all values back to zero.
//                bytesRead = r[columnIndex](columnIndex, blob.Count, chunkBuffer, 0, chunkSize);
//                blob.AddRange(chunkBuffer.Take(Convert.ToInt32(bytesRead)));
//            } while (bytesRead > 0);
//
//            return blob.ToArray();
//        }

        public void Close ()
        {
            if (statement == null) return;

            statement.Dispose();

            statement = null;
        }

        public bool IsAfterLast ()
        {
            return !HasRows;
        }

        #region IDisposable implementation

        public void Dispose ()
        {
            if (this.dbLock != null)
            {
                this.dbLock = null;
            }

            if (statement != null)
            {
                Close();
            }
        }

        #endregion
    }
}

