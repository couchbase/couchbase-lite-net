using System;
using System.Threading.Tasks;
using System.Threading;
using NUnit.Framework;

namespace Couchbase.Lite
{
    public class StorageEngineTest : LiteTestCase
    {
        [Test]
        [Description("If the delegate returns true, the transaction should be committed.")]
        public void TestRunInTransactionCommits()
        {
            var storageEngine = database.StorageEngine;

            storageEngine.ExecSQL("CREATE TABLE transTest (id INTEGER PRIMARY KEY, whatever INTEGER)");

            database.RunInTransaction(() =>
            {
                storageEngine.ExecSQL("INSERT INTO transTest VALUES (0,1)");
                return true;
            });

            var result = storageEngine.RawQuery("SELECT EXISTS (SELECT 1 FROM transTest WHERE id=0 AND whatever=1)");

            Assert.AreEqual(1, result.GetInt(0));
        }

        [Test]
        [Description("If the delegate returns false, the transaction should be rolledback.")]
        public void TestRunInTransactionRollsback()
        {
            var storageEngine = database.StorageEngine;

            storageEngine.ExecSQL("CREATE TABLE transTest (id INTEGER PRIMARY KEY, whatever INTEGER)");

            database.RunInTransaction(() =>
            {
                storageEngine.ExecSQL("INSERT INTO transTest values (0,1)");
                return false;
            });

            var result = storageEngine.RawQuery("SELECT EXISTS (SELECT 1 FROM transTest WHERE id=0 AND whatever=1)");

            Assert.AreEqual(0, result.GetInt(0));
        }
    }
}
