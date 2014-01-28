namespace Sharpen
{
	using System;
	using System.Threading;

	internal class ReentrantLock
	{
        readonly Object lockObject = new Object();

		public void Lock ()
		{
            Monitor.Enter (lockObject);
		}

		public bool TryLock ()
		{
            return Monitor.TryEnter (lockObject);
		}

		public void Unlock ()
		{
            Monitor.Exit (lockObject);
		}
	}
}
