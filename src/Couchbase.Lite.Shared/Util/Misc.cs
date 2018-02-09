// 
// Misc.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Text;
using System.Threading;

using JetBrains.Annotations;

namespace Couchbase.Lite.Util
{
    internal static class Misc
    {
        #region Public Methods

        [NotNull]
        public static TClass TryCast<TInterface, TClass>(TInterface iface)
            where TClass : class, TInterface
        {
            return iface as TClass ??
                   throw new NotSupportedException($"Custom {typeof(TInterface).Name} is not supported");
        }

        public static void SafeSwap<T>(ref T old, T @new) where T : class, IDisposable
        {
            var oldRef = Interlocked.Exchange(ref old, @new);
            oldRef?.Dispose();
        }

        public static string CreateGuid()
        {
            var sb = new StringBuilder(Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('='));

            // URL-safe character set per RFC 4648 sec. 5:
            sb.Replace('/', '_');
            sb.Replace('+', '-');

            // prefix a '-' to make it more clear where this string came from and prevent having a leading
            // '_' character:
            sb.Insert(0, '-');
            return sb.ToString();
        }

        #endregion
    }
}
