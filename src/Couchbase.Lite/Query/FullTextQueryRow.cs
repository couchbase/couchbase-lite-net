//
//  FullTextQueryRow.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Text;

using Couchbase.Lite.DB;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;

namespace Couchbase.Lite.Querying
{
    internal sealed unsafe class FullTextQueryRow : QueryRow, IFullTextQueryRow
    {
        #region Variables

        private readonly C4FullTextTerm[] _matches;
        private readonly C4Query* _query;

        #endregion

        #region Properties

        public string FullTextMatched
        {
            get {
                C4Error err;
                var retVal = Native.c4query_fullTextMatched(_query, DocumentID, Sequence, &err);
                if(retVal == null) {
                    throw new LiteCoreException(err);
                }

                return retVal;
            }
        }

        public uint MatchCount { get; }

        #endregion

        #region Constructors

        internal FullTextQueryRow(Database db, C4Query* query, C4QueryEnumerator* enumerator)
            : base(db, enumerator)
        {
            _query = query;
            MatchCount = enumerator->fullTextTermCount;
            _matches = new C4FullTextTerm[MatchCount];
            for(int i = 0; i < MatchCount; i++) {
                _matches[i] = enumerator->fullTextTerms[i];
            }
        }

        #endregion

        #region Public Methods

        public uint GetTermIndex(uint matchNumber)
        {
            if(matchNumber >= MatchCount) {
                throw new ArgumentOutOfRangeException(nameof(matchNumber), matchNumber, "Must be less than MatchCount");
            }

            return _matches[matchNumber].termIndex;
        }

        public Range GetTextRange(uint matchNumber)
        {
            if(matchNumber >= MatchCount) {
                throw new ArgumentOutOfRangeException(nameof(matchNumber), matchNumber, "Must be less than MatchCount");
            }

            var start = _matches[matchNumber].start;
            var length = _matches[matchNumber].length;
            using(var id = new C4String(DocumentID)) {
                C4Error err;
                var rawText = NativeRaw.c4query_fullTextMatched(_query, id.AsC4Slice(), Sequence, &err);
                if(rawText.buf == null) {
                    throw new LiteCoreException(err);
                }

                byte* bytes = (byte*)rawText.buf;
                return new Range(CharCountOfUTF8ByteRange(bytes, 0, start), CharCountOfUTF8ByteRange(bytes, start, length));
            }
        }

        #endregion

        #region Private Methods

        private uint CharCountOfUTF8ByteRange(byte* bytes, uint start, uint length)
        {
            if(length == 0) {
                return 0;
            }

            return (uint)Encoding.UTF8.GetCharCount(bytes + start, (int)length);
        }

        #endregion
    }
}
