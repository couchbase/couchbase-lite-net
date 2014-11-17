using System;
namespace Couchbase.Lite.Portable
{
    public interface IReplication
    {
        Couchbase.Lite.Auth.IAuthenticator Authenticator { get; set; }
        event EventHandler<Couchbase.Lite.Portable.ReplicationChangeEventArgs> Changed;
        int ChangesCount { get; }
        System.Collections.Generic.IEnumerable<string> Channels { get; set; }
        int CompletedChangesCount { get; }
        bool Continuous { get; set; }
        bool CreateTarget { get; set; }
        void DeleteCookie(string name);
        System.Collections.Generic.IEnumerable<string> DocIds { get; set; }
        string Filter { get; set; }
        System.Collections.Generic.IDictionary<string, object> FilterParams { get; set; }
        System.Collections.Generic.IDictionary<string, string> Headers { get; set; }
        bool IsPull { get; }
        bool IsRunning { get; }
        Exception LastError { get; }
        Uri RemoteUrl { get; }
        void Restart();
        void SetCookie(string name, string value, string path, DateTime expirationDate, bool secure, bool httpOnly);
        void Start();
        Couchbase.Lite.Portable.ReplicationStatus Status { get; set; }
        void Stop();
        Couchbase.Lite.Portable.PropertyTransformationDelegate TransformationFunction { get; set; }
    }
}
