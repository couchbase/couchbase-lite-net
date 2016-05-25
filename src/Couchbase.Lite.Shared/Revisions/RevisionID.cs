//
// RevisionID.cs
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
using Couchbase.Lite.Util;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Couchbase.Lite.Revisions
{
    internal sealed class RevisionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(TreeRevisionID);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var str = reader.Value as string;
            return RevisionIDFactory.FromString(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }

    internal static class RevisionIDFactory
    {
        public static RevisionID FromString(string str)
        {
            if(str == null) {
                return null;
            }

            return FromData(Encoding.UTF8.GetBytes(str));
        }

        public static RevisionID FromData(IEnumerable<byte> data)
        {
            if(data == null) {
                return null;
            }

            return new TreeRevisionID(data);
        }
    }

    [JsonConverter(typeof(RevisionConverter))]
    internal abstract class RevisionID : IComparable<RevisionID>, IEquatable<string>
    {
        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
        private static extern int memcmp(byte[] b1, byte[] b2, UIntPtr count);

        public bool IsValid
        {
            get {
                return Generation > 0 && Suffix != null;
            }
        }

        public virtual int Generation
        {
            get { return 0; }
        }

        public virtual string Suffix
        {
            get { return null; }
        }

        public abstract byte[] AsData();

        internal static int CBLCollateRevIDs(byte[] revID1, byte[] revID2)
        {
            var dash1 = Array.IndexOf(revID1, (byte)'-');
            var dash2 = Array.IndexOf(revID2, (byte)'-');
            if((dash1 == 1 && dash2 == 1) || dash1 > 8 || dash2 > 8 || dash1 == -1 || dash2 == -1) {
                // Single digit generation #s, or improper rev IDs; just compare as plain text
                return DefaultCollate(revID1, revID2);
            }

            // Parse generation numbers.  If either is invalid, revert to default collation:
            var gen1 = ParseDigits(revID1, dash1);
            var gen2 = ParseDigits(revID2, dash2);
            if(gen1 == 0 || gen2 == 0) {
                return DefaultCollate(revID1, revID2);
            }

            // Compare generation numbers; if they match, compare suffixes:
            var retVal = Math.Sign(gen1 - gen2);
            if(retVal != 0) {
                return retVal;
            }

            return DefaultCollate(revID1, revID2, dash1 + 1, dash2 + 1);
        }

        protected static int ParseDigits(byte[] revID, int position)
        {
            var result = 0;
            for(int i = 0; i < position; i++) {
                var current = revID[i];
                if(current < (byte)'0' || current > (byte)'9') {
                    return 0;
                }

                result = 10 * result + (current - '0');
            }

            return result;
        }

        protected static unsafe int DefaultCollate(byte[] revID1, byte[] revID2, int startIndex1 = 0, int startIndex2 = 0)
        {
            var len = Math.Min(revID1.Length, revID2.Length);
            var result = memcmp(revID1, revID2, (UIntPtr)len);

            if(result == 0) {
                result = revID1.Length - revID2.Length;
            }

            return Math.Sign(result);
        }

        public override string ToString()
        {
            return Encoding.ASCII.GetString(AsData());
        }

        public override int GetHashCode()
        {
            var data = AsData();
            unchecked {
                int hash = 19;
                foreach(var b in data) {
                    hash = hash * 31 + b;
                }

                return hash;
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as RevisionID;
            if(other == null) {
                var otherStr = obj as string;
                if(otherStr != null) {
                    return ToString() == otherStr;
                }

                return false;
            }

            return AsData().SequenceEqual(other.AsData());
        }

        public abstract int CompareTo(RevisionID other);

        public bool Equals(string other)
        {
            return ToString().Equals(other);
        }

        public static bool operator ==(RevisionID left, RevisionID right)
        {
            if(ReferenceEquals(left, null)) {
                return ReferenceEquals(right, null);
            }

            return left.Equals(right);
        }

        public static bool operator !=(RevisionID left, RevisionID right)
        {
            if(ReferenceEquals(left, null)) {
                return !ReferenceEquals(right, null);
            }

            return !left.Equals(right);
        }
    }

    internal static class RevisionIDExt
    {
        public static RevisionID AsRevID(this string str)
        {
            return RevisionIDFactory.FromString(str);
        }

        public static IEnumerable<RevisionID> AsRevIDs(this IList<string> list)
        {
            return list?.Select(x => RevisionIDFactory.FromString(x));
        }

        public static IEnumerable<RevisionID> AsMaybeRevIDs(this IList list)
        {
            foreach(var obj in list) {
                var str = obj as string;
                if(str == null) {
                    yield return null;
                }

                yield return RevisionIDFactory.FromString(str);
            }
        }
    }
}

