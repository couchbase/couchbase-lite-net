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
using Couchbase.Lite.Revisions;
using System.Text;
using Couchbase.Lite.Util;

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

        protected readonly string _docId;
        protected readonly RevisionID _revId;
        protected Body _body;

        #endregion

        #region Properties

        public string DocID
        {
            get { return _docId; }
        }

        public RevisionID RevID
        {
            get { return _revId; }
        }

        public long Sequence { get; internal set; }

        public bool Deleted { get; internal set; }

        public int Generation
        {
            get { return _revId == null ? 0 : _revId.Generation; }
        }

        public bool Missing { get; internal set; }

        #endregion

        #region Constructors

        internal RevisionInternal(RevisionInternal other) : this(other.DocID, other.RevID, other.Deleted)
        {
            var unmodifiableProperties = other.GetProperties();
            var properties = new Dictionary<string, object>();
            if(unmodifiableProperties != null) {
                properties.PutAll(unmodifiableProperties);
            }

            SetProperties(properties);
        }

        internal RevisionInternal(string docId, RevisionID revId, bool deleted)
        {
            // TODO: get rid of this field!
            _docId = docId;
            _revId = revId;
            Deleted = deleted;
        }

        internal RevisionInternal(string docId, RevisionID revId, bool deleted, Body body)
            : this(docId, revId, deleted)
        {
            _body = body;
        }

        internal RevisionInternal(Body body)
            : this(body.GetProperties().CblID(), body.GetProperties().CblRev(), body.GetProperties().CblDeleted())
        {
            _body = body;
        }

        internal RevisionInternal(IDictionary<String, Object> properties)
            : this(new Body(properties)) { }


        #endregion

        #region Methods

        // Used by listener
        public static bool IsValid(Body body)
        {
            return body.GetPropertyForKey("_id") != null ||
            (body.GetPropertyForKey("_rev") == null && body.GetPropertyForKey("_deleted") == null);
        }

        public IDictionary<string, object> GetProperties()
        {
            IDictionary<string, object> result = null;
            if(_body != null) {
                try {
                    result = _body.GetProperties();
                } catch(InvalidOperationException) {
                    // handle when both object and json are null for this body
                    return null;
                }

                if(_docId != null) {
                    result["_id"] = _docId;
                }

                if(_revId != null) {
                    result.SetRevID(_revId);
                }

                if(Deleted) {
                    result["_deleted"] = true;
                }
            }
            return result;
        }

        public RevisionInternal CopyWithoutBody()
        {
            if(_body == null) {
                return this;
            }

            var rev = new RevisionInternal(_docId, _revId, Deleted);
            rev.Sequence = Sequence;
            rev.Missing = Missing;
            return rev;
        }

        public object GetPropertyForKey(string key)
        {
            if(key == "_id") {
                return _docId;
            }

            if(key == "_rev") {
                return _revId;
            }

            if(key == "_deleted") {
                return Deleted ? (object)true : null;
            }

            return _body?.GetPropertyForKey(key);
        }

        internal RevisionInternal AddBasicMetadata()
        {
            var props = GetProperties();
            if(props == null) {
                return null;
            }

            props.SetDocRevID(DocID, RevID);
            if(Deleted) {
                props["_deleted"] = true;
            }

            var result = new RevisionInternal(props);
            result.Sequence = Sequence;
            return result;
        }

        internal void SetProperties(IDictionary<string, object> properties)
        {
            _body = new Body(properties);
        }

        // Used by plugins
        internal void SetPropertyForKey(string key, object value)
        {
            _body.SetPropertyForKey(key, value);
        }

        // Unused, but here for balance
        internal IEnumerable<byte> GetJson()
        {
            IEnumerable<byte> result = null;
            if(_body != null) {
                result = _body.AsJson();
            }

            return result;
        }

        // Used by plugins
        internal void SetJson(IEnumerable<byte> json)
        {
            if(json != null) {
                _body = new Body(json, DocID, RevID, Deleted);
                Missing = false;
            } else {
                _body = null;
                Missing = true;
            }
        }

        public IDictionary<string, object> GetAttachments()
        {
            var props = GetProperties();
            if(props == null) {
                return null;
            }

            return props.Get("_attachments").AsDictionary<string, object>();
        }

        // Used by listener and plugins
        public Body GetBody()
        {
            if(_body == null) {
                return _body;
            }

            var props = _body.GetProperties();
            if(_docId != null) {
                props["_id"] = _docId;
            }

            if(_revId != null) {
                props.SetRevID(_revId);
            }

            if(Deleted) {
                props["_deleted"] = true;
            }

            return new Body(props);
        }

        internal void SetBody(Body body)
        {
            _body = body;
        }

        public RevisionInternal Copy(string docId, RevisionID revId)
        {
            System.Diagnostics.Debug.Assert((docId != null));
            System.Diagnostics.Debug.Assert(((_docId == null) || (_docId.Equals(docId))));

            var result = new RevisionInternal(docId, revId, Deleted);
            var unmodifiableProperties = GetProperties();
            var properties = new Dictionary<string, object>();
            if(unmodifiableProperties != null) {
                properties.PutAll(unmodifiableProperties);
            }

            properties.SetDocRevID(docId, revId);
            result.SetProperties(properties);
            return result;
        }

        // Calls the block on every attachment dictionary. The block can return a different dictionary,
        // which will be replaced in the rev's properties. If it returns nil, the operation aborts.
        // Returns YES if any changes were made.
        public bool MutateAttachments(Func<string, IDictionary<string, object>, IDictionary<string, object>> mutator)
        {
            var properties = GetProperties();
            IDictionary<string, object> editedProperties = null;

            IDictionary<string, object> attachments = null;
            if(properties.ContainsKey("_attachments")) {
                attachments = properties["_attachments"].AsDictionary<string, object>();
            }

            IDictionary<string, object> editedAttachments = null;

            if(attachments != null) {
                foreach(var kvp in attachments) {
                    var attachment = new Dictionary<string, object>(kvp.Value.AsDictionary<string, object>());
                    var editedAttachment = mutator(kvp.Key, attachment);
                    if(editedAttachment == null) {
                        return false;
                    }

                    if(editedAttachment != attachment) {
                        if(editedProperties == null) {
                            // Make the document properties and _attachments dictionary mutable:
                            editedProperties = new Dictionary<string, object>(properties);
                            editedAttachments = new Dictionary<string, object>(attachments);
                            editedProperties["_attachments"] = editedAttachments;
                        }
                        editedAttachments[kvp.Key] = editedAttachment;
                    }
                }
            }

            if(editedProperties != null) {
                SetProperties(editedProperties);
                return true;
            }

            return false;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return String.Format("{{{0} #{1}{2}}}",
                new SecureLogString(_docId, LogMessageSensitivity.PotentiallyInsecure), _revId, Deleted ? "DEL" : String.Empty);
        }

        public override bool Equals(object o)
        {
            var other = o as RevisionInternal;
            bool result = false;
            if(other != null) {
                if(_docId.Equals(other._docId) && _revId.Equals(other._revId)) {
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
