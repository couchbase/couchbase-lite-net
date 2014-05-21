using System;
using System.Collections.Generic;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Linq;
using Couchbase;
using System.Diagnostics;
using MonoTouch.CoreGraphics;
using System.Drawing;
using Couchbase.Lite;

namespace CouchbaseSample
{
  public partial class RootViewController : UIViewController
  {
    private  const string ReplicationChangeNotification = "CBLReplicationChange";
    private  const string DefaultViewName = "byDate";
    private  const string DocumentDisplayPropertyName = "text";
    internal const string CheckboxPropertyName = "check";
    internal static readonly String CreationDatePropertyName = (NSString)"created_at";
    internal static readonly String DeletedKey = (NSString)"_deleted";
    Boolean showingSyncButton;
    Replication pull;
    Replication push;
    int _lastPushCompleted;
    int _lastPullCompleted;
    Replication _leader;

    UIProgressView Progress { get; set; }

    public Database Database { get; set; }

    LiveQuery DoneQuery { get; set; }

    #region Initialization/Configuration
    public RootViewController () : base ("RootViewController", null)
    {
      Title = NSBundle.MainBundle.LocalizedString ("Grocery Sync", "Grocery Sync");
    }

    public ConfigViewController DetailViewController { get; set; }

    public override void ViewDidLoad ()
    {
      base.ViewDidLoad ();

      var addButton = new UIBarButtonItem ("Clean", UIBarButtonItemStyle.Plain, DeleteCheckedItems);
      NavigationItem.RightBarButtonItem = addButton;

      ShowSyncButton ();

      EntryField.ShouldEndEditing += (sender) => { 
        EntryField.ResignFirstResponder (); 
        return true; 
      };

      EntryField.EditingDidEndOnExit += AddNewItem;

      // Custom initialization
      InitializeDatabase ();
      InitializeCouchbaseView ();
      InitializeCouchbaseSummaryView ();
      InitializeDatasource ();

      Datasource.TableView = TableView;
      Datasource.TableView.Delegate = new CouchtableDelegate (this, Datasource);
      TableView.SectionHeaderHeight = 0;

      UIImage backgroundImage = null;
            switch (Convert.ToInt32 (UIScreen.MainScreen.PreferredMode.Size.Height)) {
      case 480:
        backgroundImage = UIImage.FromBundle ("Default");
        break;
      case 960:
        backgroundImage = UIImage.FromBundle ("Default@2x");
        break;
      case 1136:
        backgroundImage = UIImage.FromBundle ("Default-568h@2x");
        break;
      }

      var background = new UIImageView (UIImage.FromImage (backgroundImage.CGImage, UIScreen.MainScreen.Scale, UIImageOrientation.Up)) {
        ContentMode = UIViewContentMode.ScaleAspectFill,
        ContentScaleFactor = UIScreen.MainScreen.Scale
      };
      background.AutosizesSubviews = true;
      background.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
      var newLocation = background.Frame.Location;
      newLocation.Y = -65f;
      background.Frame = new System.Drawing.RectangleF (newLocation, background.Frame.Size);

      // Handle iOS 7 specific code.
      if (AppDelegate.CurrentSystemVersion < AppDelegate.iOS7) {
        TableView.BackgroundColor = UIColor.Clear;
        TableView.BackgroundView = null;
        NavigationController.NavigationBar.TintColor = UIColor.FromRGB (0.564f, 0.0f, 0.015f);
      }

      View.InsertSubviewBelow (background, View.Subviews [0]);
    }

    public override void ViewWillAppear (bool animated)
    {
      base.ViewWillAppear (animated);

      // Check for changes after returning from the sync config view:
      UpdateSyncUrl ();
    }

    void InitializeDatabase ()
    {
        var db = Manager.SharedInstance.GetDatabase ("grocery-sync");
        if (db == null)
            throw new ApplicationException ("Could not create database");

        Database = db;
    }

    void InitializeCouchbaseView ()
    {
        var view = Database.GetView (DefaultViewName);

        var mapBlock = new MapDelegate ((doc, emit) => 
        {
            object date;
            doc.TryGetValue (CreationDatePropertyName, out date);

            object deleted;
            doc.TryGetValue (DeletedKey, out deleted);

            if (date != null && deleted == null)
                emit (date, doc);
        });

        view.SetMap (mapBlock, "1.1");

        var validationBlock = new ValidateDelegate ((revision, context) =>
                {
                    if (revision.IsDeletion)
                        return true;

                    object date;
                    revision.Properties.TryGetValue (CreationDatePropertyName, out date);
                    return (date != null);
                });

        Database.SetValidation(CreationDatePropertyName, validationBlock);
    }

