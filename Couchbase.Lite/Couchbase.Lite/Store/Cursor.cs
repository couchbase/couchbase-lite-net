using System;
using System.Data;
using System.ComponentModel.Design;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using System.Linq;

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
            return reader.GetString(columnIndex);
        }

        public byte[] GetBlob (int columnIndex)
        {
            return GetBlob(columnIndex, DefaultChunkSize);
        }

        public byte[] GetBlob (int columnIndex, int chunkSize)
        {
            if (reader.IsDBNull (columnIndex)) return new byte[2]; // NOTE.ZJG: Database.AppendDictToJSON assumes an empty json doc has a for a length of two.

            var chunkBuffer = new byte[chunkSize];
            var blob = new List<Byte>(chunkSize); // We know we'll be reading at least 1 chunk, so pre-allocate now to avoid an immediate resize.

            long bytesRead;
            do
            {
                chunkBuffer.Initialize(); // Resets all values back to zero.
                bytesRead = reader.GetBytes(columnIndex, blob.Count, chunkBuffer, 0, chunkSize);
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
            return !HasRows; //currentRow > reader.RecordsAffected;
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

