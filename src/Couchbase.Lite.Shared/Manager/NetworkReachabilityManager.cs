using System;
using System.Net.NetworkInformation;

#if __ANDROID__
using Android.App;
using Android.Net;
using Android.Content;
using Android.Webkit;
#endif
namespace Couchbase.Lite
{

    // NOTE: The Android specific #ifdefs are in here solely
    //       to work around Xamarin.Android bug 
    //       https://bugzilla.xamarin.com/show_bug.cgi?id=1969


    /// <summary>
    /// This uses the NetworkAvailability API to listen for network reachability
    /// change events and fires off changes internally.
    /// </summary>
    internal sealed class NetworkReachabilityManager : INetworkReachabilityManager
    {
        #if __ANDROID__
        private class NetworkChangeReceiver : BroadcastReceiver
        {
            readonly private Action<NetworkReachabilityStatus> _callback;

            private volatile Boolean _ignoreNotifications;

            public NetworkChangeReceiver(Action<NetworkReachabilityStatus> callback)
            {
                _callback = callback;
            }

            public override void OnReceive(Context context, Intent intent)
            {
                if (_ignoreNotifications) {
                    return;
                }

                var status = intent.GetBooleanExtra(ConnectivityManager.ExtraNoConnectivity, false)
                    ? NetworkReachabilityStatus.Unreachable
                    : NetworkReachabilityStatus.Reachable;

                _callback(status);
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

        private NetworkChangeReceiver _receiver;

        #endif

        #region INetworkReachabilityManager implementation

        public event EventHandler<NetworkReachabilityChangeEventArgs> StatusChanged;

        /// <summary>This method starts listening for network connectivity state changes.</summary>
        /// <remarks>This method starts listening for network connectivity state changes.</remarks>
        public void StartListening()
        {
            #if __ANDROID__
            if (_receiver != null) {
                return; // We only need one handler.
            }
            var intent = new IntentFilter(ConnectivityManager.ConnectivityAction);
            _receiver = new NetworkChangeReceiver(InvokeNetworkChangeEvent);
            Application.Context.RegisterReceiver(_receiver, intent);
            #else
            if (_isListening) {
                return;
            }
            NetworkChange.NetworkAvailabilityChanged += OnNetworkChange;
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
            NetworkChange.NetworkAvailabilityChanged -= OnNetworkChange;          
            #endif
        }
        #endregion

        #region Private Members

        #if !__ANDROID__
        private volatile Boolean _isListening;
        #endif

        #endregion

        /// <summary>Notify listeners that the network is now reachable/unreachable.</summary>
        internal void OnNetworkChange(Object sender, NetworkAvailabilityEventArgs args)
        {
            var status = args.IsAvailable
                ? NetworkReachabilityStatus.Reachable
                : NetworkReachabilityStatus.Unreachable;
            
            InvokeNetworkChangeEvent(status);
        }

        void InvokeNetworkChangeEvent(NetworkReachabilityStatus status)
        {
            var evt = StatusChanged;
            if (evt == null)
            {
                return;
            }
            var eventArgs = new NetworkReachabilityChangeEventArgs(status);
            evt(this, eventArgs);
        }
    }
}

