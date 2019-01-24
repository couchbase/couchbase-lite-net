using System;
using System.Collections.Concurrent;
using System.Reflection;

using Couchbase.Lite.Internal.Logging;

using JetBrains.Annotations;

namespace Couchbase.Lite.Util
{
    internal sealed class RunOnce
    {
        #region Constants

        private const string Tag = nameof(RunOnce);

        [NotNull] private static readonly ConcurrentDictionary<string, RunOnce> Instances =
            new ConcurrentDictionary<string, RunOnce>();

        #endregion

        #region Variables

        [NotNull]
        private readonly string _id;

        [NotNull]
        private readonly ConcurrentDictionary<MethodInfo, bool> _seen = new ConcurrentDictionary<MethodInfo, bool>();

        #endregion

        #region Constructors

        private RunOnce([NotNull]string identifier)
        {
            _id = identifier;
        }

        #endregion

        #region Public Methods

        public static RunOnce GetInstance([NotNull]string identifier)
        {
            return Instances.GetOrAdd(identifier, k => new RunOnce(k));
        }

        public void Run([NotNull]Action a)
        {

            if (_seen.TryAdd(a.Method, false)) {
                WriteLog.To.Database.V(Tag, "Executing logic for {0}", _id);
                a();
            }
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return $"RunOnce => [{_id}]";
        }

        #endregion
    }
}
