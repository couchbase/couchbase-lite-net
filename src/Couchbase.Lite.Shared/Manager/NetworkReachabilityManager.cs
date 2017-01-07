using System;
using System.Net.NetworkInformation;
using Couchbase.Lite.Util;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Net.Http;
using System.Text;
using Couchbase.Lite.Support;
using Couchbase.Lite.Internal;

#if __ANDROID__
using Android.App;
using Android.Net;
using Android.Content;
using Android.Webkit;
using Uri = System.Uri;
#endif

#if NET_3_5
using WebRequest = System.Net.Couchbase.WebRequest;
using HttpWebRequest = System.Net.Couchbase.HttpWebRequest;
using HttpWebResponse = System.Net.Couchbase.HttpWebResponse;
using WebException = System.Net.Couchbase.WebException;
#endif

namespace Couchbase.Lite
{

    // NOTE: The Android specific #ifdefs are in here solely
    //       to work around Xamarin.Android bug 
    //       https://bugzilla.xamarin.com/show_bug.cgi?id=1969

    // NOTE: The issue above was fixed as of Xamarin.Android 4.18, but seems
    // to have regressed in 4.20

    /// <summary>
    /// This uses the NetworkAvailability API to listen for network reachability
    /// change events and fires off changes internally.
    /// </summary>
    internal sealed class NetworkReachabilityManager : INetworkReachabilityManager
    {
        private const string TAG = "NetworkReachabilityManager";
        internal static bool AllowLoopback = false; // For unit tests

        public Exception LastError { get; private set; }

        public bool CanReach(RemoteSession session, string remoteUri, TimeSpan timeout)
        {
            CouchbaseLiteHttpClientFactory.SetupSslCallback();
            var uri = new Uri(remoteUri);
            try {
                using(var c = new TcpClient(uri.Host, uri.Port)) {
                    return true;
                }
            } catch(Exception e) {
                Log.To.Sync.I(TAG, "Didn't get successful connection to {0}", remoteUri);
                Log.To.Sync.V(TAG, "   Cause: ", e);
                LastError = e;
                return false;
            }
        }

#if __ANDROID__
        private class AndroidNetworkChangeReceiver : BroadcastReceiver
        {
            const string Tag = "AndroidNetworkChangeReceiver";

            readonly private Action<NetworkReachabilityStatus> _callback;

            object lockObject;

            private volatile Boolean _ignoreNotifications;

            private NetworkReachabilityStatus _lastStatus;

            public AndroidNetworkChangeReceiver(Action<NetworkReachabilityStatus> callback)
            {
                _callback = callback;
                _ignoreNotifications = true;

                lockObject = new object();
            }

            public override void OnReceive(Context context, Intent intent)
            {
                Log.To.Sync.D(Tag + ".OnReceive", "Received intent: {0}", intent.ToString());

                if (_ignoreNotifications) {
                    Log.To.Sync.D(Tag + ".OnReceive", "Ignoring received intent: {0}", intent.ToString());
                    _ignoreNotifications = false;
                    return;
                }

                Log.To.Sync.D(Tag + ".OnReceive", "Received intent: {0}", intent.ToString());

                var manager = (ConnectivityManager)context.GetSystemService(Context.ConnectivityService);
                var networkInfo = manager.ActiveNetworkInfo;
                var status = networkInfo == null || !networkInfo.IsConnected
                    ? NetworkReachabilityStatus.Unreachable
                    : NetworkReachabilityStatus.Reachable;

                if (!status.Equals(_lastStatus))
                {
                    _callback(status);
                }

                lock(lockObject) 
                {
                    _lastStatus = status;
                }
            }

            public void EnableListening()
            {
                _ignoreNotifications = false;
            }

            public void DisableListening()
            {
                _ignoreNotifications = true;
            }
        }

        private AndroidNetworkChangeReceiver _receiver;

#endif

#region INetworkReachabilityManager implementation

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged
        {
            add { _statusChanged = (EventHandler<NetworkReachabilityChangeEventArgs>)Delegate.Combine(_statusChanged, value); }
            remove { _statusChanged = (EventHandler<NetworkReachabilityChangeEventArgs>)Delegate.Remove(_statusChanged, value); }
        }
        private EventHandler<NetworkReachabilityChangeEventArgs> _statusChanged;

