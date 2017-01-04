using System.Reflection;

using Android.App;
using Android.OS;
using Android.NUnitLite.UI;

namespace Couchbase.Lite.Android.Tests
{
    [Activity (Label = "Couchbase.Lite.Android.Tests", MainLauncher = true)]
    public class MainActivity : RunnerActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            // tests can be inside the main assembly
            Add (Assembly.GetExecutingAssembly ());
            // or in any reference assemblies
            // AddTest (typeof (Your.Library.TestClass).Assembly);

            // Once you called base.OnCreate(), you cannot add more assemblies.
            base.OnCreate (bundle);
        }
    }
}

