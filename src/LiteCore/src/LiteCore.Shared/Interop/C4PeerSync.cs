using LiteCore.Util;
using System;
using System.Runtime.InteropServices;

namespace LiteCore.Interop;

/** Callback that notifies that C4PeerSync has started, failed to start, or stopped. */
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void C4PeerSync_StatusCallback(C4PeerSync* peer,   ///< Sender
                                          bool started,                     ///< Whether it's running or not
                                          C4Error* err,                     ///< Error, if any
                                          void* context);

/** Callback that notifies that a peer has been discovered, or is no longer visible. */
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void C4PeerSync_DiscoveryCallback(C4PeerSync* peer,    ///< Sender
                                             C4PeerID* peerID,                  ///< Peer's ID
                                             bool online,                       ///< Is peer online?
                                             void* context);

/** Callback that authenticates a peer based on its X.509 certificate.
    This is not called when a peer is discovered, only when making a direct connection.
    It should return `true` to allow the connection, `false` to prevent it. */
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate bool C4PeerSync_AuthenticatorCallback(C4PeerSync* peer,    ///< Sender
                                                 C4PeerID* peerID,                  ///< Peer's ID
                                                 C4Cert* peerCert,                  ///< Peer's X.509 certificate
                                                 void* context);

/** Callback that notifies the status of an individual replication with one peer.
    @note This is similar to \ref C4ReplicatorStatusChangedCallback, but adds the peer's ID
          and indicates whether I connected to the peer or vice versa (just in case you care.) */
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void C4PeerSync_ReplicatorCallback(C4PeerSync* peer,   ///< Sender
                                              C4PeerID* peerID,                 ///< Peer's ID
                                              bool outgoing,                    ///< True if I opened the socket
                                              C4ReplicatorStatus* status,       ///< Status/progress
                                              void* context);

/** Callback that notifies that documents have been pushed or pulled.
    @note This is similar to \ref C4ReplicatorDocumentsEndedCallback, but adds the peer's ID. */
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void C4PeerSync_DocsCallback(C4PeerSync* peer, ///< Sender
                                        C4PeerID* peerID,               ///< Peer ID
                                        bool pushing,                   ///< Direction of sync
                                        UIntPtr numDocs,                ///< Size of docs[]
                                        C4DocumentEnded* docs,          ///< Document info
                                        void* context);

/** Callback that notifies about progress pushing or pulling a single blob.
    @note This is similar to \ref C4ReplicatorBlobProgressCallback, but adds the peer's ID. */
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate void C4PeerSync_BlobCallback(C4PeerSync* peer, ///< Sender
                                        C4PeerID* peerID,               ///< Peer ID
                                        bool pushing,                   ///< Direction of transfer
                                        C4BlobProgress* progress,       ///< Progress info
                                        void* context);

/** Replicator document validation / filtering callback.
    @note This is similar to \ref C4ReplicatorValidationFunction, but adds the peer's ID. */
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal unsafe delegate bool C4PeerSync_ValidationFunction(C4PeerSync* sender, ///< Sender
                                              C4PeerID* peerID,                 ///< Peer's ID
                                              C4CollectionSpec spec,            ///< Collection
                                              C4String docID,                   ///< Document ID
                                              C4String revID,                   ///< Revision ID
                                              C4RevisionFlags flags,            ///< Revision flags
                                              FLDict body,                      ///< Document body
                                              void* context);