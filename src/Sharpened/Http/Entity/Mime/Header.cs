//
// Header.cs
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

using System.Collections.Generic;
using System.Globalization;
using Apache.Http.Entity.Mime;
using Sharpen;

namespace Apache.Http.Entity.Mime
{
    /// <summary>The header of an entity (see RFC 2045).</summary>
    /// <remarks>The header of an entity (see RFC 2045).</remarks>
    public class Header : IEnumerable<MinimalField>
    {
        private readonly IList<MinimalField> fields;

        private readonly IDictionary<string, IList<MinimalField>> fieldMap;

        public Header() : base()
        {
            this.fields = new List<MinimalField>();
            this.fieldMap = new Dictionary<string, IList<MinimalField>>();
        }

        public virtual void AddField(MinimalField field)
        {
            if (field == null)
            {
                return;
            }
            string key = field.GetName().ToLower(CultureInfo.InvariantCulture);
            IList<MinimalField> values = this.fieldMap.Get(key);
            if (values == null)
            {
                values = new List<MinimalField>();
                this.fieldMap.Put(key, values);
            }
            values.AddItem(field);
            this.fields.AddItem(field);
        }

        public virtual IList<MinimalField> GetFields()
        {
            return new AList<MinimalField>(this.fields);
        }

        public virtual MinimalField GetField(string name)
        {
            if (name == null)
            {
                return null;
            }
            string key = name.ToLower(CultureInfo.InvariantCulture);
            IList<MinimalField> list = this.fieldMap.Get(key);
            if (list != null && !list.IsEmpty())
            {
                return list[0];
            }
            return null;
        }

        public virtual IList<MinimalField> GetFields(string name)
        {
            if (name == null)
            {
                return null;
            }
            string key = name.ToLower(CultureInfo.InvariantCulture);
            IList<MinimalField> list = this.fieldMap.Get(key);
            if (list == null || list.IsEmpty())
            {
                return Sharpen.Collections.EmptyList();
            }
            else
            {
                return new AList<MinimalField>(list);
            }
        }

        public virtual int RemoveFields(string name)
        {
            if (name == null)
            {
                return 0;
            }
            string key = name.ToLower(CultureInfo.InvariantCulture);
            IList<MinimalField> removed = Sharpen.Collections.Remove(fieldMap, key);
            if (removed == null || removed.IsEmpty())
            {
                return 0;
            }
            this.fields.RemoveAll(removed);
            return removed.Count;
        }

        public virtual void SetField(MinimalField field)
        {
            if (field == null)
            {
                return;
            }
            string key = field.GetName().ToLower(CultureInfo.InvariantCulture);
            IList<MinimalField> list = fieldMap.Get(key);
            if (list == null || list.IsEmpty())
            {
                AddField(field);
                return;
            }
            list.Clear();
            list.AddItem(field);
            int firstOccurrence = -1;
            int index = 0;
            for (IEnumerator<MinimalField> it = this.fields.GetEnumerator(); it.HasNext(); index
                ++)
            {
                MinimalField f = it.Next();
                if (Sharpen.Runtime.EqualsIgnoreCase(f.GetName(), field.GetName()))
                {
                    it.Remove();
                    if (firstOccurrence == -1)
                    {
                        firstOccurrence = index;
                    }
                }
            }
            this.fields.Add(firstOccurrence, field);
        }

        public override IEnumerator<MinimalField> GetEnumerator()
        {
            return Sharpen.Collections.UnmodifiableList(fields).GetEnumerator();
        }

        public override string ToString()
        {
            return this.fields.ToString();
        }
    }
}
