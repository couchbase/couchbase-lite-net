//
// RevisionInternal.cs
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
using System.Collections.Generic;
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;
using System.Text;
using System.Linq;

#if !NET_3_5
using StringEx = System.String;
#endif

namespace Couchbase.Lite.Internal
{
    /// <summary>Stores information about a revision -- its docID, revID, and whether it's deleted.
    ///     </summary>
    /// <remarks>
    /// Stores information about a revision -- its docID, revID, and whether it's deleted.
    /// It can also store the sequence number and document contents (they can be added after creation).
    /// </remarks>
    internal class RevisionInternal
    {

        #region Variables

        private readonly string _docId;
        private readonly string _revId;
        private bool _deleted;
        private bool _missing;
        private Body _body;
        private long _sequence;

        #endregion

        #region Constructors

        internal RevisionInternal(String docId, String revId, Boolean deleted)
        {
            // TODO: get rid of this field!
            this._docId = docId;
            this._revId = revId;
            this._deleted = deleted;
        }

        internal RevisionInternal(Body body)
            : this(body.GetPropertyForKey<string>("_id"), body.GetPropertyForKey<string>("_rev"), body.GetPropertyForKey<bool>("_deleted"))
        {
            this._body = body;
        }

        internal RevisionInternal(IDictionary<String, Object> properties)
            : this(new Body(properties)) { }

        #endregion

        #region Methods

        public static bool IsValid(Body body)
        {
            return body.GetPropertyForKey("_id") != null ||
            (body.GetPropertyForKey("_rev") == null && body.GetPropertyForKey("_deleted") == null);
        }

        internal static Tuple<int, string> ParseRevId(string revId)
        {
            if (revId == null || revId.Contains(" ")) {
                return Tuple.Create(-1, string.Empty); 
            }

            int dashPos = revId.IndexOf("-", StringComparison.InvariantCulture);
            if (dashPos == -1) {
                return Tuple.Create(-1, string.Empty);
            }

            var genStr = revId.Substring(0, dashPos);
            int generation;
            if (!int.TryParse(genStr, out generation)) {
                return Tuple.Create(-1, string.Empty);
            }

            var suffix = revId.Substring(dashPos + 1);
            if (suffix.Length == 0) {
                return Tuple.Create(-1, string.Empty);
            }

            return Tuple.Create(generation, suffix);
        }

        internal IDictionary<String, Object> GetProperties()
        {
            IDictionary<string, object> result = null;
            if (_body != null) {
                IDictionary<string, object> prop;
                try {
                    prop = _body.GetProperties();
                } catch (InvalidOperationException) {
                    // handle when both object and json are null for this body
                    return null;
                }

                if (result == null) {
                    result = new Dictionary<string, object>();
                }
                result.PutAll(prop);

                if (_docId != null) {
                    result["_id"] = _docId;
                }

                if (_revId != null) {
                    result["_rev"] = _revId;
                }

                if (_deleted) {
                    result["_deleted"] = true;
                }
            }
            return result;
        }

        internal RevisionInternal CopyWithoutBody()
        {
            if (_body == null) {
                return this;
            }

            var rev = new RevisionInternal(_docId, _revId, _deleted);
            rev.SetSequence(_sequence);
            rev.SetMissing(_missing);
            return rev;
        }

        internal object GetPropertyForKey(string key)
        {
            if (key == "_id") {
                return _docId;
            }

            if (key == "_rev") {
                return _revId;
            }

            if (key == "_deleted") {
                return _deleted ? (object)true : null;
            }

            var prop = GetProperties();
            if (prop == null)
            {
                return null;
            }
            return GetProperties().Get(key);
        }

        internal void SetProperties(IDictionary<string, object> properties)
        {
            _body = new Body(properties);
        }

        internal IEnumerable<Byte> GetJson()
        {
            IEnumerable<Byte> result = null;
            if (_body != null)
            {
                result = _body.AsJson();
            }
            return result;
        }

        internal void SetJson(IEnumerable<Byte> json)
        {
            _body = new Body(json);
        }

        internal IDictionary<string, object> GetAttachments()
        {
            var props = GetProperties();
            if (props == null) {
                return null;
            }

            return props.Get("_attachments").AsDictionary<string, object>();
        }

        internal string GetDocId()
        {
            return _docId;
        }

        internal string GetRevId()
        {
            return _revId;
        }

        internal bool IsDeleted()
        {
            return _deleted;
        }

        internal void SetDeleted(bool deleted)
        {
            this._deleted = deleted;
        }

        internal Body GetBody()
        {
            if (_body == null) {
                return _body;
            }

            var props = _body.GetProperties();
            if (_docId != null) {
                props["_id"] = _docId;
            }

            if (_revId != null) {
                props["_rev"] = _revId;
            }

            if (_deleted) {
                props["_deleted"] = true;
            }

            return new Body(props);
        }

        internal void SetBody(Body body)
        {
            this._body = body;
        }

        internal Boolean IsMissing()
        {
            return _missing;
        }

        internal void SetMissing(Boolean isMissing)
        {
            _missing = isMissing;
        }

