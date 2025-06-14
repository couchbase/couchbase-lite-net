using LiteCore.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text;

namespace LiteCore.Interop
{
    /// <summary>
    /// Called when a client connects, after the TLS handshake (if any), when the initial HTTP request is received.
    /// </summary>
    /// <param name="c4Listener">The C4Listener handling the connection.</param>
    /// <param name="authHeader">The "Authorization" header value from the client's HTTP request, or null.</param>
    /// <param name="context">The `callbackContext` from the listener config.</param>
    /// <returns>True to allow the connection, false to refuse it.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate bool C4ListenerHTTPAuthCallback(C4Listener* c4Listener,
                                               FLSlice authHeader,
                                               void* context);

    /// <summary>
    /// Called when a client connects, during the TLS handshake, if a client certificate is received.
    /// </summary>
    /// <param name="c4Listener">The C4Listener handling the connection.</param>
    /// <param name="clientCertData">The client's X.509 certificate in DER encoding.</param>
    /// <param name="context">The `tlsCallbackContext` from the `C4TLSConfig`.</param>
    /// <returns>True to allow the connection, false to refuse it.</returns>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal unsafe delegate bool C4ListenerCertAuthCallback(C4Listener* c4Listener,
                                               FLSlice clientCertData,
                                               void* context);


    [ExcludeFromCodeCoverage]
    internal sealed unsafe class TLSConfig : IDisposable
    {
        #region Variables

        private C4TLSConfig _c4TLSConfig;
        private C4ListenerCertAuthCallback? _onCertAuthCallback;

        #endregion

        #region Properties

        /// <summary>
        /// TLS configuration for C4Listener.
        /// </summary>
        public C4TLSConfig C4TLSConfig => _c4TLSConfig;

        /// <summary>
        /// The `tlsCallbackContext` from the `C4TLSConfig`.
        /// </summary>
        public unsafe object? Context
        {
            get => GCHandle.FromIntPtr((IntPtr) _c4TLSConfig.tlsCallbackContext).Target;
            set {
                if (_c4TLSConfig.tlsCallbackContext != null) {
                    GCHandle.FromIntPtr((IntPtr) _c4TLSConfig.tlsCallbackContext).Free();
                }

                if (value != null) {
                    _c4TLSConfig.tlsCallbackContext = GCHandle.ToIntPtr(GCHandle.Alloc(value)).ToPointer();
                }
            }
        }

        /// <summary>
        /// Callback for X.509 cert auth
        /// Called when a client connects, during the TLS handshake, if a client certificate is received.
        /// </summary>
        public C4ListenerCertAuthCallback? OnCertAuthCallback
        {
            get => _onCertAuthCallback;
            set {
                _onCertAuthCallback = value;
                _c4TLSConfig.certAuthCallback = value != null ? Marshal.GetFunctionPointerForDelegate(value) : IntPtr.Zero;
            }
        }

        /// <summary>
        /// Interpretation of `privateKey`
        /// </summary>
        public C4PrivateKeyRepresentation PrivateKeyRepresentation
        {
            get => _c4TLSConfig.privateKeyRepresentation;
            set => _c4TLSConfig.privateKeyRepresentation = value;
        }

        /// <summary>
        /// A key pair that contains the private key
        /// </summary>
        public C4KeyPair* Key
        {
            get => _c4TLSConfig.key;
            set => _c4TLSConfig.key = value;
        }

        /// <summary>
        /// X.509 certificate data
        /// </summary>
        public C4Cert* Certificate
        {
            get => _c4TLSConfig.certificate;
            set => _c4TLSConfig.certificate = value;
        }

        /// <summary>
        /// Root CA certs to trust when verifying client cert
        /// </summary>
        public C4Cert* RootClientCerts
        {
            get => _c4TLSConfig.rootClientCerts;
            set => _c4TLSConfig.rootClientCerts = value;
        }

        /// <summary>
        /// True to require clients to authenticate with a cert
        /// </summary>
        public bool RequireClientCerts
        {
            get => _c4TLSConfig.requireClientCerts;
            set => _c4TLSConfig.requireClientCerts = value;
        }

        #endregion

        #region Constructors

        public TLSConfig()
        {
        }

        ~TLSConfig()
        {
            Dispose(true);
        }

        #endregion

        #region Private Methods

        private unsafe void Dispose(bool finalizing)
        {
            Context = null;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    [ExcludeFromCodeCoverage]
    internal sealed unsafe class ListenerConfig : IDisposable
    {
        #region Variables

        private C4ListenerConfig _c4ListenerConfig;
        private C4ListenerHTTPAuthCallback? _onHTTPAuthCallback;
        private C4String _networkInterface;
        private C4String _serverName;
        private C4String _serverVersion;

        #endregion

        #region Properties

        /// <summary>
        /// Configuration for a C4Listener.
        /// </summary>
        public C4ListenerConfig C4ListenerConfig => _c4ListenerConfig;

        public object? Context
        {
            get => GCHandle.FromIntPtr((IntPtr) _c4ListenerConfig.callbackContext).Target;
            set {
                if (_c4ListenerConfig.callbackContext != null) {
                    GCHandle.FromIntPtr((IntPtr) _c4ListenerConfig.callbackContext).Free();
                }

                if (value != null) {
                    _c4ListenerConfig.callbackContext = GCHandle.ToIntPtr(GCHandle.Alloc(value)).ToPointer();
                }
            }
        }

        public C4ListenerHTTPAuthCallback? OnHTTPAuthCallback
        {
            get => _onHTTPAuthCallback;
            set {
                _onHTTPAuthCallback = value;
                _c4ListenerConfig.httpAuthCallback = value != null ? Marshal.GetFunctionPointerForDelegate(value) : IntPtr.Zero;
            }
        }

        /// <summary>
        /// TCP port to listen on
        /// </summary>
        public ushort Port
        {
            get => _c4ListenerConfig.port;
            set => _c4ListenerConfig.port = value;
        }

        /// <summary>
        /// name or address of interface to listen on; else all
        /// </summary>
        public string? NetworkInterface
        {
            get => _c4ListenerConfig.networkInterface.CreateString();
            set {
                _networkInterface.Dispose();
                _networkInterface = new C4String(value);
                _c4ListenerConfig.networkInterface = _networkInterface.AsFLSlice();
            }
        }

        /// <summary>
        /// TLS configuration, or NULL for no TLS
        /// </summary>
        public TLSConfig? TlsConfig { get; set; }

        /// <summary>
        /// The optional name of the server to return from requests
        /// </summary>
        public string? ServerName
        {
            get => _c4ListenerConfig.serverName.CreateString();
            set {
                _serverName.Dispose();
                _serverName = new C4String(value);
                _c4ListenerConfig.serverName = _serverName.AsFLSlice();
            }
        }

        /// <summary>
        /// The optional version of the server to return from requests
        /// </summary>
        public string? ServerVersion
        {
            get => _c4ListenerConfig.serverName.CreateString();
            set {
                _serverVersion.Dispose();
                _serverVersion = new C4String(value);
                _c4ListenerConfig.serverName = _serverVersion.AsFLSlice();
            }
        }

        #region For sync listeners only:


        public bool AllowPush
        {
            get => _c4ListenerConfig.allowPush;
            set => _c4ListenerConfig.allowPush = value;
        }

        public bool AllowPull
        {
            get => _c4ListenerConfig.allowPull;
            set => _c4ListenerConfig.allowPull = value;
        }

        public bool EnableDeltaSync
        {
            get => _c4ListenerConfig.enableDeltaSync;
            set => _c4ListenerConfig.enableDeltaSync = value;
        }

        #endregion

        #endregion

        #region Constructors

        ~ListenerConfig()
        {
            Dispose(true);
        }

        #endregion

        #region Private Methods

        private void Dispose(bool finalizing)
        {
            Context = null;
            _networkInterface.Dispose();
            _serverName.Dispose();
            _serverVersion.Dispose();
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion

    }
}
