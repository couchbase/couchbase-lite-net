// 
//  Meta.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Internal.Query;

using JetBrains.Annotations;

namespace Couchbase.Lite.Query
{
    public static class Meta
    {
        private const string IDKeyPath = "_id";
        private const string IDColumnName = "id";
        private const string SequenceKeyPath = "_sequence";
        private const string SequenceColumnName = "sequence";

        #region Properties
        
        [NotNull]
        public static IMetaExpression ID => new QueryTypeExpression(IDKeyPath, IDColumnName);
        
        [NotNull]
        public static IMetaExpression Sequence => new QueryTypeExpression(SequenceKeyPath, SequenceColumnName);

        #endregion
    }
}