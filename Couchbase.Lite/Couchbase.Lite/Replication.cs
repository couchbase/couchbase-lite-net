using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class Replication {

    #region Enums
    
    public enum ReplicationStatus {
        Stopped,
        Offline,
        Idle,
        Active
    }
                        
    #endregion
    
    #region Instance Members
        //Properties
        public Database LocalDatabase { get { throw new NotImplementedException(); } }

        public Uri RemoteUrl { get { throw new NotImplementedException(); } }

        public Boolean IsPull { get { throw new NotImplementedException(); } }

        public Boolean CreateTarget { get; set; }

        public Boolean Continuous { get; set; }

        public String Filter { get; set; }

        public Dictionary<String, String> FilterParams { get; set; }

        public IEnumerable<String> Channels { get; set; }

        public IEnumerable<String> DocIds { get; set; }

        public Dictionary<String, String> Headers { get; set; }

        public ReplicationStatus Status { get { throw new NotImplementedException(); } }

        public Boolean IsRunning { get { throw new NotImplementedException(); } }

        public Exception LastError { get { throw new NotImplementedException(); } }

        public int CompletedChangesCount { get { throw new NotImplementedException(); } }

        public int ChangesCount { get { throw new NotImplementedException(); } }

        //Methods
        public void Start() { throw new NotImplementedException(); }

        public void Stop() { throw new NotImplementedException(); }

        public void Retart() { throw new NotImplementedException(); }

        public event EventHandler<ReplicationChangeEventArgs> Change;

    #endregion
    
    #region Delegates
        

    #endregion
    
    #region EventArgs Subclasses
        public class ReplicationChangeEventArgs : EventArgs {

            //Properties
            public Replication Source { get { throw new NotImplementedException(); } }

        }

    #endregion
    
    }

}

