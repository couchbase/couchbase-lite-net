// 
// ReadOnlyDocument.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 
using Couchbase.Lite.Logging;
using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal unsafe class ReadOnlyDocument : ReadOnlyDictionary, IReadOnlyDocument
    {
        #region Properties

        public string Id { get; }

        public bool IsDeleted => _threadSafety.DoLocked(() => c4Doc != null && c4Doc->flags.HasFlag(C4DocumentFlags.Deleted));

        public ulong Sequence => _threadSafety.DoLocked(() => c4Doc != null ? c4Doc->sequence : 0UL);

        internal C4Document* c4Doc { get; set; }

        internal bool Exists => _threadSafety.DoLocked(() => c4Doc != null && c4Doc->flags.HasFlag(C4DocumentFlags.Exists));

        internal uint Generation => _threadSafety.DoLocked(() => c4Doc != null ? NativeRaw.c4rev_getGeneration(c4Doc->revID) : 0U);

        #endregion

        #region Constructors

        public ReadOnlyDocument(string documentID, C4Document* c4Doc, IReadOnlyDictionary data)
            : base(data)
        {
            Id = documentID;
            this.c4Doc = c4Doc;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            var id = new SecureLogString(Id, LogMessageSensitivity.PotentiallyInsecure);
            return $"{GetType().Name}[{id}]";
        }

        #endregion
    }
}
