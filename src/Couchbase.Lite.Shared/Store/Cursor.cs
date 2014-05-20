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
using System.Data;
using System.ComponentModel.Design;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using System.Linq;
using System.Text;

namespace Couchbase.Lite
{
    public class Cursor : IDisposable
    {
        const Int32 DefaultChunkSize = 8192;

        readonly SqliteDataReader reader;

        Boolean HasRows {
            get {
                 return reader.HasRows; 
            }
        }

        Int64 currentRow;

        public Cursor (SqliteDataReader reader)
        {
            this.reader = reader;
            currentRow = -1;
        }

        public bool MoveToNext ()
        {
            currentRow++;
            var moreRecords = reader.Read();
            return moreRecords;
        }

        public int GetInt (int columnIndex)
        {
            return reader.IsDBNull (columnIndex) ? -1 : reader.GetInt32(columnIndex);
        }

        public long GetLong (int columnIndex)
        {
            return reader.IsDBNull (columnIndex) ? -1 : reader.GetInt64(columnIndex);
        }

        public string GetString (int columnIndex)
        {
            return reader.IsDBNull (columnIndex) ? null : reader.GetString(columnIndex);
        }

        public byte[] GetBlob (int columnIndex)
        {
            return GetBlob(columnIndex, DefaultChunkSize);
        }

        public byte[] GetBlob (int columnIndex, int chunkSize)
        {
            if (reader.IsDBNull (columnIndex)) return new byte[2]; // NOTE.ZJG: Database.AppendDictToJSON assumes an empty json doc has a for a length of two.
            var r = reader;

            var fieldType = reader.GetFieldType(columnIndex);
            if (fieldType == typeof(String)) {
                return Encoding.UTF8.GetBytes(reader.GetString(columnIndex));
            }

            var chunkBuffer = new byte[chunkSize];
            var blob = new List<Byte>(chunkSize); // We know we'll be reading at least 1 chunk, so pre-allocate now to avoid an immediate resize.

            long bytesRead;
            do
            {
                chunkBuffer.Initialize(); // Resets all values back to zero.
                bytesRead = r.GetBytes(columnIndex, blob.Count, chunkBuffer, 0, chunkSize);
                blob.AddRange(chunkBuffer.Take(Convert.ToInt32(bytesRead)));
            } while (bytesRead > 0);

            return blob.ToArray();
        }

        public void Close ()
        {
            if (!reader.IsClosed) {
                reader.Close();
            }
        }

        public bool IsAfterLast ()
        {
            return !HasRows;
        }

        #region IDisposable implementation

        public void Dispose ()
        {
            if (reader != null && !reader.IsClosed)
            {
                reader.Close();
            }
        }

        #endregion
    }
}

