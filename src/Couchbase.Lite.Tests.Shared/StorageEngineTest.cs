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

        [Test]
        [Ignore("This test just attempts to reproduce issue https://github.com/couchbase/couchbase-lite-net/issues/257")]
        [Description("Potentially nested transactions should not affect each others outcome. Unfortunately, they currently do.")]
        public void TestRunInTransactionCommitsThreadSafe()
        {
            var storageEngine = database.StorageEngine;

            storageEngine.ExecSQL("CREATE TABLE transTest (id INTEGER PRIMARY KEY, whatever INTEGER)");

            var syncEvent = new ManualResetEvent(false);

            var firstTransaction = Task.Factory.StartNew(() =>
            {
                database.RunInTransaction(() =>
                {
                    storageEngine.ExecSQL("INSERT INTO transTest VALUES (0,1)");

                    syncEvent.WaitOne();

                    return false;
                });
            });

            var secondTransaction = Task.Factory.StartNew(() =>
            {
                database.RunInTransaction(() =>
                {
                    storageEngine.ExecSQL("INSERT INTO transTest VALUES (1,2)");

                    // before we commit this transaction, we signal the other task
                    // so that the other transaction is suposedly rolledback
                    // then we see how this affects it
                    syncEvent.Set();
                    return true;
                });
            });

            Task.WaitAll(firstTransaction, secondTransaction);

            var firstTransResult = storageEngine.RawQuery("SELECT EXISTS (SELECT 1 FROM transTest WHERE id=0 AND whatever=1)");
            var secondTransResult = storageEngine.RawQuery("SELECT EXISTS (SELECT 1 FROM transTest WHERE id=1 AND whatever=2)");

            // should not fail since the second transaction will 
            // return true right after signalling the other task
            Assert.AreEqual(1, firstTransResult.GetInt(0));

            Assert.AreEqual(1, secondTransResult.GetInt(0));
        }
    }
}
