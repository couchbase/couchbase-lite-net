using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.iOS
{

    public abstract class CouchBaseTableDelegate : UITableViewDelegate
    {
        public abstract UITableViewCell CellForRowAtIndexPath (CouchbaseTableSource source, NSIndexPath indexPath);

        public abstract void WillUpdateFromQuery (CouchbaseTableSource source, LiveQuery query);

        public abstract void UpdateFromQuery (CouchbaseTableSource source, LiveQuery query, QueryRow [] previousRows);

        public abstract void WillUseCell (CouchbaseTableSource source, UITableViewCell cell, QueryRow row);

        public abstract bool DeleteRow (CouchbaseTableSource source, QueryRow row);

        public abstract void DeleteFailed (CouchbaseTableSource source);
    }
}
