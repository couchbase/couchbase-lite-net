//
//  CouchBaseTableDelegate.cs
//
//  Author:
//      Unknown (Current maintainer: Jim Borden  <jim.borden@couchbase.com>)
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;

namespace Couchbase.Lite.iOS
{
    /// <summary>
    /// A data adapter class that holds data for use in conjunction with <see cref="Couchbase.Lite.iOS.CouchBaseTableDelegate" />
    /// </summary>
    [Register("CouchbaseTableSource")]
    public class CouchbaseTableSource : UITableViewDataSource
    {

        #region Variables

        /// <summary>
        /// An event that is fired before the data set is reloaded
        /// </summary>
        public event EventHandler<ReloadEventArgs> WillReload;

        /// <summary>
        /// An event that is fired when the data set is reloaded
        /// </summary>
        public event EventHandler<ReloadEventArgs> Reload;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the query that will drive this data source
        /// </summary>
        public virtual LiveQuery Query {
            get { return query; }
            set {
                if (query == value) return;

                query = value;
                query.Changed += (sender, e) => ReloadFromQuery();
            }
        }
        private LiveQuery query;

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="Couchbase.Lite.iOS.CouchbaseTableSource"/> allows deletions.
        /// </summary>
        /// <value><c>true</c> if deletion allowed; otherwise, <c>false</c>.</value>
        public virtual Boolean DeletionAllowed { get; set; }

        /// <summary>
        /// Gets or sets the UITableView that uses this object as a source
        /// </summary>
        /// <value>The table view.</value>
        public virtual UITableView TableView { get; set; }

        /// <summary>
        /// Gets or sets the rows of the underlying query
        /// </summary>
        /// <value>The rows.</value>
        public virtual QueryEnumerator Rows { get; set; }

        /// <summary>
        /// Gets or sets the propery name used for storing the value of a given query row
        /// </summary>
        public virtual string LabelProperty { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        public CouchbaseTableSource ()
        {
            Initialize ();
        }

        /// <summary>
        /// Constructor (iOS internal use)
        /// </summary>
        /// <param name="coder">An object with the saved state of this object</param>
        public CouchbaseTableSource(NSObjectFlag coder) : base(coder)
        {
            Initialize ();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="ptr">Used by base clase</param>
        public CouchbaseTableSource(IntPtr ptr) : base(ptr)
        {
            Initialize ();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Reloads the data set from the underlying query
        /// </summary>
        public virtual void ReloadFromQuery()
        {
            var enumerator = Query.Rows;
            if (enumerator == null) return;

            var oldRows = Rows;
            Rows = new QueryEnumerator(enumerator);

            var evt = WillReload;
            if (evt != null)
            {
                var args = new ReloadEventArgs(Query);
                evt(this, args);
            }

            var reloadEvt = Reload;
            if (reloadEvt != null) {
                var args = new ReloadEventArgs(Query, oldRows);
                reloadEvt(this, args);
            } else {
                TableView.ReloadData();
            }
        }

        /// <summary>
        /// Gets the query result at the given index
        /// </summary>
        /// <returns>The data for the row of the data set</returns>
        /// <param name="index">The index to grab</param>
        public virtual QueryRow RowAtIndex (int index)
        {
            return Rows.GetRow(index);
        }

        /// <summary>
        /// Scans the result set for the index of a given document
        /// </summary>
        /// <returns>The index of the document.</returns>
        /// <param name="document">The document to search for</param>
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

        /// <summary>
        /// Gets the query row at the given index
        /// </summary>
        /// <returns>The data for the row of the data set</returns>
        /// <param name="path">The index to grab</param>
        public virtual QueryRow RowAtIndexPath (NSIndexPath path)
        {
            if (path.Section == 0)
            {
                return Rows.GetRow(path.Row);
            }
            return null;
        }

        /// <summary>
        /// Gets the document of the row at the given index
        /// </summary>
        /// <returns>The document for the row of the data set</returns>
        /// <param name="path">The index to grab</param>
        public virtual Document DocumentAtIndexPath (NSIndexPath path)
        {
            return RowAtIndexPath(path).Document;
        }

        /// <summary>
        /// Deletes the documents at the specified indices
        /// </summary>
        /// <returns><c>true</c>, if the documents at the indices were deleted, <c>false</c> otherwise.</returns>
        /// <param name="indexPaths">The index paths to delete the documents at</param>
        public virtual bool DeleteDocumentsAtIndexes (NSIndexPath[] indexPaths)
        {
            var documents = indexPaths.Select(DocumentAtIndexPath);
            return DeleteDocumentsAtIndexes(documents, indexPaths);
        }

        /// <summary>
        /// Deletes the specified documents
        /// </summary>
        /// <returns><c>true</c>, if documents were deleted, <c>false</c> otherwise.</returns>
        /// <param name="documents">The documents to delete</param>
        public virtual bool DeleteDocuments (IEnumerable<Document> documents) 
        {
            var paths = documents.Select(IndexPathForDocument);
            return DeleteDocumentsAtIndexes(documents, paths);
        }

        /// <summary>
        /// Deletes the documents at the specified indices
        /// </summary>
        /// <returns><c>true</c>, if the documents at the indices were deleted, <c>false</c> otherwise.</returns>
        /// <param name="documents">The documents to delete</param> 
        /// <param name="indexPaths">The index paths to delete the documents at</param>
        //FIXME: Why does this function have the indexPaths argument?
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

            return true;
        }

        #endregion

        #region Private Methods

        private void Initialize ()
        {
            DeletionAllowed = true;
        }

        private string GetLabel (QueryRow row)
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

        #region UITableViewDataSource
        #pragma warning disable 1591

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

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            var rows = Rows != null ? Rows.Count : 0;
            return rows;
        }

        #pragma warning restore 1591
        #endregion
    }
}

