using System;
using System.Collections.Generic;

using Android.App;
using Android.Content;
using Android.Net.Wifi;
using Android.OS;
using Android.Preferences;
using Android.Util;
using Android.Views;
using Android.Widget;
using Couchbase.Lite;
using CouchbaseSample.Android.Document;
using CouchbaseSample.Android.Helper;
using System.Linq;

namespace SimpleAndroidSync
{
    [Activity (Label = "SimpleAndroidSync", MainLauncher = true)]
    public class MainActivity : Activity
    {
        static readonly string Tag = "SimpleAndroidSync";

        Query Query { get; set; }
        LiveQuery LiveQuery { get; set; }
        Database Database { get; set; }
        Replication Pull { get; set; }
        Replication Push { get; set; }

        private ListView listView;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            RequestWindowFeature(WindowFeatures.IndeterminateProgress);

            var opts = new DatabaseOptions();
            opts.Create = true;
            opts.StorageType = DatabaseOptions.FORESTDB_STORAGE;
            Database = Manager.SharedInstance.OpenDatabase(Tag.ToLower(), opts);

            Query = List.GetQuery(Database);
            Query.Completed += (sender, e) => 
                Log.Verbose("MainActivity", e.ErrorInfo.ToString() ?? e.Rows.ToString());
            LiveQuery = Query.ToLiveQuery();

            var layout = new LinearLayout(this);
            layout.LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, 
                ViewGroup.LayoutParams.MatchParent);
            layout.Orientation = Orientation.Vertical;

