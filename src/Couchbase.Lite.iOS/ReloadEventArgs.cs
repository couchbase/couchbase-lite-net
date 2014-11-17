using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Portable;


namespace Couchbase.Lite.iOS
{
    public class ReloadEventArgs : EventArgs
    {
        public IQuery Query { get; private set; }
        public IQueryEnumerator Rows { get; private set; }

        public ReloadEventArgs(IQuery query, IQueryEnumerator rows = null)
        {
            Query = query;
            Rows = rows;
        }
    }

}