    void InitializeCouchbaseSummaryView ()
    {

        var view = Database.GetView ("Done");

        var mapBlock = new MapDelegate ((doc, emit) => 
                {
                    object date;
                    doc.TryGetValue (CreationDatePropertyName, out date);

                    object checkedOff;
                    doc.TryGetValue ((NSString)"check", out checkedOff);

                    if (date != null) {
                       emit (new[] { checkedOff, date }, null);
                    }
                });

        var reduceBlock = new ReduceDelegate ((keys, values, rereduce) => 
                {
                    var key = keys.Sum(data => 1 - (int)keys.ElementAt(0));

                    var result = new Dictionary<string,string>
                    {
                        {"Items Remaining", "Label"},
                        {key.ToString (), "Count"}
                    };

                    return result;
                });

        view.SetMapReduce (mapBlock, reduceBlock, "1.1");
    }

    void InitializeDatasource ()
    {
            var view = Database.GetView (DefaultViewName);

            var query = view.CreateQuery().ToLiveQuery();
            query.Descending = true;

            Datasource.Query = query;
            Datasource.LabelProperty = DocumentDisplayPropertyName; // Document property to display in the cell label

            DoneQuery = Database.GetView ("Done").CreateQuery().ToLiveQuery();
            DoneQuery.Changed += (sender, e) => {
                    String val;
                    var label = TableView.TableHeaderView as UILabel;

                    if (DoneQuery.Rows.Count == 0) {
                        val = String.Empty;
                    } else {
                        var row = DoneQuery.Rows.ElementAt(0);
                        var doc = row.Value as IDictionary<object,object>;

                        val = String.Format ("{0}: {1}\t", doc["Label"], doc["Count"]);
                    }
                    label.Text = val;
                };
    }
    #endregion
    #region CRUD Operations
    IEnumerable<Document> CheckedDocuments {
      get {

        var docs = new List<Document> ();
            foreach (var row in Datasource.Rows) {
                var doc = row.Document;
                object val;

                if (doc.Properties.TryGetValue (CheckboxPropertyName, out val) && ((bool)val))
                    docs.Add (doc);            
            }
        return docs;
      }
    }

    void AddNewItem (object sender, EventArgs args)
    {
        var value = EntryField.Text;
        if (String.IsNullOrWhiteSpace (value))
            return;

        var jsonDate = DateTime.UtcNow.ToString ("o"); // ISO 8601 date/time format.
        var vals =  new Dictionary<String,Object> {
            {DocumentDisplayPropertyName , value},
            {CheckboxPropertyName , false},
            {CreationDatePropertyName , jsonDate}
        };

        var doc = Database.CreateDocument();
        var result = doc.PutProperties (vals);
        if (result == null)
            throw new ApplicationException ("failed to save a new document");

        var docContent = doc.Properties;
        docContent["check"] = false;

        EntryField.Text = null;
    }

    void DeleteCheckedItems (object sender, EventArgs args)
    {
      var numChecked = CheckedDocuments.Count ();
      if (numChecked == 0)
        return;

      var prompt = String.Format ("Are you sure you want to remove the {0} checked-off item{1}?",
                                  numChecked,
                                  numChecked == 1 ? String.Empty : "s");

      var alert = new UIAlertView ("Remove Completed Items?",
                                   prompt,
                                   null,
                                   "Cancel",
                                   "Remove");

      alert.Dismissed += (alertView, e) => 
        {
            if (e.ButtonIndex == 0)
                return;

            try {
                Datasource.DeleteDocuments (CheckedDocuments);
            } catch (Exception ex) {
                ShowErrorAlert ("Unabled to delete checked documents", ex);
            }
        };
      alert.Show ();
    }
    #endregion
    #region Error Handling
    public void ShowErrorAlert (string errorMessage, Exception error = null, Boolean fatal = false)
    {
      if (error != null)
                errorMessage = String.Format ("{0}\r\n{1}", errorMessage, error.Message);

      var alert = new UIAlertView (fatal ? @"Fatal Error" : @"Error",
                                   errorMessage,
                                   null,
                                   fatal ? null : "Dismiss"
      );
      alert.Show ();
    }
    #endregion
    #region Sync
    void ConfigureSync (object sender, EventArgs args)
    {
      var navController = ParentViewController as UINavigationController;
      var controller = new ConfigViewController ();
      if (AppDelegate.CurrentSystemVersion >= AppDelegate.iOS7) {
        controller.EdgesForExtendedLayout = UIRectEdge.None;
      }
      navController.PushViewController (controller, true);
    }

    void ShowSyncButton ()
    {
      if (!showingSyncButton) {
        showingSyncButton = true;
        var button = new UIBarButtonItem ("Configure", UIBarButtonItemStyle.Plain, ConfigureSync);
        NavigationItem.LeftBarButtonItem = button;
      }
    }

