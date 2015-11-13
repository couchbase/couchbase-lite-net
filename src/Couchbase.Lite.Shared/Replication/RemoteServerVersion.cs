//
// ServerVersion.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.IO;
using System.Text.RegularExpressions;

namespace Couchbase.Lite
{
    internal sealed class RemoteServerVersion
    {
        public readonly string Name;
        public readonly string Version;
        public readonly string Branch;
        public readonly string Commit;

        public bool IsSyncGateway
        {
            get {
                return Name == "Couchbase Sync Gateway";
            }
        }

        public RemoteServerVersion(string versionString)
        {
            var newPattern = new Regex("Couchbase Sync Gateway/([0-9]+\\.[0-9]+) branch/(.*?)commit/(.*)");
            var fallback = new Regex("(.*)/(.*)");

            var newPatternMatch = newPattern.Match(versionString);
            if (newPatternMatch.Success) {
                Name = "Couchbase Sync Gateway";
                Version = newPatternMatch.Groups[1].Value;
                Branch = newPatternMatch.Groups[2].Value.TrimEnd();
                Commit = newPatternMatch.Groups[3].Value;
            } else {
                Branch = Commit = String.Empty;
                var fallbackMatch = fallback.Match(versionString);
                if (!fallbackMatch.Success) {
                    Version = String.Empty;
                    Name = String.Empty; 
                } else {
                    Version = fallbackMatch.Groups[2].Value;
                    Name = fallbackMatch.Groups[1].Value;
                }
            }
        }

        public override string ToString()
        {
            if(!String.IsNullOrEmpty(Branch)) {
                return string.Format("[Couchbase Sync Gateway: Version={0} Branch={1} Commit={2}]", Version, Branch, Commit);
            } else {
                return string.Format("[{0}: Version={1}]", Name, Version);
            }
        }
    }
}

