//
//  StorageEngineRule.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Util;

namespace Couchbase.Lite.Internal
{
    internal sealed class StorageEngineRule : LogicalDictionaryRule<string, Type>
    {
        protected override bool CanChangeKey(string key, Type newVal, Type oldVal)
        {
#if __IOS__
            return false;
#else
            return key == StorageEngineTypes.SQLite && oldVal.Namespace == "Couchbase.Lite.Storage.SystemSQLite";
#endif
        }

        protected override bool CanRemoveKey(string key)
        {
            return false;
        }

        protected override string GetExceptionMessage_Internal(LogicalDictionaryEvent e, string key, Type newVal, Type oldVal)
        {
            if(e == LogicalDictionaryEvent.Remove) {
                return "Removals not allowed for this dictionary!";
            }

            return $"Cannot change value for {key} storage engine once set!";
        }
    }
}