        /// <summary>This method starts listening for network connectivity state changes.</summary>
        /// <remarks>This method starts listening for network connectivity state changes.</remarks>
        public void StartListening()
        {
#if __ANDROID__
            if (_receiver != null) {
                return; // We only need one handler.
            }
            var intent = new IntentFilter(ConnectivityManager.ConnectivityAction);
            _receiver = new AndroidNetworkChangeReceiver(InvokeNetworkChangeEvent);
            Application.Context.RegisterReceiver(_receiver, intent);
#else
            if (_isListening) {
                return;
            }
            NetworkChange.NetworkAddressChanged += OnNetworkChange;
            _isListening = true;
#endif
        }

        /// <summary>This method stops this class from listening for network changes.</summary>
        /// <remarks>This method stops this class from listening for network changes.</remarks>
        public void StopListening()
        {
#if __ANDROID__
            if (_receiver == null) {
                return;
            }
            _receiver.DisableListening();
            Application.Context.UnregisterReceiver(_receiver);
            _receiver = null;
#else
            if (!_isListening) {
                return;
            }
            NetworkChange.NetworkAddressChanged -= OnNetworkChange;          
#endif
        }

#endregion

#region Private Members

#if !__ANDROID__
        private volatile Boolean _isListening;
#endif

#endregion

        
        internal void OnNetworkChange(object sender, EventArgs args)
        {
            Log.To.Sync.I(TAG, "Network change detected, analyzing connection status...");
            var status = NetworkReachabilityStatus.Unknown;
                       // https://social.msdn.microsoft.com/Forums/vstudio/en-US/a6b3541b-b7de-49e2-a7a6-ba0687761af5/networkavailabilitychanged-event-does-not-fire
            if(!NetworkInterface.GetIsNetworkAvailable()) {
                Log.To.Sync.I(TAG, "NetworkInterface.GetIsNetworkAvailable() indicated no network available");
                status = NetworkReachabilityStatus.Unreachable;
            } else {
                var firstValidIP = NetworkInterface.GetAllNetworkInterfaces().Where(IsInterfaceValid)
                    .SelectMany(x => x.GetIPProperties().UnicastAddresses)
                    .Select(x => x.Address).FirstOrDefault();

                if(firstValidIP == null) {
                    Log.To.Sync.I(TAG, "No acceptable IP addresses found, signaling network unreachable");
                    status = NetworkReachabilityStatus.Unreachable;
                } else {
                    Log.To.Sync.I(TAG, "At least one acceptable IP address found ({0}), signaling network reachable", new SecureLogString(firstValidIP, LogMessageSensitivity.PotentiallyInsecure));
                    status = NetworkReachabilityStatus.Reachable;
                }
            }
            
            InvokeNetworkChangeEvent(status);
        }

        internal void InvokeNetworkChangeEvent(NetworkReachabilityStatus status)
        {
            var evt = _statusChanged;
            if(evt == null) {
                return;
            }

            var eventArgs = new NetworkReachabilityChangeEventArgs(status);
            evt(this, eventArgs);
        }

        private static bool IsInterfaceValid(NetworkInterface ni)
        {
            Log.To.Sync.V(TAG, "    Testing {0} ({1})...", ni.Name, ni.Description);
            if(ni.OperationalStatus != OperationalStatus.Up) {
                Log.To.Sync.V(TAG, "    NIC invalid (not up)");
                return false;
            }

            if((!AllowLoopback && ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel
                || ni.Description.IndexOf("Loopback", StringComparison.OrdinalIgnoreCase) >= 0) {
                Log.To.Sync.V(TAG, "    NIC invalid (not outward facing)");
                return false;
            }

            if(ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) {
                Log.To.Sync.V(TAG, "    NIC invalid (virtual)");
                return false;
            }

            Log.To.Sync.I(TAG, "Found Acceptable NIC {0} ({1})", ni.Name, ni.Description);
            return true;
        }

    }
}

