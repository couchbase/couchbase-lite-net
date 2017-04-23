// 
// ArrayObject.cs
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
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Couchbase.Lite.Internal.Doc
{
    internal sealed class ArrayObject : ReadOnlyArray, IArray
    {
        #region Variables

        internal event EventHandler<ObjectChangedEventArgs<ArrayObject>> Changed;
        private bool _changed;
        private IList _list;

        #endregion

        #region Properties

        public override int Count => _list.Count;

        public new IFragment this[int index]
        {
            get {
                var value = index >= 0 && index < Count ? GetObject(index) : null;
                return new Fragment(value, this, index);
            }
        }

        #endregion

        #region Constructors

        public ArrayObject()
            : this(new FleeceArray())
        {
            
        }

        public ArrayObject(IList array)
            : this()
        {
            Set(array);
        }

        public ArrayObject(IReadOnlyArray data)
            : base(data)
        {
            _list = new List<object>();
            LoadBackingData();
        }

        #endregion

        #region Private Methods

        private void LoadBackingData()
        {
            var count = base.Count;
            for (int i = 0; i < count; i++) {
                var value = base.GetObject(i);
                _list.Add(PrepareValue(value));
            }
        }

        private void ObjectChanged(object sender, ObjectChangedEventArgs<DictionaryObject> args)
        {
            SetChanged();
        }

        private void ObjectChanged(object sender, ObjectChangedEventArgs<ArrayObject> args)
        {
            SetChanged();
        }

        private object PrepareValue(object value)
        {
            Doc.Data.ValidateValue(value);
            return Doc.Data.ConvertValue(value, ObjectChanged, ObjectChanged);
        }

        private void RemoveAllChangedListeners()
        {
            foreach (var obj in _list) {
                RemoveChangedListener(obj);
            }
        }

        private void RemoveChangedListener(object value)
        {
            switch (value) {
                case Subdocument subdoc:
                    subdoc.Dictionary.Changed -= ObjectChanged;
                    break;
                case ArrayObject array:
                    array.Changed -= ObjectChanged;
                    break;
            }
        }

        private void SetChanged()
        {
            if (!_changed) {
                _changed = true;
                Changed?.Invoke(this, new ObjectChangedEventArgs<ArrayObject>(this));
            }
        }

        private void SetValue(int index, object value, bool isChange)
        {
            _list[index] = value;
            if (isChange) {
                SetChanged();
            }
        }

        #endregion

        #region Overrides

        public override IBlob GetBlob(int index)
        {
            return _list[index] as IBlob;
        }

        public override bool GetBoolean(int index)
        {
            var value = _list[index];
            if (value == null) {
                return false;
            }

            try {
                return Convert.ToBoolean(value);
            } catch (InvalidCastException) {
                return false;
            }
        }

        public override DateTimeOffset GetDate(int index)
        {
            var value = _list[index] as string;
            if (value == null) {
                return DateTimeOffset.MinValue;
            }

            return DateTimeOffset.ParseExact(value, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);
        }

        public override double GetDouble(int index)
        {
            var value = _list[index];
            if (value == null) {
                return 0.0;
            }

            try {
                return Convert.ToDouble(value);
            } catch (InvalidCastException) {
                return 0.0;
            }
        }

        public override int GetInt(int index)
        {
            var value = _list[index];
            if (value == null) {
                return 0;
            }

            try {
                return Convert.ToInt32(value);
            } catch (InvalidCastException) {
                return 0;
            }
        }

        public override long GetLong(int index)
        {
            var value = _list[index];
            if (value == null) {
                return 0L;
            }

            try {
                return Convert.ToInt64(value);
            } catch (InvalidCastException) {
                return 0L;
            }
        }

        public override object GetObject(int index)
        {
            return _list[index];
        }

        public override string GetString(int index)
        {
            return _list[index] as string;
        }

        public override IList<object> ToArray()
        {
            var array = new List<object>();
            foreach (var item in _list) {
                switch (item) {
                    case IReadOnlyDictionary dict:
                        array.Add(dict.ToDictionary());
                        break;
                    case IReadOnlyArray arr:
                        array.Add(arr.ToArray());
                        break;
                    default:
                        array.Add(item);
                        break;
                }
            }

            return array;
        }

        #endregion

        #region IArray

        public IArray Add(object value)
        {
            _list.Add(PrepareValue(value));
            SetChanged();
            return this;
        }

        public new IArray GetArray(int index)
        {
            return _list[index] as IArray;
        }

        public new ISubdocument GetSubdocument(int index)
        {
            return _list[index] as ISubdocument;
        }

        public IArray Insert(int index, object value)
        {
            _list.Insert(index, PrepareValue(value));
            SetChanged();
            return this;
        }

        public IArray RemoveAt(int index)
        {
            var value = _list[index];
            RemoveChangedListener(value);
            _list.RemoveAt(index);
            SetChanged();
            return this;
        }

        public IArray Set(IList array)
        {
            RemoveAllChangedListeners();

            var result = new List<object>();
            foreach (var item in array) {
                result.Add(PrepareValue(item));
            }

            _list = result;
            SetChanged();
            return this;
        }

        public IArray Set(int index, object value)
        {
            var oldValue = _list[index];
            if (value?.Equals(oldValue) == false) {
                value = PrepareValue(value);
                RemoveChangedListener(oldValue);
                SetValue(index, value, true);
            }

            return this;
        }

        #endregion
    }
}
