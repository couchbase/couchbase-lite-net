//
// Misc.cs
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
using Sharpen;

namespace Couchbase.Lite.Storage.SystemSQLite.Internal
{
    internal static class Misc
    {
        internal static int CBLCompareRevIDs(string revId1, string revId2)
        {
            System.Diagnostics.Debug.Assert((revId1 != null));
            System.Diagnostics.Debug.Assert((revId2 != null));
            return CBLCollateRevIDs(revId1, revId2);
        }

        internal static int CBLCollateRevIDs(string revId1, string revId2)
        {
            string rev1GenerationStr = null;
            string rev2GenerationStr = null;
            string rev1Hash = null;
            string rev2Hash = null;
            var st1 = new StringTokenizer(revId1, "-");
            try {
                rev1GenerationStr = st1.NextToken();
                rev1Hash = st1.NextToken();
            } catch (Exception) {
            }

            StringTokenizer st2 = new StringTokenizer(revId2, "-");
            try {
                rev2GenerationStr = st2.NextToken();
                rev2Hash = st2.NextToken();
            } catch (Exception) {
            }

            // improper rev IDs; just compare as plain text:
            if (rev1GenerationStr == null || rev2GenerationStr == null) {
                return revId1.CompareToIgnoreCase(revId2);
            }

            int rev1Generation;
            int rev2Generation;
            try {
                rev1Generation = System.Convert.ToInt32(rev1GenerationStr);
                rev2Generation = System.Convert.ToInt32(rev2GenerationStr);
            } catch (FormatException) {
                // improper rev IDs; just compare as plain text:
                return revId1.CompareToIgnoreCase(revId2);
            }

            // Compare generation numbers; if they match, compare suffixes:
            if (rev1Generation.CompareTo(rev2Generation) != 0) {
                return rev1Generation.CompareTo(rev2Generation);
            } else {
                if (rev1Hash != null && rev2Hash != null) {
                    // compare suffixes if possible
                    return String.CompareOrdinal(rev1Hash, rev2Hash);
                } else {
                    // just compare as plain text:
                    return revId1.CompareToIgnoreCase(revId2);
                }
            }
        }
    }
}

