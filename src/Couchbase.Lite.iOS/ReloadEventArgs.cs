using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.iOS
{
    public class ReloadEventArgs : EventArgs
    {
        public Query Query { get; private set; }
        public QueryEnumerator Rows { get; private set; }

        public ReloadEventArgs(Query query, QueryEnumerator rows = null)
        {
            Query = query;
            Rows = rows;
        }
    }

}

