using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {
    
    public partial class QueryEnumerator : IEnumerator<QueryRow>
    {

    #region Constructors

        internal QueryEnumerator (Database database, IEnumerable<QueryRow> rows, Int64 lastSequence)
        {
            Database = database;
            Rows = rows;
            SequenceNumber = lastSequence;

            Reset();
        }

    #endregion

    #region Non-public Members
    
        private Database Database { get; set; }

        private IEnumerable<QueryRow> Rows { get; set; }

        private Int32 CurrentRow { get; set; }

    #endregion

    #region Instance Members
        //Properties
        public Int32 Count { get { return Rows.Count(); } }

        public Int64 SequenceNumber { get; private set; }

        /// <summary>True if the database has changed since the view was generated.</summary>
        public Boolean Stale { get { return SequenceNumber < Database.GetLastSequenceNumber(); } }

        /// <summary>
        /// Advances to the next QueryRow in the results, or false
        /// if there are no more results.
        /// </summary>
//        public QueryRow Next() { throw new NotImplementedException(); }

        public QueryRow GetRow(Int32 index) {
            var row = Rows.ElementAt(index);
            row.Database = Database; // Avoid multiple enumerations by doing this here instead of the constructor.
            return row;
        }

    #endregion

    #region Operator Overloads

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            return GetHashCode() == obj.GetHashCode();
        }

        public override int GetHashCode ()
        {
            var idString = String.Format("{0}{1}{2}{3}", Database.Path, Count, SequenceNumber, Stale);
            return idString.GetHashCode ();
        }


    #endregion

    #region IEnumerator Implementation

        public void Reset() {
            CurrentRow = -1; 
            Current = null;
        }

        public QueryRow Current { get; private set; }

        public Boolean MoveNext ()
        {
            if (CurrentRow++ >= Count)
                return false;

            Current = GetRow(CurrentRow);

            return true;
        }

        public void Dispose ()
        {
            Database = null;
            Rows = null;
        }

        Object IEnumerator.Current { get { return Current; } }

    #endregion

    }

    

}
