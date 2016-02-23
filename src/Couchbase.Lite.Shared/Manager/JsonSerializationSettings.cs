//
// JsonSerializationSettings.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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

namespace Couchbase.Lite
{

    /// <summary>
    /// Specifies methods for deserializing date times that are written as strings
    /// </summary>
    public enum DateTimeHandling
    {
        /// <summary>
        /// Deserialize to System.DateTime (local time zone)
        /// </summary>
        UseDateTime,

        /// <summary>
        /// Deserialize to System.DateTimeOffset (embedded time zone)
        /// </summary>
        UseDateTimeOffset
    }

    /// <summary>
    /// A struct containing options for JSON serialization and deserialization
    /// </summary>
    public struct JsonSerializationSettings
    {

        /// <summary>
        /// Gets or sets how the serializer will handle deserializing date times.
        /// </summary>
        public DateTimeHandling DateTimeHandling { get; set; }

        #region Operators
        #pragma warning disable 1591

        public static bool operator ==(JsonSerializationSettings x, JsonSerializationSettings y) 
        {
            return x.Equals(y);
        }

        public static bool operator !=(JsonSerializationSettings x, JsonSerializationSettings y) 
        {
            return !x.Equals(y);
        }

        #endregion

        #region Overrides

        public override bool Equals(object obj)
        {
            if (!(obj is JsonSerializationSettings)) {
                return false;
            }

            return DateTimeHandling == ((JsonSerializationSettings)obj).DateTimeHandling;
        }

        public override int GetHashCode()
        {
            return DateTimeHandling.GetHashCode();
        }

        #pragma warning restore 1591
        #endregion
    }
}

