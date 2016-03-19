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
#if !NOSQLITE
using System;
using Couchbase.Lite.Store;
using Couchbase.Lite.Util;

using SQLitePCL;
using SQLitePCL.Ugly;

namespace Couchbase.Lite
{

    /// <summary>
    /// A class for encapsulating a result set from a database
    /// </summary>
    public class Cursor : IDisposable
    {
        #region Constants

        private const int DEFAULT_CHUNK_SIZE = 8192;

        #endregion

        #region Variables

        /// <summary>
        /// An object to lock on when interacting with statements owned by this Cursor
        /// </summary>
        /// <remarks>
        /// This is a bit of a hack and will be fixed in a later refactor
        /// </remarks>
        public static object StmtDisposeLock = new object();

        private sqlite3_stmt _statement;
        private int _currentStep = -1;
        private long _currentRow;

        #endregion

        #region Properties

        private bool HasRows {
            get {
                return _currentStep == SQLiteResult.ROW; 
            }
        }

        #endregion

        #region Constructors

        //NOTE.JHB: Can throw an exception
        internal Cursor (sqlite3_stmt stmt)
        {
            this._statement = stmt;
            _currentRow = -1;
            _currentStep = _statement.step();

            if (_currentStep != raw.SQLITE_OK && _currentStep != raw.SQLITE_ROW && _currentStep != raw.SQLITE_DONE) {
                Log.E ("Cursor", "currentStep: " + _currentStep);
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Moves to the next result in the set
        /// </summary>
        /// <returns><c>true</c>, if the cursor was able to move, <c>false</c> otherwise.</returns>
        public bool MoveToNext ()
        {
            if (_currentRow >= 0) {
                try {
                    _currentStep = _statement.step();
                } catch(ugly.sqlite3_exception e) {
                    Log.E("Cursor", "Couldn't move to next row: {0} ({1})", e.errcode, e.errmsg);
                    throw;
                }
            }

            if (HasRows) _currentRow++;
            return HasRows;
        }

        /// <summary>
        /// Gets the value of the column at the given index as an integer
        /// </summary>
        /// <returns>The value of the column as an integer</returns>
        /// <param name="columnIndex">The index of the column to evaluate</param>
        public int GetInt (int columnIndex)
        {
            return _statement.column_int(columnIndex);
        }

        /// <summary>
        /// Gets the value of the column at the given index as a long integer
        /// </summary>
        /// <returns>The value of the column as a long integer</returns>
        /// <param name="columnIndex">The index of the column to evaluate</param>
        public long GetLong (int columnIndex)
        {
            return _statement.column_int64(columnIndex);
        }

        /// <summary>
        /// Gets the value of the column at the given index as a string
        /// </summary>
        /// <returns>The value of the column as a string</returns>
        /// <param name="columnIndex">The index of the column to evaluate</param>
        public string GetString (int columnIndex)
        {
            return _statement.column_text(columnIndex);
        }

        // TODO: Refactor this to return IEnumerable<byte>.
        /// <summary>
        /// Gets the value of the column at the given index as a blob
        /// </summary>
        /// <returns>The value of the column as a blob</returns>
        /// <param name="columnIndex">The index of the column to evaluate</param>
        public byte[] GetBlob (int columnIndex)
        {
            return _statement.column_blob(columnIndex);
        }

        /// <summary>
        /// Closes the cursor and frees its resources
        /// </summary>
        public void Close ()
        {
            Dispose();
        }

        /// <summary>
        /// Returns whether or not the cursor is at the end of the result set
        /// </summary>
        /// <returns><c>true</c> if this instance is at the end; otherwise, <c>false</c>.</returns>
        public bool IsAfterLast ()
        {
            return !HasRows;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Releases all resource used by the <see cref="Couchbase.Lite.Cursor"/> object.
        /// </summary>
        /// <remarks>Call <see cref="Dispose"/> when you are finished using the <see cref="Couchbase.Lite.Cursor"/>. The
        /// <see cref="Dispose"/> method leaves the <see cref="Couchbase.Lite.Cursor"/> in an unusable state. After
        /// calling <see cref="Dispose"/>, you must release all references to the <see cref="Couchbase.Lite.Cursor"/> so
        /// the garbage collector can reclaim the memory that the <see cref="Couchbase.Lite.Cursor"/> was occupying.</remarks>
        public void Dispose ()
        {
            if (_statement == null) {
                return;
            }

            lock (StmtDisposeLock) 
            {
                _statement.Dispose ();
                _statement = null;
            }
        }

        #endregion
    }
}
#endif