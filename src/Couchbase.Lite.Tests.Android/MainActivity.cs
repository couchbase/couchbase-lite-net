using System;
using System.IO;
using System.Reflection;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

using Xunit.Sdk;
using Xunit.Runners.UI;

namespace Couchbase.Lite.Tests.Android
{
    [Activity(Label = "CBLUnit", MainLauncher = true, Theme = "@android:style/Theme.Material.Light", Name ="test.activity")]
    public class MainActivity : RunnerActivity
    {
        public static Context ActivityContext { get; private set; }

        protected override void OnCreate(Bundle bundle)
        {
            ActivityContext = ApplicationContext;
            Couchbase.Lite.Support.Droid.Activate(ApplicationContext);

            // tests can be inside the main assembly
            AddTestAssembly(Assembly.GetExecutingAssembly());

            AddExecutionAssembly(typeof(ExtensibilityPointFactory).Assembly);
            AutoStart = true;
            TerminateAfterExecution = true;
            using (var str = GetType().Assembly.GetManifestResourceStream("result_ip"))
            using (var sr = new StreamReader(str)) {
                Writer = new TcpTextWriter(sr.ReadToEnd().TrimEnd(), 12345);
            }

            // you cannot add more assemblies once calling base
            base.OnCreate(bundle);
        }
    }
}

