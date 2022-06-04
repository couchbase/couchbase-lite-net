using Couchbase.Lite.Query;
using Couchbase.Lite.Support;
using JetBrains.Annotations;
using LiteCore.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.Lite
{
    public sealed unsafe class Scope : IDisposable
    {
        #region Properties

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
        public string Name { get; }

        /// <summary>
        /// Gets all collections in the Scope
        /// </summary>
        public IReadOnlyList<Collection> Collections { get; private set; } = new List<Collection>();

        internal C4Database* c4Db
        {
            get {
                Debug.Assert(Database != null && Database.c4db != null);
                return Database.c4db;
            }
        }

        /// <summary>
        /// Gets the database that this document belongs to, if any
        /// </summary>
        [NotNull]
        internal Database Database { get; set; }

        [NotNull]
        internal ThreadSafety ThreadSafety { get; }

        #endregion

        #region Constructors

        internal Scope([NotNull] Database database, string scope)
        {
            Database = database;
            ThreadSafety = database.ThreadSafety;

            Name = scope;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets one collection of the given name
        /// </summary>
        /// <param name="name">The collection name</param>
        /// <returns>null will be returned if the collection doesn't exist in the Scope</returns>
        public Collection GetCollection(string name)
        {
            var colls = Collections as List<Collection>;
            return colls.SingleOrDefault(x => x.Name == name);
        }

        #endregion

        #region Internal Methods

        internal void Add(Collection collection)
        {
            (Collections as List<Collection>).Add(collection);
        }

        internal void Delete(Collection collection)
        {
            (Collections as List<Collection>).Remove(collection);
        }

        internal void GetCollections()
        {
            (Collections as List<Collection>).Clear();
            ThreadSafety.DoLocked(() =>
            {
                var arrColl = Native.c4db_collectionNames(c4Db, Name);
                for (uint i = 0; i < Native.FLArray_Count((FLArray*)arrColl); i++) {
                    var collStr = (string)FLSliceExtensions.ToObject(Native.FLArray_Get((FLArray*)arrColl, i));
                    var col = new Collection(Database, collStr, Name);
                    (Collections as List<Collection>).Add(col);
                }

                Native.FLValue_Release((FLValue*)arrColl);
            });
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            ThreadSafety.DoLocked(() =>
            {
                (Collections as List<Collection>).Clear();
                Collections = null;
            });
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
