using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class Manager {

    #region Static Members
        //Properties
        public static Manager SharedInstance { get { throw new NotImplementedException(); } }

        //Methods
        public static Boolean IsValidDatabaseName(String name) { throw new NotImplementedException(); }

    #endregion
    
    #region Instance Members
        //Properties
        public String Directory { get { throw new NotImplementedException(); } }

        public IEnumerable<String> AllDatabaseNames { get { throw new NotImplementedException(); } }

        //Methods
        public void Close() { throw new NotImplementedException(); }

        public Database GetDatabase(String name) { throw new NotImplementedException(); }

        public Database GetExistingDatabase(String name) { throw new NotImplementedException(); }

        public Boolean ReplaceDatabase(String name, FileInfo databaseFile, DirectoryInfo attachmentsDirectory) { throw new NotImplementedException(); }

    #endregion
    
        #region Non-public Members
        public static ObjectWriter GetObjectMapper ()
        {
            throw new NotImplementedException ();
        }
        #endregion
    }

}

