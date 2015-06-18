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
        private string docId;

        private string revId;

        private bool deleted;

        private bool missing;

        private Body body;

        private long sequence;

        //private Database database;

        internal RevisionInternal(String docId, String revId, Boolean deleted)
        {
            // TODO: get rid of this field!
            this.docId = docId;
            this.revId = revId;
            this.deleted = deleted;
        }

        internal RevisionInternal(Body body)
            : this(body.GetPropertyForKey<string>("_id"), body.GetPropertyForKey<string>("_rev"), body.GetPropertyForKey<bool>("_deleted"))
        {
            this.body = body;
        }

        internal RevisionInternal(IDictionary<String, Object> properties)
            : this(new Body(properties)) { }

        public static bool IsValid(Body body)
        {
            return body.GetPropertyForKey("_id") != null ||
            (body.GetPropertyForKey("_rev") == null && body.GetPropertyForKey("_deleted") == null);
        }

        public static bool ParseRevId(string revId, out int generation, out string suffix)
        {
            generation = 0;
            suffix = null;
            var components = revId.Split('-');
            if (components.Length != 2) {
                return false;
            }

            if (!int.TryParse(components[0], out generation)) {
                return false;
            }

            suffix = components[1];
            return true;
        }

        internal IDictionary<String, Object> GetProperties()
        {
            IDictionary<string, object> result = null;
            if (body != null) {
                IDictionary<string, object> prop;
                try {
                    prop = body.GetProperties();
                } catch (InvalidOperationException) {
                    // handle when both object and json are null for this body
                    return null;
                }

                if (result == null) {
                    result = new Dictionary<string, object>();
                }
                result.PutAll(prop);

                if (docId != null) {
                    result["_id"] = docId;
                }

                if (revId != null) {
                    result["_rev"] = revId;
                }

                if (deleted) {
                    result["_deleted"] = true;
                }
            }
            return result;
        }

        internal RevisionInternal CopyWithoutBody()
        {
            if (body == null) {
                return this;
            }

            var rev = new RevisionInternal(docId, revId, deleted);
            rev.SetSequence(sequence);
            rev.SetMissing(missing);
            return rev;
        }

        internal object GetPropertyForKey(string key)
        {
            if (key == "_id") {
                return docId;
            }

            if (key == "_rev") {
                return revId;
            }

            if (key == "_deleted") {
                return deleted ? (object)true : null;
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
            body = new Body(properties);
        }

        internal IEnumerable<Byte> GetJson()
        {
            IEnumerable<Byte> result = null;
            if (body != null)
            {
                result = body.AsJson();
            }
            return result;
        }

        internal void SetJson(IEnumerable<Byte> json)
        {
            body = new Body(json);
        }

        public override bool Equals(object o)
        {
            var result = false;
            if (o is RevisionInternal)
            {
                RevisionInternal other = (RevisionInternal)o;
                if (docId.Equals(other.docId) && revId.Equals(other.revId))
                {
                    result = true;
                }
            }
            return result;
        }

        public override int GetHashCode()
        {
            return docId.GetHashCode() ^ revId.GetHashCode();
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
            return docId;
        }

        internal void SetDocId(string docId)
        {
            this.docId = docId;
        }

        internal string GetRevId()
        {
            return revId;
        }

        internal void SetRevId(string revId)
        {
            this.revId = revId;
        }

        internal bool IsDeleted()
        {
            return deleted;
        }

        internal void SetDeleted(bool deleted)
        {
            this.deleted = deleted;
        }

        internal Body GetBody()
        {
            if (body == null) {
                return body;
            }

            var props = body.GetProperties();
            if (docId != null) {
                props["_id"] = docId;
            }

            if (revId != null) {
                props["_rev"] = revId;
            }

            if (deleted) {
                props["_deleted"] = true;
            }

            return new Body(props);
        }

        internal void SetBody(Body body)
        {
            this.body = body;
        }

        internal Boolean IsMissing()
        {
            return missing;
        }

        internal void SetMissing(Boolean isMissing)
        {
            missing = isMissing;
        }

        internal RevisionInternal CopyWithDocID(String docId, String revId)
        {
            System.Diagnostics.Debug.Assert((docId != null));
            System.Diagnostics.Debug.Assert(((this.docId == null) || (this.docId.Equals(docId))));

            var result = new RevisionInternal(docId, revId, deleted);
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
            this.sequence = sequence;
        }

        internal long GetSequence()
        {
            return sequence;
        }

        public override string ToString()
        {
            return "{" + this.docId + " #" + this.revId + (deleted ? "DEL" : string.Empty) + "}";
        }

        /// <summary>Generation number: 1 for a new document, 2 for the 2nd revision, ...</summary>
        /// <remarks>
        /// Generation number: 1 for a new document, 2 for the 2nd revision, ...
        /// Extracted from the numeric prefix of the revID.
        /// </remarks>
        internal int GetGeneration()
        {
            return GenerationFromRevID(revId);
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

    }
}
