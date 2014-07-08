// 
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
//using System.Collections.Generic;
using Couchbase.Lite.Support;
using Sharpen;

namespace Couchbase.Lite.Support
{
	public class WeakValueHashMap<K, V> : AbstractMap<K, V>
	{
		private Dictionary<K, WeakValueHashMap.WeakValue<V>> references;

		private ReferenceQueue<V> referenceQueue;

		public WeakValueHashMap()
		{
			references = new Dictionary<K, WeakValueHashMap.WeakValue<V>>();
			referenceQueue = new ReferenceQueue<V>();
		}

		public WeakValueHashMap(IDictionary<K, V> map) : this()
		{
			foreach (KeyValuePair<K, V> entry in map.EntrySet())
			{
				Put(entry.Key, entry.Value);
			}
		}

		public override V Put(K key, V value)
		{
			PruneDeadReferences();
			WeakValueHashMap.WeakValue<V> valueRef = new WeakValueHashMap.WeakValue<V>(this, 
				key, value, referenceQueue);
			return GetReferenceValue(references.Put(key, valueRef));
		}

		public override V Get(object key)
		{
			PruneDeadReferences();
			return GetReferenceValue(references.Get(key));
		}

		public override V Remove(object key)
		{
			V value = GetReferenceValue(references.Get(key));
			Sharpen.Collections.Remove(references, key);
			return value;
		}

		public override void Clear()
		{
			references.Clear();
		}

		public override bool ContainsKey(object key)
		{
			PruneDeadReferences();
			return references.ContainsKey(key);
		}

		public override bool ContainsValue(object value)
		{
			PruneDeadReferences();
			foreach (KeyValuePair<K, WeakValueHashMap.WeakValue<V>> entry in references.EntrySet
				())
			{
				if (value == GetReferenceValue(entry.Value))
				{
					return true;
				}
			}
			return false;
		}

		public override ICollection<K> Keys
		{
			get
			{
				PruneDeadReferences();
				return references.Keys;
			}
		}

		public override int Count
		{
			get
			{
				PruneDeadReferences();
				return references.Count;
			}
		}

		public override ICollection<KeyValuePair<K, V>> EntrySet()
		{
			PruneDeadReferences();
			ICollection<KeyValuePair<K, V>> entries = new LinkedHashSet<KeyValuePair<K, V>>();
			foreach (KeyValuePair<K, WeakValueHashMap.WeakValue<V>> entry in references.EntrySet
				())
			{
				entries.AddItem(new AbstractMap.SimpleEntry<K, V>(entry.Key, GetReferenceValue(entry
					.Value)));
			}
			return entries;
		}

		public override ICollection<V> Values
		{
			get
			{
				PruneDeadReferences();
				ICollection<V> values = new AList<V>();
				foreach (WeakValueHashMap.WeakValue<V> valueRef in references.Values)
				{
					values.AddItem(GetReferenceValue(valueRef));
				}
				return values;
			}
		}

		private V GetReferenceValue(WeakValueHashMap.WeakValue<V> valueRef)
		{
			return valueRef == null ? null : valueRef.Get();
		}

		private void PruneDeadReferences()
		{
			WeakValueHashMap.WeakValue<V> valueRef;
			while ((valueRef = (WeakValueHashMap.WeakValue<V>)referenceQueue.Poll()) != null)
			{
				Sharpen.Collections.Remove(references, valueRef.GetKey());
			}
		}

		private class WeakValue<T> : WeakReference<T>
		{
			private readonly K key;

			private WeakValue(WeakValueHashMap<K, V> _enclosing, K key, T value, ReferenceQueue
				<T> queue) : base(value, queue)
			{
				this._enclosing = _enclosing;
				this.key = key;
			}

			private K GetKey()
			{
				return this.key;
			}

			private readonly WeakValueHashMap<K, V> _enclosing;
		}
	}
}
