using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Couchbase.Lite.iOS
{
    [Register("CouchbaseTableSource")]
    public class CouchbaseTableSource : UITableViewDataSource
    {
        public event EventHandler<ReloadEventArgs> WillReload;
        public event EventHandler<ReloadEventArgs> Reload;

        LiveQuery query;
        public virtual LiveQuery Query {
            get { return query; }
            set {
                if (query == value) return;

                query = value;
                query.Changed += async (sender, e) => ReloadFromQuery();
            }
        }

        public virtual Boolean DeletionAllowed { get; set; }

        public virtual UITableView TableView { get; set; }

        public virtual QueryEnumerator Rows { get; set; }

        public CouchbaseTableSource ()
        {
            Initialize ();
        }

        public CouchbaseTableSource(NSCoder coder) : base(coder)
        {
            Initialize ();
        }

        public CouchbaseTableSource(IntPtr ptr) : base(ptr)
        {
            Initialize ();
        }

        TaskFactory Factory;

        void Initialize ()
        {
            DeletionAllowed = true;
            Factory = new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext());
        }

        #region implemented abstract members of UITableViewDataSource
        public virtual void ReloadFromQuery ()
        {
            var enumerator = Query.Rows;
            if (enumerator == null) return;

            var oldRows = Rows;
            Rows = new QueryEnumerator(enumerator);

            var evt = WillReload;
            if (evt != null)
            {
                var args = new ReloadEventArgs(Query);
//                UIApplication.SharedApplication.InvokeOnMainThread(new NSAction(()=> evt(this, args)));
                evt(this, args);
            }

            var reloadEvt = Reload;
            if (evt != null) {
                var args = new ReloadEventArgs(Query, oldRows);
                reloadEvt(this, args);
//                UIApplication.SharedApplication.InvokeOnMainThread(new NSAction(()=> reloadEvt(this, args)));
            } else {
//                UIApplication.SharedApplication.InvokeOnMainThread(
//                    new NSAction (TableView.ReloadData));
                TableView.ReloadData();
            }
        }

        public virtual QueryRow RowAtIndex (int index)
        {
            return Rows.GetRow(index);
        }

        public virtual NSIndexPath IndexPathForDocument (Document document)
        {
            var documentId = document.Id;
            var index = 0;
            foreach (var row in Rows)
            {
                if (row.DocumentId.Equals(documentId))
                    return NSIndexPath.FromRowSection(row: index, section: 0);
                index++;
            }
            return null;
        }

        public virtual QueryRow RowAtIndexPath (NSIndexPath path)
        {
            if (path.Section == 0)
            {
                return Rows.GetRow(path.Row);
            }
            return null;
        }

        public virtual Document DocumentAtIndexPath (NSIndexPath path)
        {
            return RowAtIndexPath(path).Document;
        }

        public virtual bool DeleteDocumentsAtIndexes (NSIndexPath [] indexPaths)
        {
            var documents = indexPaths.Select(DocumentAtIndexPath);
            return DeleteDocumentsAtIndexes(documents, indexPaths);
        }

        public virtual bool DeleteDocuments (IEnumerable<Document> documents) 
        {
            var paths = documents.Select(IndexPathForDocument);
            return DeleteDocumentsAtIndexes(documents, paths);
        }

        public virtual bool DeleteDocumentsAtIndexes (IEnumerable<Document> documents, IEnumerable<NSIndexPath> indexPaths)
        {
            var result = Query.Database.RunInTransaction(()=>{
                foreach(var doc in documents) {
                    if (doc.CurrentRevision.DeleteDocument() == null)
                        return false;
                }
                return true;
            });

            if (!result)
                throw new CouchbaseLiteException("Could not delete one or more docuements.");

            var paths = indexPaths.ToArray();

            TableView.DeleteRows(paths, UITableViewRowAnimation.Fade);

            return true;
        }

        public override void CommitEditingStyle (UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
        {
            if (editingStyle != UITableViewCellEditingStyle.Delete) return;

            // Delete the document from the database.

            var row = RowAtIndex(indexPath.Row);
            var tableDelegate = TableView.Delegate as CouchBaseTableDelegate;
            if (tableDelegate != null) {
                if (!tableDelegate.DeleteRow(this, row))
                    return;
            } else {
                if (row.Document.CurrentRevision.DeleteDocument() != null) {
                    tableDelegate.DeleteFailed(this);
                    return;
                }
            }

            // Delete the row from the table data source.
            Rows.ToList().RemoveAt(indexPath.Row);
            TableView.DeleteRows(
                atIndexPaths: new[] { indexPath },
                withRowAnimation: UITableViewRowAnimation.Fade
            );
        }

        public override bool CanEditRow (UITableView tableView, NSIndexPath indexPath)
        {
            return DeletionAllowed;
        }

        public override bool CanMoveRow (UITableView tableView, NSIndexPath indexPath)
        {
            return false;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            // Allow the delegate to create its own cell:
            var tableDelegate = TableView.Delegate as CouchBaseTableDelegate;
            UITableViewCell cell = null;

            if (tableDelegate != null)
                cell = tableDelegate.CellForRowAtIndexPath(this, indexPath);

            if (cell == null) {
                // ...if it doesn't, create a cell for it:
                cell = TableView.DequeueReusableCell(identifier: @"CouchBaseTableDelegate");
                if (cell == null)
                    cell = new UITableViewCell(
                        style: UITableViewCellStyle.Default,
                        reuseIdentifier: @"CouchBaseTableDelegate"
                    );

                var row = RowAtIndex(indexPath.Row);
                cell.TextLabel.Text = GetLabel(row);
                 
                // Allow the delegate to customize the cell:
                if (tableDelegate != null)
                    tableDelegate.WillUseCell(this, cell, row);
            }

            return cell;
        }

        public override int RowsInSection (UITableView tableView, int section)
        {
            var rows = Rows != null ? Rows.Count : 0;
            return rows;
        }

        public virtual string LabelProperty { get; set; }

        String GetLabel (QueryRow row)
        {
            var value = row.Value;

            if (LabelProperty != null) {
                if (value is IDictionary<string,object>)
                    ((IDictionary<string,object>)value).TryGetValue(LabelProperty, out value);
                else
                    value = null;

                if (value == null)
                    value = row.Document.GetProperty(LabelProperty);
            }

            return value == null ? String.Empty : value.ToString();
        }
        #endregion
    }
}

