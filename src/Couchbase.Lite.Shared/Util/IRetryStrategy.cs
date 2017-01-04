//
// IRetryStrategy.cs
//
// Author:
//  Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;

namespace Couchbase.Lite.Util
{
    /// <summary>
    /// An interface describing the way in which a replicator will
    /// retry sending messages after receiving transient errors.
    /// Must be able to clone itself and have the clone be independent.
    /// </summary>
    public interface IRetryStrategy
    {
        /// <summary>
        /// Gets the number of attempts left before giving up on the
        /// failed message.
        /// </summary>
        /// <remarks>
        /// Not always utilized, so don't make NextDelay() depend on it.
        /// </remarks>
        int RetriesRemaining { get; }

        /// <summary>
        /// Gets the maximum number of retries that this strategy allows
        /// </summary>
        int MaxRetries { get; }

        /// <summary>
        /// Gets the next delay to use before resending a failed message (useful
        /// in cases where it is not a fixed number e.g. exponential backoff).
        /// </summary>
        /// <param name="increment">Whether or not to count this call as a retry 
        /// (otherwise it will just be a query)</param> 
        /// <returns>The next delay to use before retrying a failed message</returns>
        TimeSpan NextDelay(bool increment);

        /// <summary>
        /// Resets the strategy to its initial state
        /// </summary>
        void Reset();

        /// <summary>
        /// Makes a copy of this retry strategy, set to its initial state
        /// </summary>
        IRetryStrategy Copy();
    }
}

