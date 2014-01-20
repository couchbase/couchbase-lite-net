using System;
using System.Data;
using System.ComponentModel.Design;
using System.Collections.Generic;
using Mono.Data.Sqlite;

namespace Couchbase.Lite
{
    public class Cursor : IDisposable
    {
        const Int32 DefaultChunkSize = 8192;

        readonly IDataReader reader;

        Int64 currentRow;

        public Cursor (IDataReader reader)
        {
            this.reader = reader;
            currentRow = -1;
        }

        public bool MoveToNext ()
        {
            currentRow++;
            return reader.NextResult();
        }

        public int GetInt (int columnIndex)
        {
            return reader.GetInt32(columnIndex);
        }

        public long GetLong (int columnIndex)
        {
            return reader.GetInt64(columnIndex);
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
            var chunkBuffer = new byte[chunkSize];
            var blob = new List<Byte>(chunkSize); // We know we'll be reading at least 1 chunk, so pre-allocate now to avoid an immediate resize.

            long bytesRemaining;
            do
            {
                bytesRemaining = reader.GetBytes(columnIndex, blob.Count, chunkBuffer, 0, chunkSize);
                blob.AddRange(chunkBuffer);
                chunkBuffer.Initialize(); // Resets all values back to zero.
            } while (bytesRemaining > 0);

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
            return currentRow > reader.RecordsAffected;
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

