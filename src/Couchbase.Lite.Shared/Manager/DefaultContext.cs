using System;
using System.IO;

namespace Couchbase.Lite
{
    public class DefaultContext : IContext
    {

        #region Constructors

        public DefaultContext() { }

        #endregion

        #region IContext implementation

        public DirectoryInfo FilesDir
        {
            get
            {
                return new DirectoryInfo(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData));
            }
        }

        public INetworkReachabilityManager NetworkReachabilityManager { get; set; }

        #endregion
    }
}
