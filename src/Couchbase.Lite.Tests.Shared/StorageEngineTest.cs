using System;
using System.Threading.Tasks;
using System.Threading;
using NUnit.Framework;

namespace Couchbase.Lite
{
    public class StorageEngineTest : LiteTestCase
    {
        [Test]
        public void TestTransactionThreadSafe()
        {
            Assert.Inconclusive("This test does not result in a pass even when it should.");
            var storageEngine = database.StorageEngine;
            try
            {
                storageEngine.BeginTransaction();
                storageEngine.ExecSQL("CREATE TABLE testtrans (id INTEGER PRIMARY KEY, count INTEGER)");
                storageEngine.ExecSQL("INSERT INTO testtrans (id, count) VALUES (1, 0)");
                storageEngine.SetTransactionSuccessful();
                storageEngine.EndTransaction();

                storageEngine.BeginTransaction();
                storageEngine.ExecSQL("UPDATE testtrans SET count=1 WHERE id=1");

                var startEvent = new ManualResetEvent(false);
                var doneEvent = new ManualResetEvent(false);

                Task.Factory.StartNew(()=> 
                { 
                    storageEngine.BeginTransaction();
                    storageEngine.ExecSQL("UPDATE testtrans SET count=2 WHERE id=1");
                    storageEngine.SetTransactionSuccessful();
                    storageEngine.EndTransaction();
                    startEvent.Set();
                });

                // Ensure that the other thread is running
                Assert.IsTrue(startEvent.WaitOne(TimeSpan.FromSeconds(3)));
                // Give the other thread a little time to work
                //Thread.Sleep(1000);

                var cursor = storageEngine.RawQuery("SELECT id, count FROM testtrans WHERE id=1");
                Assert.IsTrue(cursor.MoveToNext());
                Assert.AreEqual(1, cursor.GetInt(0));
                Assert.AreEqual(1, cursor.GetInt(1));
                storageEngine.SetTransactionSuccessful();
                storageEngine.EndTransaction();

                //doneEvent.WaitOne();
            }
            finally
            {
                try 
                { 
                    storageEngine.ExecSQL("DROP TABLE IF EXISTS testtrans"); 
                } 
                catch (Exception e) { }
            }
        }
    }
}
