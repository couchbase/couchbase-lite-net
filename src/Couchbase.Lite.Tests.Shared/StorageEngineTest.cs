using System;
using System.Threading.Tasks;
using System.Threading;
using NUnit.Framework;
using Couchbase.Lite.Store;

namespace Couchbase.Lite
{
    public class StorageEngineTest : LiteTestCase
    {

        public StorageEngineTest(string storageType) : base(storageType) {}

        [Test]
        [Description("If the delegate returns true, the transaction should be committed.")]
        public void TestRunInTransactionCommits()
        {
            /*var sqliteStorage = database.Storage as SqliteCouchStore;
            if (sqliteStorage == null) {
                Assert.Inconclusive("This test is only valid on a SQLite store");
            }

            var storageEngine = sqliteStorage.StorageEngine;

            storageEngine.ExecSQL("CREATE TABLE transTest (id INTEGER PRIMARY KEY, whatever INTEGER)");

            database.RunInTransaction(() =>
            {
                storageEngine.ExecSQL("INSERT INTO transTest VALUES (0,1)");
                return true;
            });

            var result = storageEngine.RawQuery("SELECT EXISTS (SELECT 1 FROM transTest WHERE id=0 AND whatever=1)");
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.GetInt(0));*/
            Assert.Fail();
        }

        [Test]
        [Description("If the delegate returns false, the transaction should be rolledback.")]
        public void TestRunInTransactionRollsback()
        {
            /*var sqliteStorage = database.Storage as SqliteCouchStore;
            if (sqliteStorage == null) {
                Assert.Inconclusive("This test is only valid on a SQLite store");
            }

            var storageEngine = sqliteStorage.StorageEngine;

            storageEngine.ExecSQL("CREATE TABLE transTest (id INTEGER PRIMARY KEY, whatever INTEGER)");

            database.RunInTransaction(() =>
            {
                storageEngine.ExecSQL("INSERT INTO transTest values (0,1)");
                return false;
            });

            var result = storageEngine.RawQuery("SELECT EXISTS (SELECT 1 FROM transTest WHERE id=0 AND whatever=1)");

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.GetInt(0));*/
            Assert.Fail();
        }
    }
}
