//
//  MainActivity.cs
//
//  Author:
//      Jed Foss-Alfke  <jed.foss-alfke@couchbase.com>
//
using System;
using Android.App;
using Android.Widget;
using Android.OS;

namespace Listener
{
    [Activity(Label="LiteServ", Name="com.couchbase.liteserv.MainActivity", MainLauncher=true)]
    public class MainActivity : Activity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            ListenerShared.StartListener();
        }
    }
}


