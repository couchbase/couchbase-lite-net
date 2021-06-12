// 
// DocContext.cs
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

using Couchbase.Lite.Internal.Serialization;

using LiteCore.Interop;

namespace Couchbase.Lite.Internal.Doc
{
    internal unsafe class DocContext : MContext
    {
        #region Properties

        public Database Db { get; }

        public C4DocumentWrapper Doc { get; }

        #endregion

        #region Constructors

        public DocContext(Database db, C4DocumentWrapper doc)
            : base(new FLSlice())
        {
            Db = db;
            Doc = doc?.Retain<C4DocumentWrapper>();
        }

        #endregion

        #region Public Methods

        public object ToObject(FLValue* value, bool dotNetType)
        {
            return FLValueConverter.ToCouchbaseObject(value, Db, dotNetType);
        }

        #endregion

        #region Overrides

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing) {
                Doc?.Dispose();
            }
        }

        #endregion
    }
}