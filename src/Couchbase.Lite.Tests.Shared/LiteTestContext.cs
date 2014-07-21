using System.IO;

namespace Couchbase.Lite
{
    public class LiteTestContext : IContext
    {
        #region Private

        private DirectoryInfo dir;

        #endregion

        #region Constructors

        public LiteTestContext(DirectoryInfo dir) { 
            this.dir = dir;
        }

        #endregion

        #region IContext implementation

        public DirectoryInfo FilesDir
        {
            get
            {
                return this.dir;
            }
        }

        public INetworkReachabilityManager NetworkReachabilityManager { get; set; }

        #endregion
    }
}