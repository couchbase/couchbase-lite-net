using System.IO;

namespace Couchbase.Lite
{
    public interface IContext
    {
        DirectoryInfo FilesDir { get; }

        INetworkReachabilityManager NetworkReachabilityManager { get; set; }
    }
}
