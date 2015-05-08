//
//  ZipEntry.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

using Couchbase.Lite.Util;

namespace Couchbase.Lite.Util
{
    
    internal static class ZipArchive
    {

        #region Public Methods

        public static IEnumerable<ZipEntry> GetEntriesFromStream(Stream compressedStream)
        {
            ZipEntry next = new ZipEntry(compressedStream);
            while(next.IsValid) {
                yield return next;
                next.Dispose();
                next = new ZipEntry(compressedStream);
            }
        }

        #endregion
    }

    internal class ZipEntry : IDisposable
    {

        #region Constants

        private const string TAG = "ZipEntry";
        private static readonly long LOCAL_HEADER_SIG = GetLittleEndianNumberFromByteArray(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, 0, 4);

        #endregion

        #region Variables

        private MemoryStream _compressedData;

        #endregion

        #region Properties

        public bool IsValid { get; private set; }

        public bool IsDirectory { 
            get {
                return Filename.EndsWith("/");
            }
        }

        public string Filename { get; private set; }

        public Stream FileData { get; private set; }

        #endregion

        #region Constructors

        public ZipEntry(Stream compressedStream)
        {
            IsValid = true;
            var buffer = new byte[4];
            compressedStream.Read(buffer, 0, 4);
            var signature = GetLittleEndianNumberFromByteArray(buffer, 0, 4);
            if (signature != LOCAL_HEADER_SIG) {
                IsValid = false;
                return;
            }

            compressedStream.Seek(2, SeekOrigin.Current);
            var generalBit = new byte[2];
            compressedStream.Read(generalBit, 0, 2);
            if ((generalBit[1] & 0x1) != 0) {
                throw new InvalidDataException("Encryption is not supported on zips for this API");
            }

            compressedStream.Read(buffer, 0, 2);
            var compressionType = GetLittleEndianNumberFromByteArray(buffer, 0, 2);
            if (compressionType != 0 && compressionType != 8) {
                Log.W(TAG, "Invalid compression type: {0}", compressionType);
                IsValid = false;
                return;
            }

            compressedStream.Seek(8, SeekOrigin.Current);
            compressedStream.Read(buffer, 0, 4);
            var compressedLength = GetLittleEndianNumberFromByteArray(buffer, 0, 4);

            compressedStream.Seek(4, SeekOrigin.Current);
            compressedStream.Read(buffer, 0, 4);
            var filenameLength = GetLittleEndianNumberFromByteArray(buffer, 0, 2);
            var extraFieldLength = GetLittleEndianNumberFromByteArray(buffer, 2, 2);

            var filenameBytes = new byte[filenameLength];
            compressedStream.Read(filenameBytes, 0, filenameLength);

            bool dosEncoding = (generalBit[0] & 0x4) == 0;
            if (dosEncoding) {
                Filename = Encoding.GetEncoding("CP437").GetString(filenameBytes);
            } else {
                Filename = Encoding.UTF8.GetString(filenameBytes);
            }

            var rawData = new byte[compressedLength];
            compressedStream.Seek(extraFieldLength, SeekOrigin.Current);
            compressedStream.Read(rawData, 0, compressedLength);
            _compressedData = new MemoryStream(rawData);

            if (compressionType == 0) {
                FileData = _compressedData;
            } else {
                FileData = new DeflateStream(_compressedData, CompressionMode.Decompress, false);
            }
        }

        #endregion

        #region Private Methods

        private static int GetLittleEndianNumberFromByteArray(byte[] data, int startIndex, int length) {
            length = Math.Min(length, data.Length - startIndex);
            int retVal = 0;
            for (int i = length-1; i >= 0; i--) {
                retVal |= (data[startIndex + i] << i * 8);
            }

            return retVal;
        }

        #endregion

        #region IDisposable

        public void Dispose() {
            if (FileData != null) {
                FileData.Dispose();
            }
        }

        #endregion
    }
}