    void UpdateSyncUrl ()
    {
        if (Database == null)
            return;

        Uri newRemoteUrl = null;
        var syncPoint = NSUserDefaults.StandardUserDefaults.StringForKey (ConfigViewController.SyncUrlKey);
        if (!String.IsNullOrWhiteSpace (syncPoint))
            newRemoteUrl = new Uri (syncPoint);
        else
            return;
        ForgetSync ();

        pull = Database.CreatePullReplication (newRemoteUrl);
            push = Database.CreatePushReplication (newRemoteUrl);
        pull.Continuous = push.Continuous = true;
        pull.Changed += ReplicationProgress;
        push.Changed += ReplicationProgress;
        pull.Start();
        push.Start();
    }

    void ReplicationProgress (object replication, ReplicationChangeEventArgs args)
    {
        var active = args.Source;
            Debug.WriteLine (String.Format ("Push: {0}, Pull: {1}", push.Status, pull.Status));

      int lastTotal = 0;

      if (_leader == null) {
        if (active.IsPull && (pull.Status == ReplicationStatus.Active && push.Status != ReplicationStatus.Active)) {
          _leader = pull;
        } else if (!active.IsPull && (push.Status == ReplicationStatus.Active && pull.Status != ReplicationStatus.Active)) {
          _leader = push;
        } else {
          _leader = null;
        }
      } 
      if (active == pull) {
        lastTotal = _lastPullCompleted;
      } else {
        lastTotal = _lastPushCompleted;
      }

      Debug.WriteLine (String.Format ("Sync: {2} Progress: {0}/{1};", active.CompletedChangesCount - lastTotal, active.ChangesCount - lastTotal, active == push ? "Push" : "Pull"));

      var progress = (float)(active.CompletedChangesCount - lastTotal) / (float)(Math.Max (active.ChangesCount - lastTotal, 1));

      if (AppDelegate.CurrentSystemVersion < AppDelegate.iOS7) {
        ShowSyncStatusLegacy ();
      } else {
        ShowSyncStatus ();
      }

      Debug.WriteLine (String.Format ("({0})", progress));

      if (active == pull) {
        if (AppDelegate.CurrentSystemVersion >= AppDelegate.iOS7) Progress.TintColor = UIColor.White;
      } else {
        if (AppDelegate.CurrentSystemVersion >= AppDelegate.iOS7) Progress.TintColor = UIColor.LightGray;
      }

      Progress.Hidden = false;
      
      if (progress < Progress.Progress)
        Progress.SetProgress (progress, false);
      else
        Progress.SetProgress (progress, false);

            if (!(pull.Status != ReplicationStatus.Active && push.Status != ReplicationStatus.Active))
        return;
      if (active == null)
        return;
      var initiatorName = _leader.IsPull ? "Pull" : "Push";

      _lastPushCompleted = push.ChangesCount;
      _lastPullCompleted = pull.ChangesCount;

      if (Progress == null)
        return;
      Progress.Hidden = false;
      Progress.SetProgress (1f, false);

      var t = new System.Timers.Timer (300);
      t.Elapsed += (sender, e) => { 
        InvokeOnMainThread (() => {
          t.Dispose ();
          Progress.Hidden = true;
          Progress.SetProgress (0f, false);
          Debug.WriteLine (String.Format ("{0} Sync Session Finished.", initiatorName));
          ShowSyncButton ();
        });
      };
      t.Start ();

    }

    void ShowSyncStatus ()
    {
      if (showingSyncButton) {
        showingSyncButton = false;
        if (Progress == null) {
          Progress = new UIProgressView (UIProgressViewStyle.Bar);
          var frame = Progress.Frame;
          var size = new SizeF (View.Frame.Size.Width, frame.Height);
          frame.Size = size;
          Progress.Frame = frame;
          Progress.SetProgress (0f, false);
        }
        var progressItem = new UIBarButtonItem (Progress);
        progressItem.Enabled = false;

        View.InsertSubviewAbove (Progress, View.Subviews [0]);
      }
    }

    void ShowSyncStatusLegacy ()
    {
      if (showingSyncButton) {
        showingSyncButton = false;
        if (Progress == null) {
          Progress = new UIProgressView (UIProgressViewStyle.Bar);
          var frame = Progress.Frame;
          var size = new SizeF (View.Frame.Size.Width / 4f, frame.Height);
          frame.Size = size;
          Progress.Frame = frame;
        }
        var progressItem = new UIBarButtonItem (Progress);
        progressItem.Enabled = false;
        NavigationItem.LeftBarButtonItem = progressItem;
      }
    }

    void ForgetSync ()
    {
      var nctr = NSNotificationCenter.DefaultCenter;

      if (pull != null) {
        pull.Changed -= ReplicationProgress;
        pull.Stop();
        pull = null;
      }

      if (push != null) {
        push.Changed -= ReplicationProgress;
        push.Stop();
        push = null;
      }
    }
    #endregion
  }
}
