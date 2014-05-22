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

            // Add Items
            var newItemBox = new EditText(this);
            newItemBox.KeyPress += (sender, e) => {
                if (e.KeyCode == Keycode.Enter)
                {
                    AddItem(newItemBox.Text);
                }

                // Make sure the event bubbles to the EditText control
                // so that we can pass the complete string to AddItem later.
                e.Handled = false;
            };
            layout.AddView(newItemBox);

            // Create our table
            var listView = new ListView(this);
            listView.Adapter = new LiveQueryAdapter(this, LiveQuery);
            layout.AddView(listView);

            SetContentView (layout);

        }

        void AddItem (string text)
        {
            var doc = Database.CreateDocument();
            var props = new Dictionary<string,object>
            {
                { "type", "list" },
                { "text", text }
            };
            doc.PutProperties(props);
        }
    }
}