            // Allow user to add Items.
            var newItemText = new EditText(this)
            {   // Ensure we get a 'done' key instead of carriage return.
                InputType = Android.Text.InputTypes.ClassText
            };
            newItemText.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, 
                ViewGroup.LayoutParams.WrapContent);

            newItemText.KeyPress += (sender, e) => 
            {
                e.Handled = false;
                if (e.KeyCode == Keycode.Enter) {
                    if (e.Event.Action == KeyEventActions.Down 
                        && newItemText.Text.Length > 0) {
                        AddItem(newItemText.Text);
                    }
                    newItemText.Text = "";
                }
                e.Handled = false;
            };
            layout.AddView(newItemText);

            // Create our table
            listView = new ListView(this);
            listView.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, 
                ViewGroup.LayoutParams.MatchParent);
            listView.Adapter = new ListLiveQueryAdapter(this, LiveQuery);
            layout.AddView(listView);

            SetContentView (layout);
        }

        protected override void OnResume()
        {
            base.OnResume(); // Always call the superclass first.

            UpdateSync();
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            var offlineMenu = menu.Add("Toggle Wifi");
            offlineMenu.SetShowAsAction(ShowAsAction.Always);
            offlineMenu.SetOnMenuItemClickListener(new DelegatedMenuItemListener(
            (item)=>
            {
                var preferences = PreferenceManager.GetDefaultSharedPreferences(this);
                var syncUrl = preferences.GetString("sync-gateway-url", null);
                
                if (!String.IsNullOrWhiteSpace(syncUrl))
                {
                    var mgr = Application.Context.GetSystemService(Application.WifiService) as WifiManager;
                    var setEnabled = !mgr.IsWifiEnabled;
                    mgr.SetWifiEnabled(setEnabled);
                    item.SetTitle(!setEnabled ? "Enable Wifi" : "Disable Wifi");
                }
            
                return true;
            }));

            var addMenu = menu.Add("Config");
            addMenu.SetShowAsAction(ShowAsAction.Always);
            addMenu.SetOnMenuItemClickListener(new DelegatedMenuItemListener(OnConfigClicked));

            var cleanMenu = menu.Add("Clean");
            cleanMenu.SetShowAsAction(ShowAsAction.Always);
            cleanMenu.SetOnMenuItemClickListener(new DelegatedMenuItemListener(OnCleanClicked));

            return true;
        }

        private bool OnConfigClicked(IMenuItem menuItem)
        {
            var activity = new Intent (this, typeof(ConfigActivity));
            StartActivity(activity);
            return true;
        }

        private bool OnCleanClicked(IMenuItem menuItem)
        {
            ((ListLiveQueryAdapter)listView.Adapter).Clean();
            return true;
        }

        private void AddItem(string text)
        {
            var jsonDate = DateTime.UtcNow.ToString ("o"); // ISO 8601 date/time format.
            var doc = Database.CreateDocument();
            var props = new Dictionary<string, object>
            {
                { "created_at", jsonDate },
                { "type", "list" },
                { "text", text },
                { "checked", false}
            };
            doc.PutProperties(props);
        }

        private void UpdateSync()
        {
            if (Database == null)
                return;

            var preferences = PreferenceManager.GetDefaultSharedPreferences(this);
            var syncUrl = preferences.GetString("sync-gateway-url", null);

            ForgetSync ();

            if (!String.IsNullOrEmpty(syncUrl))
            {
                try 
                {
                    var uri = new System.Uri(syncUrl);
                    Pull = Database.CreatePullReplication(uri);
                    Pull.Continuous = true;
                    Pull.Changed += ReplicationChanged;

                    Push = Database.CreatePushReplication(uri);
                    Push.Continuous = true;
                    Push.Changed += ReplicationChanged;

                    Pull.Start();
                    Push.Start();
                } 
                catch (Java.Lang.Throwable th)
                {
                    Log.Debug(Tag, th, "UpdateSync Error");
                }
            }
        }

        private void ForgetSync()
        {
            if (Pull != null) {
                Pull.Changed -= ReplicationChanged;
                Pull.Stop();
                Pull = null;
            }

            if (Push != null) {
                Push.Changed -= ReplicationChanged;
                Push.Stop();
                Push = null;
            }
        }

        public void ReplicationChanged(object sender, ReplicationChangeEventArgs args)
        {
            Couchbase.Lite.Util.Log.D(Tag, "Replication Changed: {0}", args);

            var replicator = args.Source;

            var totalCount = replicator.ChangesCount;
            var completedCount = replicator.CompletedChangesCount;

            if (totalCount > 0 && completedCount < totalCount) {
                SetProgressBarIndeterminateVisibility(true);
            } else {
                SetProgressBarIndeterminateVisibility(false);
            }
        }

        private class ListLiveQueryAdapter : LiveQueryAdapter
        {
            public ListLiveQueryAdapter(Context context, LiveQuery query) 
                : base(context, query) { }

            public override Android.Views.View GetView(int position,
                Android.Views.View convertView, ViewGroup parent)
            {
                var view = convertView;
                if (view == null)
                {
                    view = ((Activity)Context).LayoutInflater.Inflate(
                        Resource.Layout.ListItemView, null);
                }

                var queryRow = this[position];

                var text = view.FindViewById<TextView>(Resource.Id.text);
                text.Text = (string)queryRow.Key;

                var checkBox = view.FindViewById<ContextCheckBox>(Resource.Id.check);
                checkBox.DataContext = queryRow;

                var isChecked = (bool)queryRow.Value;
                checkBox.Click -= OnClick;
                checkBox.Checked = isChecked;
                checkBox.Click += OnClick;

                return view;
            }

            private void OnClick(object sender, EventArgs e)
            {
                var checkBox = sender as ContextCheckBox;
                var dataSource = checkBox.DataContext as QueryRow;
                if ((bool)dataSource.Value != checkBox.Checked)
                {
                    var props = dataSource.Document.Properties;
                    props["checked"] = checkBox.Checked;
                    dataSource.Document.PutProperties(props);
                }
            }

            public void Clean()
            {
                var checkedRows = base.enumerator.Where(row => row.ValueAs<bool>()).Select(row => row.Document);
                foreach (var checkedRow in checkedRows) {
                    checkedRow.Delete();
                }
            }
        }
    }
}


