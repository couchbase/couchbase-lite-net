using System;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Lite.Tests.Shared.Util.CouchBaseLite
{
    public class ReplicationHelper
    {
        public int StartSleep { get; set; }
        public int PeriodSleep { get; set; }
        public ReplicationHelper()
        {
            StartSleep = 1000;
            PeriodSleep = 500;
        }

        //Start the replication and waiting until ReplicationStatus is not 
        //active anymore
        //throws ReplicationHelperException
        public void StartAndWaitForReplication(Replication replication)
        {
            replication.Start();
            Sleep(StartSleep);
            while (replication.Status == ReplicationStatus.Active)
            {
                Sleep(PeriodSleep);
            }
            if (replication.LastError != null)
            {
                throw new ReplicationHelperException("Exception during the replication", replication.LastError);
            }
        }

        private static void Sleep(int milliseconds)
        {
            if (milliseconds < 1000)
            {
                Thread.Sleep(milliseconds);
                return;
            }

            while (milliseconds > 0)
            {
                Console.WriteLine("Sleeping...");
                Thread.Sleep(Math.Min(milliseconds, 1000));
                milliseconds -= 1000;
            }
        }
    }
}
