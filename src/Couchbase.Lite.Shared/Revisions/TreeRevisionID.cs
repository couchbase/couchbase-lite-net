//
// TreeRevisionID.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using Couchbase.Lite.Util;
using Newtonsoft.Json;

namespace Couchbase.Lite.Revisions
{
    internal sealed class TreeRevisionID : RevisionID
    {
        private readonly byte[] _data;
        private static readonly string Tag = typeof(TreeRevisionID).Name;

        public override int Generation
        {
            get {
                var pos = Array.IndexOf(_data, (byte)'-');
                if(pos == -1 || pos > 9) {
                    return 0;
                }

                return ParseDigits(_data, pos);
            }
        }

        public override string Suffix
        {
            get {
                var pos = Array.IndexOf(_data, (byte)'-') + 1;
                if(pos == 0) {
                    return null;
                }

                var length = _data.Length - pos;
                if(length == 0) {
                    return null;
                }

                return Encoding.ASCII.GetString(_data, pos, length);
            }
        }

        public TreeRevisionID(IEnumerable<byte> data)
        {
            _data = data.ToArray();
        }

        public static RevisionID RevIDForJSON(IEnumerable<byte> json, bool deleted, RevisionID prevRevID)
        {
            // Revision IDs have a generation count, a hyphen, and a hex digest
            var generation = 0;
            if(prevRevID != null) {
                generation = prevRevID.Generation;
                if(generation == 0) {
                    return null;
                }
            }

            // Generate a digest for this revision based on the previous revision ID, document JSON,
            // and attachment digests. This doesn't need to be secure; we just need to ensure that this
            // code consistently generates the same ID given equivalent revisions.
            MessageDigest md5Digest;
            try {
                md5Digest = MessageDigest.GetInstance("MD5");
            } catch(NotSupportedException) {
                throw Misc.CreateExceptionAndLog(Log.To.Database, Tag, "Failed to acquire a class to create MD5");
            }

            if(prevRevID != null) {
                var prevIDData = prevRevID.AsData();
                var length = prevIDData.Length;
                var lengthByte = unchecked((byte)(length & unchecked((0xFF))));
                md5Digest.Update(lengthByte);
                if(lengthByte > 0) {
                    md5Digest.Update(prevIDData);
                }
            }

            var isDeleted = deleted ? 1 : 0;
            md5Digest.Update((byte)isDeleted);

            if(json != null) {
                md5Digest.Update(json.ToArray());
            }

            var md5DigestResult = md5Digest.Digest();
            var digestAsHex = BitConverter.ToString(md5DigestResult).Replace("-", String.Empty);
            int generationIncremented = generation + 1;
            return RevisionIDFactory.FromString(String.Format("{0}-{1}", generationIncremented, digestAsHex).ToLower());
        }

        public static IDictionary<string, object> MakeRevisionHistoryDict(IList<RevisionID> history)
        {
            if(history == null) {
                return null;
            }

            var suffixes = new List<string>();
            int? start = null;
            var lastRevNo = -1;
            foreach(var revID in history) {
                var revNo = revID.Generation;
                var suffix = revID.Suffix;
                if(revNo > 0 && suffix != null) {
                    if(!start.HasValue) {
                        start = revNo;
                    } else if(revNo != lastRevNo - 1) {
                        start = null;
                        break;
                    }

                    lastRevNo = revNo;
                    suffixes.Add(suffix);
                } else {
                    start = null;
                    break;
                }
            }

            if(start.HasValue) {
                return new Dictionary<string, object> {
                    { "ids", suffixes },
                    { "start", start }
                };
            } else {
                return new Dictionary<string, object> {
                    { "ids", history.Select(x => x.ToString()) }
                };
            }
        }

        public static IList<RevisionID> ParseRevisionHistoryDict(IDictionary<string, object> dict)
        {
            if(dict == null) {
                return null;
            }

            // Extract the history, expanding the numeric prefixes
            var start = dict.GetCast<int>("start");
            var revIDs = dict.Get("ids").AsList<string>();
            return revIDs?.Select(x =>
            {
                var str = x as string;
                if(str == null) {
                    return null;
                }

                if(start > 0) {
                    str = String.Format("{0}-{1}", start--, str);
                }

                return str.AsRevID();
            })?.ToList();
        }

        public override byte[] AsData()
        {
            return _data;
        }

        public override int CompareTo(RevisionID other)
        {
            var otherCast = other as TreeRevisionID;
            if(otherCast == null) {
                Log.To.Database.E(Tag, "Cannot compare to {0}, throwing...", other.GetType().Name);
                throw new ArgumentException(String.Format("Cannot compare TreeRevisionID to {0}", other.GetType().Name));
            }

            return CBLCollateRevIDs(_data, otherCast._data);
        }

        public int Compare(RevisionID x, RevisionID y)
        {
            return x.CompareTo(y);
        }
    }
}
