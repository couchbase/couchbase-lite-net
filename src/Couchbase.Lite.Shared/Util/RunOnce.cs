using System;
using System.Collections.Concurrent;
using System.Diagnostics;

using JetBrains.Annotations;

namespace Couchbase.Lite.Util
{
    internal static class Run
    {
        #region Constants

        [NotNull] private static readonly ConcurrentDictionary<string, byte> Instances =
            new ConcurrentDictionary<string, byte>();

        #endregion

        #region Public Methods

        public static void Once([NotNull]string identifier, [NotNull] Action a)
        {
            Debug.Assert(identifier != null);
            Debug.Assert(a != null);

            if (Instances.TryAdd(identifier, 0)) {
                a();
            }
        }

        #endregion

        #region Internal Methods

#if DEBUG

        internal static void Clear([NotNull] string identifier)
        {
            Instances.TryRemove(identifier, out var tmp);
        }

#endif

        #endregion
    }
}
