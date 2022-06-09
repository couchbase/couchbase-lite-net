using Couchbase.Lite.Query;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed class Scope : IDisposable
    {
        /// <summary>
        /// Gets the Scope Name
        /// </summary>
        /// <remarks>
        /// Naming rules:
        /// Must be between 1 and 251 characters in length.
        /// Can only contain the characters A-Z, a-z, 0-9, and the symbols _, -, and %. 
        /// Cannot start with _ or %.
        /// Case sensitive.
        /// </remarks>
        public string Name => throw new NotImplementedException();

        /// <summary>
        /// Gets all collections in the Scope
        /// </summary>
        public IReadOnlyList<Collection> Collections => throw new NotImplementedException();

        public ListenerToken AddChangeListener([CanBeNull] TaskScheduler scheduler, [NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public ListenerToken AddChangeListener([NotNull] EventHandler<DatabaseChangedEventArgs> handler)
        {
            throw new NotImplementedException();
        }

        public void RemoveChangeListener(ListenerToken token)
        {
            throw new NotImplementedException();
        }

        public IQuery CreateQuery([NotNull] string queryExpression)
        {
            throw new NotImplementedException();
        }

        public Collection GetCollection(string name)
        {
            throw new NotImplementedException();
        }

        #region object
        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Name?.GetHashCode() ?? 0;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            if (!(obj is Scope other)) {
                return false;
            }

            return String.Equals(Name, other.Name, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override string ToString() => $"SCOPE[{Name}]";
        #endregion

        #region IDisposable

        public void Dispose()
        {

        }

        #endregion
    }
}
