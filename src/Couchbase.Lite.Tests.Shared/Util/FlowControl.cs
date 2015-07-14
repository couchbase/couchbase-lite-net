using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Tests
{
    internal abstract class FlowItem {
        private int _currentCount = 0;

        public int ExecutionCount { get; set; }

        public bool Done { 
            get { 
                return ExecutionCount != -1 && _currentCount >= ExecutionCount;
            }
        }

        public object Execute(params object[] args)
        {
            if(!Done)
            {
                Interlocked.Increment(ref _currentCount);
                return ExecuteOverride(args);
            }

            return null;
        }

        protected abstract object ExecuteOverride(object[] args);
    }

    internal class ExceptionThrower : FlowItem
    {
        private readonly Exception E;

        public ExceptionThrower(Exception e)
        {
            E = e;
        }

        protected override object ExecuteOverride(object[] args) 
        {
            throw E;
        }
    }

    internal class ActionRunner : FlowItem
    {
        private readonly Action ACTION;

        public ActionRunner(Action a)
        {
            ACTION = a;
        }

        protected override object ExecuteOverride(object[] args)
        {
            ACTION();
            return null;
        }
    }

    internal class FunctionRunner<T> : FlowItem
    {
        private readonly Func<T> FUNCTION;

        public FunctionRunner(Func<T> f) {
            FUNCTION = f;
        }

        protected override object ExecuteOverride(object[] args)
        {
            return FUNCTION();
        }
    }

    internal class FlowControl
    {
        private readonly IEnumerable<FlowItem> ACTIONS;
        private IEnumerator<FlowItem> _currentItem;

        public FlowControl(IEnumerable<FlowItem> actions) 
        {
            ACTIONS = actions;
            _currentItem = ACTIONS.GetEnumerator();
            _currentItem.MoveNext();
        }

        public T ExecuteNext<T>() 
        {
            if (_currentItem.Current.Done && !_currentItem.MoveNext())
            {
                return default(T);
            }

            return (T)_currentItem.Current.Execute();
        }
    }
}

