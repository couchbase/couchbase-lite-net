using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Couchbase.Lite.DI;
using Java.Lang;

namespace Couchbase.Lite.Support.Android.Support
{
    public sealed class XamarinAndroidProxy : IProxy
    {
        public IWebProxy CreateProxy(Uri destination)
        {
            // if a proxy is enabled set it up here
            string host = JavaSystem.GetProperty("http.proxyHost")?.TrimEnd('/');
            string port = JavaSystem.GetProperty("http.proxyPort");

            if (host == null)
                return null;

            //proxy auth
            //ICredentials credentials = new NetworkCredential("username", "password");
            //WebProxy proxy = new WebProxy(new Uri(host+':'+port), true, null, credentials);
            
            return new WebProxy(host, Int32.Parse(port));
        }
    }
}