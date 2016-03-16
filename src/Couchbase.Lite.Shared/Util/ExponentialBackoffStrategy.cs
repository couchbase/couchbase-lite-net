using System;

namespace Couchbase.Lite.Util
{
    internal sealed class ExponentialBackoffStrategy : IRetryStrategy
    {
        
        #region Constants

        private static readonly string Tag = typeof(ExponentialBackoffStrategy).Name;

        #endregion

        #region Variables

        private readonly int _maxTries;
        private int _tries;
        private TimeSpan _currentDelay = TimeSpan.FromSeconds(2); // Wil be multiplied by 2 prior to use, so actually 4

        #endregion

        #region Properties

        public int RetriesRemaining { get { return _maxTries - _tries; } }

        public int MaxRetries
        {
            get { return 2; }
        }

        #endregion

        #region Constructors

        public ExponentialBackoffStrategy(int maxTries)
        {
            if (maxTries <= 0) {
                Log.To.Database.E(Tag, "maxTries <= 0 in ctor, throwing...");
                throw new ArgumentOutOfRangeException("maxTries", maxTries, "Max tries must be at least 1");
            }

            _maxTries = maxTries;
            _tries = 0;
        }

        #endregion

        #region IRetryStrategy

        public TimeSpan NextDelay(bool increment)
        {
            if (increment) {
                _currentDelay = _currentDelay.Add(_currentDelay);
                _tries++;
            }

            return _currentDelay;
        }

        public void Reset()
        {
            _tries = 0;
            _currentDelay = TimeSpan.FromSeconds(2);
        }

        public IRetryStrategy Copy()
        {
            return new ExponentialBackoffStrategy(_maxTries);
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return String.Format("ExponentialBackoffStrategy[StartTime={0} MaxRetries={1}]", TimeSpan.FromSeconds(4), _maxTries);
        }

        #endregion

    }
}
