
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

using Couchbase.Lite.Util;

namespace Couchbase.Lite.Unity
{
    public class UnityEditorThreadScheduler
    {
        private static readonly BlockingCollection<Task> _jobQueue = new BlockingCollection<Task>();

        public static bool TaskSchedulerReady
        {
            get
            {
                return _taskScheduler != null;
            }
        }

        private static SingleThreadScheduler _taskScheduler;
        public static TaskScheduler TaskScheduler
        {
            get
            {
                if (_taskScheduler == null) {
                    _taskScheduler = new SingleThreadScheduler(Thread.CurrentThread, _jobQueue);
                }

                return _taskScheduler;
            }
        }

        private static TaskFactory _taskFactory;
        public static TaskFactory TaskFactory
        {
            get
            {
                if (_taskFactory != null) {
                    return _taskFactory;
                }

                if (_taskFactory == null && _taskScheduler != null) {
                    _taskFactory = new TaskFactory(_taskScheduler);
                }

                return _taskFactory;
            }
        }

        void Update()
        {
            if (_jobQueue != null && _taskScheduler != null) {
                Task nextTask;
                bool gotTask = _jobQueue.TryTake(out nextTask);
                if (gotTask && nextTask.Status == TaskStatus.WaitingToRun) {
                    _taskScheduler.TryExecuteTaskHack(nextTask);
                }
            }
        }
    }
}