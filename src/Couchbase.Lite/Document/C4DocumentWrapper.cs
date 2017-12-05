// 
//  C4DocumentWrapper.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
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
using Couchbase.Lite.Util;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed unsafe class C4DocumentWrapper : RefCountedDisposable
    {
        #region Constants

        private const string Tag = nameof(C4DocumentWrapper);

        #endregion

        #region Variables

        public readonly C4Document* RawDoc;

        #endregion

        #region Properties

        public bool HasValue => RawDoc != null;

        #endregion

        #region Constructors

        public C4DocumentWrapper(C4Document* doc)
        {
            RawDoc = doc;
            if (RawDoc == null) {
                GC.SuppressFinalize(this);
            }
        }

        #endregion

        #region Overrides

        protected override void Dispose(bool disposing)
        {
            Native.c4doc_free(RawDoc);
        }

        public override string ToString()
        {
            return RawDoc == null ? "<empty>" : $"C4Document -> {RawDoc->docID.CreateString()}";
        }

        #endregion
    }
}