// 
//  Document+private.cs
// 
//  Copyright (c) 2024 Couchbase, Inc All rights reserved.
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

namespace Couchbase.Lite.Unsupported;

public static class DocumentExtensions
{
    /// <summary>
    /// Gets the revision IDs of the document from the version vector.  This
    /// is an unsupported API and may be removed or changed at any time.
    /// </summary>
    /// <param name="doc">The doc to get the revision IDs from</param>
    /// <returns>The revision IDs of the document</returns>
    public static string? RevisionIDs(this Document doc) => doc.RevisionIDs;
}