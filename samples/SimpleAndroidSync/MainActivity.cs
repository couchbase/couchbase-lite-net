using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using CouchbaseSample.Android.Helper;
using Couchbase.Lite;
using CouchbaseSample.Android.Document;
using Java.Util;
using System.Collections.Generic;

namespace SimpleAndroidSync
{
    [Activity (Label = "SimpleAndroidSync", MainLauncher = true)]
    public class MainActivity : Activity
    {
        static readonly string Tag = "SimpleAndroidSync";

        Query Query { get; set; }
        LiveQuery LiveQuery { get; set; }
        Database Database { get; set; }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            Database = Manager.SharedInstance.GetDatabase(Tag.ToLower());

            Query = List.GetQuery(Database);
            LiveQuery = Query.ToLiveQuery();

            var layout = new LinearLayout(this);
            layout.LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, 
                ViewGroup.LayoutParams.MatchParent);
            layout.Orientation = Orientation.Vertical;

            // Add Items
            var newItemText = new EditText(this);
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
            var listView = new ListView(this);
            listView.LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, 
                ViewGroup.LayoutParams.MatchParent);
            listView.Adapter = new ListLiveQueryAdapter(this, LiveQuery);
            layout.AddView(listView);

            SetContentView (layout);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            var addMenu = menu.Add("Config");
            addMenu.SetOnMenuItemClickListener(new DelegatedMenuItemListener(OnConfigClicked));

            return true;
        }

        private bool OnConfigClicked(IMenuItem menuItem)
        {
            //Intent intent = new Intent()
            return true;
        }

        void AddItem (string text)
        {
            var doc = Database.CreateDocument();
            var props = new Dictionary<string, object>
            {
                { "type", "list" },
                { "text", text },
                { "checked", false}
            };
            doc.PutProperties(props);
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

                var document = this[position];

                var text = view.FindViewById<TextView>(Resource.Id.text);
                text.Text = (string)document.GetProperty("text");

                var checkBox = view.FindViewById<CheckBox>(Resource.Id.check);
                var isChecked = (bool)document.GetProperty("checked");
                checkBox.Checked = isChecked;
                checkBox.Click += (object sender, EventArgs e) => 
                {
                    var props = new Dictionary<string, object>(document.Properties);
                    if ((bool)props["checked"] != checkBox.Checked)
                    {
                        props["checked"] = checkBox.Checked;
                        document.PutProperties(props);
                    }
                };

                return view;
            }
        }
    }
}


