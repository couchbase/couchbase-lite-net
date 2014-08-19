// 
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//using System.Collections.Generic;
using Sharpen;

namespace Couchbase.Lite.Support
{
    /// <summary>
    /// A data structure representing a type of array that allows object values to be added to the end, and removed in arbitrary order;
    /// it's used by the replicator to keep track of which revisions have been transferred and what sequences to checkpoint.
    /// </summary>
    /// <remarks>
    /// A data structure representing a type of array that allows object values to be added to the end, and removed in arbitrary order;
    /// it's used by the replicator to keep track of which revisions have been transferred and what sequences to checkpoint.
    /// </remarks>
    public class SequenceMap
    {
        private TreeSet<long> sequences;

        private long lastSequence;

        private IList<string> values;

        private long firstValueSequence;

        public SequenceMap()
        {
            // Sequence numbers currently in the map
            // last generated sequence
            // values of remaining sequences
            // sequence # of first item in _values
            sequences = new TreeSet<long>();
            values = new AList<string>(100);
            firstValueSequence = 1;
            lastSequence = 0;
        }

        /// <summary>Adds a value to the map, assigning it a sequence number and returning it.
        ///     </summary>
        /// <remarks>
        /// Adds a value to the map, assigning it a sequence number and returning it.
        /// Sequence numbers start at 1 and increment from there.
        /// </remarks>
        public virtual long AddValue(string value)
        {
            lock (this)
            {
                sequences.AddItem(++lastSequence);
                values.AddItem(value);
                return lastSequence;
            }
        }

        /// <summary>Removes a sequence and its associated value.</summary>
        /// <remarks>Removes a sequence and its associated value.</remarks>
        public virtual void RemoveSequence(long sequence)
        {
            lock (this)
            {
                sequences.Remove(sequence);
            }
        }

        public virtual bool IsEmpty()
        {
            lock (this)
            {
                return sequences.IsEmpty();
            }
        }

        /// <summary>Returns the maximum consecutively-removed sequence number.</summary>
        /// <remarks>
        /// Returns the maximum consecutively-removed sequence number.
        /// This is one less than the minimum remaining sequence number.
        /// </remarks>
        public virtual long GetCheckpointedSequence()
        {
            lock (this)
            {
                long sequence = lastSequence;
                if (!sequences.IsEmpty())
                {
                    sequence = sequences.First() - 1;
                }
                if (sequence > firstValueSequence)
                {
                    // Garbage-collect inaccessible values:
                    int numToRemove = (int)(sequence - firstValueSequence);
                    for (int i = 0; i < numToRemove; i++)
                    {
                        values.Remove(0);
                    }
                    firstValueSequence += numToRemove;
                }
                return sequence;
            }
        }

        /// <summary>Returns the value associated with the checkpointedSequence.</summary>
        /// <remarks>Returns the value associated with the checkpointedSequence.</remarks>
        public virtual string GetCheckpointedValue()
        {
            lock (this)
            {
                int index = (int)(GetCheckpointedSequence() - firstValueSequence);
                return (index >= 0) ? values[index] : null;
            }
        }
    }
}
