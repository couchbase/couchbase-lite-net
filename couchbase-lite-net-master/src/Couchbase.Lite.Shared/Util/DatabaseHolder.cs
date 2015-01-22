using System;
using System.Collections.Generic;
using Couchbase.Lite.Portable;

namespace Couchbase.Lite.Shared
{
    /// <summary>
    /// rolls up dealign with the database ref in one place.
    /// </summary>
    public abstract class DatabaseHolder : IDatabaseHolder
    {
        /// <summary>
        /// Gets the local <see cref="Couchbase.Lite.Database"/> being replicated to/from.
        /// </summary>
        /// <value>The local <see cref="Couchbase.Lite.Database"/> being replicated to/from.</value>
        public IDatabase Database
        {
            get { return mydb; }
            //set both ref here, so we fail early, and reduce casting everytime the internal ref is called
            protected set { DatabaseInternal = (Database)value; mydb = value; }
        }
        protected IDatabase mydb;
        internal Database DatabaseInternal { private set; get; }//{ return (Database)this.LocalDatabase; }}

    }
}