using Couchbase.Lite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Todo.WPF
{
    internal sealed class SimpleModel
    {
        private readonly Database _db;

        public string SyncURL;

        public SimpleModel()
        {

        }

        public SimpleModel(Manager manager, string dbName)
        {
            _db = manager.GetDatabase(dbName);
        }

        public void LoadValues()
        {
            if (_db == null)
            {
                return;
            }

            var localDoc = _db.GetExistingLocalDocument("sync_url");
            if (localDoc != null)
            {
                SyncURL = localDoc["value"] as string;
            }
        }

        public void SaveValues()
        {
            if (_db == null)
            {
                return;
            }

            _db.PutLocalDocument("sync_url", new Dictionary<string, object> { { "value", SyncURL }});
        }
    }
}
