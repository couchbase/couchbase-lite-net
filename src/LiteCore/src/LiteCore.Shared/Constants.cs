//
// Constants.cs
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

using NotNull = JetBrains.Annotations.NotNullAttribute;

namespace LiteCore
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    internal static class Constants
    {
        #if __IOS__
        [NotNull]
        internal const string DllName = "@rpath/LiteCore.framework/LiteCore";
        #else
        [NotNull]
        internal const string DllName = "LiteCore";
        #endif
        
        [NotNull]
        internal static readonly string ObjectTypeProperty = "@type";
        
        [NotNull]
        internal static readonly string ObjectTypeBlob = "blob";
        
        [NotNull]
        internal static readonly string C4LanguageDefault = null;
        
        [NotNull]
        internal static readonly string C4LanguageNone = "";
        
        [NotNull]
        internal static readonly string C4PlaceholderValue = "*";
    }
}
