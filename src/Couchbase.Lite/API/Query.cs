//
//  Query.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using Couchbase.Lite.Querying;
using LiteCore;
using LiteCore.Interop;
using LiteCore.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite
{
    public sealed class SortDescriptor
    {
        public string Key { get; }

        public bool Ascending { get; }


    }

    public sealed unsafe class Query
    {
        private C4Query* _c4query;

        public Database Database { get; }

        public ulong Skip { get; set; }

        public ulong Limit { get; set; }

        public IDictionary<string, object> Parameters { get; }

        internal Query(Database db)
        {
            Database = db;
        }

        public IEnumerable<QueryRow> Run()
        {
            C4QueryOptions options = C4QueryOptions.Default;
            options.skip = Skip;
            options.limit = Limit;

            var paramJSON = default(string);
            if(Parameters.Any()) {
                paramJSON = JsonConvert.SerializeObject(Parameters);
            }

            return new QueryEnumerator(Database, _c4query, options, paramJSON);
        }
    }

    public unsafe class QueryRow
    {
        protected readonly Database _db;

        public string DocumentID { get; }

        public ulong Sequence { get; set; }

        public Document Document
        {
            get {
                var retVal = _db.GetDocument(DocumentID);
                Debug.Assert(retVal != null);
                return retVal;
            }
        }

        internal QueryRow(Database db, C4QueryEnumerator* enumerator)
        {
            _db = db;
            DocumentID = enumerator->docID.CreateString();
            Debug.Assert(DocumentID != null);
            Sequence = enumerator->docSequence;
        }
    }

    public sealed unsafe class FullTextQueryRow : QueryRow
    {
        private readonly C4Query* _query;
        private readonly C4FullTextTerm[] _matches;

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

        public Range GetTextRange(uint matchNumber)
        {
            if(matchNumber >= MatchCount) {
                throw new ArgumentOutOfRangeException(nameof(matchNumber), matchNumber, "Must be less than MatchCount");
            }

            uint start = _matches[matchNumber].start;
            uint length = _matches[matchNumber].length;
            C4Error err;
            using(var id = new C4String(DocumentID)) {
                var rawText = NativeRaw.c4query_fullTextMatched(_query, id.AsC4Slice(), Sequence, &err);
                if(rawText.buf == null) {
                    throw new LiteCoreException(err);
                }

                byte* bytes = (byte *)rawText.buf;
                return new Range(CharCountOfUTF8ByteRange(bytes, 0, start), CharCountOfUTF8ByteRange(bytes, start, length));
            }
        }

        public uint GetTermIndex(uint matchNumber)
        {
            if(matchNumber >= MatchCount) {
                throw new ArgumentOutOfRangeException(nameof(matchNumber), matchNumber, "Must be less than MatchCount");
            }

            return _matches[matchNumber].termIndex;
        }

        private uint CharCountOfUTF8ByteRange(byte* bytes, uint start, uint length)
        {
            if(length == 0) {
                return 0;
            }

            return (uint)Encoding.UTF8.GetCharCount(bytes + start,(int) length);
        }
    }

    public struct Range
    {
        public uint Start { get; }

        public uint Length { get; }

        public Range(uint start, uint length)
        {
            Start = start;
            Length = length;
        }
    }
}
