//
// ValidationContext.cs
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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using Couchbase.Lite.Util;
using Couchbase.Lite.Storage;
using Couchbase.Lite.Internal;
using Couchbase.Lite;

namespace Couchbase.Lite
{

    public class ValidationContext : IValidationContext
    {
        IList<String> changedKeys;

        private RevisionInternal InternalRevision { get; set; }
        private RevisionInternal NewRevision { get; set; }

        private Database Database { get; set; }

        internal String RejectMessage { get; set; }

        internal ValidationContext(Database database, RevisionInternal currentRevision, RevisionInternal newRevision)
        {
            Database = database;
            InternalRevision = currentRevision;
            NewRevision = newRevision;
        }

        #region IValidationContext implementation

        public void Reject ()
        {
            if (RejectMessage == null)
            {
                Reject("invalid document");
            }
        }

        public void Reject (String message)
        {
            if (RejectMessage == null)
            {
                RejectMessage = message;
            }
        }

        public bool ValidateChanges (ValidateChangeDelegate changeValidator)
        {
            var cur = CurrentRevision.Properties;
            var nuu = NewRevision.GetProperties();

            foreach (var key in ChangedKeys)
            {
                if (!changeValidator(key, cur.Get(key), nuu.Get(key)))
                {
                    Reject(String.Format("Illegal change to '{0}' property", key));
                    return false;
                }
            }
            return true;
        }

        public SavedRevision CurrentRevision {
            get {
                if (InternalRevision != null)
                {
                    try
                    {
                        InternalRevision = Database.LoadRevisionBody(InternalRevision, EnumSet.NoneOf<TDContentOptions>());
                        return new SavedRevision(Database, InternalRevision);
                    }
                    catch (CouchbaseLiteException e)
                    {
                        throw new RuntimeException(e);
                    }
                }
                return null;

            }
        }

        public IEnumerable<String> ChangedKeys {
            get {
                if (changedKeys == null)
                {
                    changedKeys = new AList<String>();
                    var cur = CurrentRevision.Properties;
                    var nuu = NewRevision.GetProperties();

                    foreach (var key in cur.Keys)
                    {
                        if (!cur.Get(key).Equals(nuu.Get(key)) && !key.Equals("_rev"))
                        {
                            changedKeys.AddItem(key);
                        }
                    }

                    foreach (var key in nuu.Keys)
                    {
                        if (cur.Get(key) == null && !key.Equals("_rev") && !key.Equals("_id"))
                        {
                            changedKeys.AddItem(key);
                        }
                    }
                }
                return changedKeys;
            }
        }

        #endregion
    }
}
