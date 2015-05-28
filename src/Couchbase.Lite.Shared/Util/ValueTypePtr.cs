//
//  OutVal.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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

namespace Couchbase.Lite.Util
{

    /// <summary>
    /// A class for storing out variables (allows the passage of null for 
    /// an unneeded out param)
    /// </summary>
    public sealed class ValueTypePtr<T> where T : struct
    {
        #region Variables

        public T Value { get; set; }

        public bool IsNull {
            get {
                return this == NULL;
            }
        }

        /// <summary>
        /// A value to pass when the out parameter is not needed
        /// </summary>
        public static readonly ValueTypePtr<T> NULL = new ValueTypePtr<T>();

        #endregion

        #region Operators

        public static implicit operator T(ValueTypePtr<T> val) 
        {
            Nullable<bool> t;
            return val.Value;
        }

        public static implicit operator ValueTypePtr<T>(T val)
        {
            return new ValueTypePtr<T> { Value = val };
        }

        #endregion

    }
}

