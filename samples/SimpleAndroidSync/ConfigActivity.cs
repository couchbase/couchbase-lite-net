
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Preferences;

namespace SimpleAndroidSync
{
    [Activity(Label = "ConfigActivity")]            
    public class ConfigActivity : Activity
    {
        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            SetContentView(Resource.Layout.Config);

            var preferences = PreferenceManager.GetDefaultSharedPreferences(this);

            var urlText = FindViewById<EditText>(Resource.Id.url);
            urlText.Text = preferences.GetString("sync-gateway-url", null);
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            var doneMenu = menu.Add("Done");
            doneMenu.SetShowAsAction(ShowAsAction.Always);
            doneMenu.SetOnMenuItemClickListener(new DelegatedMenuItemListener(OnDoneClicked));

            return true;
        }

        private bool OnDoneClicked(IMenuItem menuItem)
        {
            var urlText = FindViewById<EditText>(Resource.Id.url);
            SaveSyncGatewayUrl(urlText.Text);
            return true;
        }

        public void SaveSyncGatewayUrl(String url) {
            var preferences = PreferenceManager.GetDefaultSharedPreferences(this);
            if (!String.IsNullOrEmpty(url)) {
                preferences.Edit().PutString("sync-gateway-url", url).Apply();
            } else {
                preferences.Edit().Remove("sync-gateway-url").Apply();
            }
            Finish();
        }
    }
}

