using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Lite.Portable;

namespace Couchbase.Lite.iOS
{

    public abstract class CouchBaseTableDelegate : UITableViewDelegate
    {
        public abstract UITableViewCell CellForRowAtIndexPath (CouchbaseTableSource source, NSIndexPath indexPath);

        public abstract void WillUpdateFromQuery (CouchbaseTableSource source, ILiveQuery query);

        public abstract void UpdateFromQuery (CouchbaseTableSource source, ILiveQuery query, IQueryRow [] previousRows);

        public abstract void WillUseCell (CouchbaseTableSource source, UITableViewCell cell, IQueryRow row);

        public abstract bool DeleteRow (CouchbaseTableSource source, IQueryRow row);

        public abstract void DeleteFailed (CouchbaseTableSource source);
    }
}
