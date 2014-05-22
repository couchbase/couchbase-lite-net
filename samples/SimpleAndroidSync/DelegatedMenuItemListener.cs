// Source : https://github.com/xamarin/google-apis/blob/master/samples/GoogleApis.Android.Sample/DelegatedMenuItemListener.cs


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

namespace SimpleAndroidSync
{
    class DelegatedMenuItemListener : Java.Lang.Object, IMenuItemOnMenuItemClickListener
    {
        public DelegatedMenuItemListener (Func<IMenuItem, bool> handler)
        {
            if (handler == null)
                throw new ArgumentNullException ("handler");

            this.handler = handler;
        }

        public bool OnMenuItemClick (IMenuItem item)
        {
            return this.handler (item);
        }

        private readonly Func<IMenuItem, bool> handler;
    }
}

