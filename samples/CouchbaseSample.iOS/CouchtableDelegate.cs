using System;
using System.Drawing;
using System.Collections.Generic;
using UIKit;
using Foundation;
using Couchbase;
using System.Diagnostics;
using System.Linq;
using Couchbase.Lite.iOS;
using Couchbase.Lite;
using Newtonsoft.Json.Linq;

namespace CouchbaseSample
{
    public class CouchtableDelegate : CouchBaseTableDelegate
    {
        static UIColor backgroundColor;
        RootViewController parent;
        CouchbaseTableSource dataSource;

        public CouchtableDelegate (RootViewController controller, CouchbaseTableSource source)
        {
          parent = controller;
          dataSource = source;
        }

        #region implemented abstract members of CouchBaseTableDelegate

        public override UITableViewCell CellForRowAtIndexPath(CouchbaseTableSource source, NSIndexPath indexPath)
        {
            return null;
        }

        public override void WillUpdateFromQuery(CouchbaseTableSource source, LiveQuery query)
        {
            return;
        }

        public override void UpdateFromQuery(CouchbaseTableSource source, LiveQuery query, QueryRow[] previousRows)
        {
            return;
        }

        public override bool DeleteRow(CouchbaseTableSource source, QueryRow row)
        {
            return false;
        }

        public override void DeleteFailed(CouchbaseTableSource source)
        {
            return;
        }

        #endregion

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
          return 50f;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
          var view = tableView.GetHeaderView (section);
          return view;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
          var row = dataSource.RowAtIndexPath (indexPath);
          var doc = row.Document;

          // Toggle the document's 'checked' property
            var docContent = doc.Properties;
            object checkedVal;
            docContent.TryGetValue (RootViewController.CheckboxPropertyName, out checkedVal);
            var wasChecked = (bool)checkedVal;
            docContent[RootViewController.CheckboxPropertyName] = !wasChecked;

            SavedRevision newRevision = null;

            try
            {
                newRevision = doc.CurrentRevision.CreateRevision(docContent);
            }
            catch (Exception ex)
            {
                if (newRevision == null)
                    parent.ShowErrorAlert ("Failed to update item", ex, false);
            }
        }

        public override void WillUseCell (CouchbaseTableSource source, UITableViewCell cell, QueryRow row)
        {
          if (backgroundColor == null) {
            var image = UIImage.FromBundle ("item_background");
            backgroundColor = UIColor.FromPatternImage (image);
          }
          
          cell.BackgroundColor = backgroundColor;
          cell.SelectionStyle = UITableViewCellSelectionStyle.Gray;

          cell.TextLabel.Font = UIFont.FromName ("Helvetica", 18f);
          cell.TextLabel.BackgroundColor = UIColor.Clear;

            var props = (IDictionary<string, object>)row.Value;
            var isChecked = (bool)props[RootViewController.CheckboxPropertyName];
//          props.TryGetValue (RootViewController.CheckboxPropertyName, out isChecked);
            cell.TextLabel.TextColor = (bool)isChecked ? UIColor.Gray : UIColor.Black;
            cell.ImageView.Image = UIImage.FromBundle ((bool)isChecked 
                ? "list_area___checkbox___checked" 
                : "list_area___checkbox___unchecked");
        }
    }
}
