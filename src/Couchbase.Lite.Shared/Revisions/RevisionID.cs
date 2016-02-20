//
// RevID.cs
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
using System.Globalization;

namespace Couchbase.Lite.Revisions
{
    internal static class RevisionID
    {
        public static int GetGeneration(string revID)
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

        public static Tuple<int, string> ParseRevId(string revId)
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

        internal static int CBLCollateRevIDs(string revId1, string revId2)
        {
            var parsed1 = RevisionID.ParseRevId(revId1);
            var parsed2 = RevisionID.ParseRevId(revId2);

            // improper rev IDs; just compare as plain text:
            if (parsed1.Item1 == -1 || parsed2.Item1 == -1) {
                return String.Compare(revId1, revId2, true, CultureInfo.InvariantCulture);
            }

            // Compare generation numbers; if they match, compare suffixes:
            if (parsed1.Item1 != parsed2.Item1) {
                return parsed1.Item1 - parsed2.Item1;
            } else {
                if (parsed1.Item2 != null && parsed2.Item2 != null) {
                    // compare suffixes if possible
                    return String.CompareOrdinal(parsed1.Item2, parsed2.Item2);
                } else {
                    // just compare as plain text:
                    return String.Compare(revId1, revId2, true, CultureInfo.InvariantCulture);
                }
            }
        }

        internal static int CBLCompareRevIDs(string revId1, string revId2)
        {
            System.Diagnostics.Debug.Assert((revId1 != null));
            System.Diagnostics.Debug.Assert((revId2 != null));
            return CBLCollateRevIDs(revId1, revId2);
        }
    }
}

