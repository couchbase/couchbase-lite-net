using System;
using System.Net.NetworkInformation;
using Couchbase.Lite.Util;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Net.Http;

#if __ANDROID__
using Android.App;
using Android.Net;
using Android.Content;
using Android.Webkit;
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
        private int _startCount = 0;
        private const string TAG = "NetworkReachabilityManager";
        internal static bool AllowLoopback = false; // Unit tests are 

        public bool CanReach(string remoteUri)
        {
            HttpWebRequest request = WebRequest.CreateHttp(remoteUri);
            request.AllowWriteStreamBuffering = true;
            request.Timeout = 10000;
            request.Method = "GET";

            try {
                using(var response = (HttpWebResponse)request.GetResponse()) {
                    return true; //We only care that the server responded
                }
            } catch(Exception e) {
                var we = e as WebException;
                if(we != null && we.Status == WebExceptionStatus.ProtocolError) {
                    return true; //Getting an HTTP error technically means we can connect
                }

                Log.I(TAG, "Didn't get successful connection to {0}", remoteUri);
                Log.D(TAG, "   Cause: ", e);
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
                Log.D(Tag + ".OnReceive", "Received intent: {0}", intent.ToString());

                if (_ignoreNotifications) {
                    Log.D(Tag + ".OnReceive", "Ignoring received intent: {0}", intent.ToString());
                    _ignoreNotifications = false;
                    return;
                }

                Log.D(Tag + ".OnReceive", "Received intent: {0}", intent.ToString());

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
            Interlocked.Increment(ref _startCount);

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
            var count = Interlocked.Decrement(ref _startCount);
            if (count > 0) {
                return;
            }

            if (count < 0) {
                Log.W(TAG, "Too many calls to INetworkReachabilityManager.StopListening()");
                Interlocked.Exchange(ref _startCount, 0);
                return;
            }

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
            Log.I(TAG, "Network change detected, analyzing connection status...");
            var status = NetworkReachabilityStatus.Unknown;
            do {
                // https://social.msdn.microsoft.com/Forums/vstudio/en-US/a6b3541b-b7de-49e2-a7a6-ba0687761af5/networkavailabilitychanged-event-does-not-fire
                // plus some tweaks
                if(!NetworkInterface.GetIsNetworkAvailable()) {
                    Log.I(TAG, "NetworkInterface.GetIsNetworkAvailable() indicated no network available");
                    status = NetworkReachabilityStatus.Unreachable;
                    break;
                }

                foreach(NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces()) {
                    // discard because of standard reasons
                    if((ni.OperationalStatus != OperationalStatus.Up) ||
                        (!AllowLoopback && ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) ||
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel)) {
                        Log.V(TAG, "Disregarding {0} ({1} because it is not up or not outward facing", ni.Name, ni.Description);
                        continue;
                    }

                    // discard virtual cards (virtual box, virtual pc, etc.)
                    if((ni.Description.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (ni.Name.IndexOf("virtual", StringComparison.OrdinalIgnoreCase) >= 0)) {
                        Log.V(TAG, "Disregarding virtual NIC {0} ({1}", ni.Name, ni.Description);
                        continue;
                    }

                    // discard "Microsoft Loopback Adapter," etc, it will not show as NetworkInterfaceType.Loopback but as Ethernet Card.
                    if(ni.Description.IndexOf("Loopback Adapter", StringComparison.OrdinalIgnoreCase) >= 0) {
                        Log.V(TAG, "Disregarding Microsoft Loopback Adapter");
                        continue;
                    }

                    Log.I(TAG, "Found acceptable NIC {0} ({1})", ni.Name, ni.Description);
                    status = NetworkReachabilityStatus.Reachable;
                    break;
                }

                if(status == NetworkReachabilityStatus.Reachable) {
                    break;
                }

                Log.I(TAG, "No acceptable NIC found");
                status = NetworkReachabilityStatus.Unreachable;
            } while(false);
            
            InvokeNetworkChangeEvent(status);
        }

        void InvokeNetworkChangeEvent(NetworkReachabilityStatus status)
        {
            var evt = _statusChanged;
            if(evt == null) {
                return;
            }

            var eventArgs = new NetworkReachabilityChangeEventArgs(status);
            evt(this, eventArgs);
        }
    }
}

