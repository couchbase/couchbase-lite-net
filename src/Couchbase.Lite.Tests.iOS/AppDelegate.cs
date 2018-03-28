using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Foundation;
using UIKit;

using Xunit.Runner;
using Xunit.Runners.UI;
using Xunit.Sdk;


namespace Couchbase.Lite.Tests.iOS
{
    // The UIApplicationDelegate for the application. This class is responsible for launching the 
    // User Interface of the application, as well as listening (and optionally responding) to 
    // application events from iOS.
    [Register("AppDelegate")]
    public partial class AppDelegate : RunnerAppDelegate
    {

        //
        // This method is invoked when the application has loaded and is ready to run. In this 
        // method you should instantiate the window, load the UI into it and then make the window
        // visible.
        //
        // You have 17 seconds to return from this method, or iOS will terminate your application.
        //
        public override bool FinishedLaunching(UIApplication app, NSDictionary options)
        {
            Couchbase.Lite.Support.iOS.Activate();

            // We need this to ensure the execution assembly is part of the app bundle
            AddExecutionAssembly(typeof(ExtensibilityPointFactory).Assembly);


            // tests can be inside the main assembly
            AddTestAssembly(Assembly.GetExecutingAssembly());
            AutoStart = true;
            TerminateAfterExecution = true;
            using (var str = GetType().Assembly.GetManifestResourceStream("result_ip"))
            using (var sr = new StreamReader(str))
            {
                Writer = new TcpTextWriter(sr.ReadToEnd().TrimEnd(), 12345);
            }

            return base.FinishedLaunching(app, options);
        }
    }
}