        internal RevisionInternal CopyWithDocID(String docId, String revId)
        {
            System.Diagnostics.Debug.Assert((docId != null));
            System.Diagnostics.Debug.Assert(((this._docId == null) || (this._docId.Equals(docId))));

            var result = new RevisionInternal(docId, revId, _deleted);
            var unmodifiableProperties = GetProperties();
            var properties = new Dictionary<string, object>();
            if (unmodifiableProperties != null)
            {
                properties.PutAll(unmodifiableProperties);
            }
            properties["_id"] = docId;
            properties["_rev"] = revId;
            result.SetProperties(properties);
            return result;
        }

        internal void SetSequence(long sequence)
        {
            this._sequence = sequence;
        }

        internal long GetSequence()
        {
            return _sequence;
        }

        public override string ToString()
        {
            return "{" + this._docId + " #" + this._revId + (_deleted ? "DEL" : string.Empty) + "}";
        }

        /// <summary>Generation number: 1 for a new document, 2 for the 2nd revision, ...</summary>
        /// <remarks>
        /// Generation number: 1 for a new document, 2 for the 2nd revision, ...
        /// Extracted from the numeric prefix of the revID.
        /// </remarks>
        internal int GetGeneration()
        {
            return GenerationFromRevID(_revId);
        }

        internal static int GenerationFromRevID(string revID)
        {
            if (revID == null) {
                return 0;
            }

            var generation = 0;
            var dashPos = revID.IndexOf("-", StringComparison.InvariantCultureIgnoreCase);
            if (dashPos > 0)
            {
                generation = Convert.ToInt32(revID.Substring(0, dashPos));
            }
            return generation;
        }

        internal static int CBLCollateRevIDs(string revId1, string revId2)
        {
            string rev1GenerationStr = null;
            string rev2GenerationStr = null;
            string rev1Hash = null;
            string rev2Hash = null;
            var st1 = new StringTokenizer(revId1, "-");
            try
            {
                rev1GenerationStr = st1.NextToken();
                rev1Hash = st1.NextToken();
            }
            catch (Exception)
            {
            }
            StringTokenizer st2 = new StringTokenizer(revId2, "-");
            try
            {
                rev2GenerationStr = st2.NextToken();
                rev2Hash = st2.NextToken();
            }
            catch (Exception)
            {
            }
            // improper rev IDs; just compare as plain text:
            if (rev1GenerationStr == null || rev2GenerationStr == null)
            {
                return revId1.CompareToIgnoreCase(revId2);
            }
            int rev1Generation;
            int rev2Generation;
            try
            {
                rev1Generation = System.Convert.ToInt32(rev1GenerationStr);
                rev2Generation = System.Convert.ToInt32(rev2GenerationStr);
            }
            catch (FormatException)
            {
                // improper rev IDs; just compare as plain text:
                return revId1.CompareToIgnoreCase(revId2);
            }
            // Compare generation numbers; if they match, compare suffixes:
            if (rev1Generation.CompareTo(rev2Generation) != 0)
            {
                return rev1Generation.CompareTo(rev2Generation);
            }
            else
            {
                if (rev1Hash != null && rev2Hash != null)
                {
                    // compare suffixes if possible
                    return Sharpen.Runtime.CompareOrdinal(rev1Hash, rev2Hash);
                }
                else
                {
                    // just compare as plain text:
                    return revId1.CompareToIgnoreCase(revId2);
                }
            }
        }

        internal static int CBLCompareRevIDs(string revId1, string revId2)
        {
            System.Diagnostics.Debug.Assert((revId1 != null));
            System.Diagnostics.Debug.Assert((revId2 != null));
            return CBLCollateRevIDs(revId1, revId2);
        }

        // Calls the block on every attachment dictionary. The block can return a different dictionary,
        // which will be replaced in the rev's properties. If it returns nil, the operation aborts.
        // Returns YES if any changes were made.
        internal bool MutateAttachments(Func<string, IDictionary<string, object>, IDictionary<string, object>> mutator)
        {
            var properties = GetProperties();
            IDictionary<string, object> editedProperties = null;

            IDictionary<string, object> attachments = null;
            if (properties.ContainsKey("_attachments"))
            {
                attachments = properties["_attachments"].AsDictionary<string, object>();
            }

            IDictionary<string, object> editedAttachments = null;

            if (attachments != null)
            {
                foreach(var kvp in attachments)
                {
                    var attachment = new Dictionary<string, object>(kvp.Value.AsDictionary<string, object>());
                    var editedAttachment = mutator(kvp.Key, attachment);
                    if (editedAttachment == null)
                    {
                        return false;
                    }

                    if (editedAttachment != attachment)
                    {
                        if (editedProperties == null)
                        {
                            // Make the document properties and _attachments dictionary mutable:
                            editedProperties = new Dictionary<string, object>(properties);
                            editedAttachments = new Dictionary<string, object>(attachments);
                            editedProperties["_attachments"] = editedAttachments;
                        }
                        editedAttachments[kvp.Key] = editedAttachment;
                    }
                }
            }

            if (editedProperties != null)
            {
                SetProperties(editedProperties);
                return true;
            }

            return false;
        }

        #endregion

        #region Overrides

        public override bool Equals(object o)
        {
            var other = o as RevisionInternal;
            bool result = false;
            if (other != null) {
                if (_docId.Equals(other._docId) && _revId.Equals(other._revId)) {
                    result = true;
                }
            }
            return result;
        }

        public override int GetHashCode()
        {
            return _docId.GetHashCode() ^ _revId.GetHashCode();
        }

        #endregion

    }
}
