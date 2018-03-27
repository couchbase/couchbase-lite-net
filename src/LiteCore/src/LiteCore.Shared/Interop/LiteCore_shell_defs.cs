// 
//  LiteCore_shell_defs.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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
using Couchbase.Lite.DI;

using JetBrains.Annotations;

using LiteCore.Interop;

namespace Couchbase.Lite.Interop
{
    internal static partial class Native
    {
        #region Constants

        [NotNull]
        private static readonly ILiteCore Impl = Service.GetRequiredInstance<ILiteCore>();

        #endregion
    }

    internal static partial class NativeRaw
    {
        #region Constants

        [NotNull]
        private static readonly ILiteCoreRaw Impl = Service.GetRequiredInstance<ILiteCoreRaw>();

        #endregion
    }

}
