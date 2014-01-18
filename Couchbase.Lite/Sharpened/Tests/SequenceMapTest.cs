/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

using Couchbase.Lite;
using Couchbase.Lite.Support;
using NUnit.Framework;
using Sharpen;

namespace Couchbase.Lite
{
	public class SequenceMapTest : LiteTestCase
	{
		public virtual void TestSequenceMap()
		{
			SequenceMap map = new SequenceMap();
			NUnit.Framework.Assert.AreEqual(0, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual(null, map.GetCheckpointedValue());
			NUnit.Framework.Assert.IsTrue(map.IsEmpty());
			NUnit.Framework.Assert.AreEqual(1, map.AddValue("one"));
			NUnit.Framework.Assert.AreEqual(0, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual(null, map.GetCheckpointedValue());
			NUnit.Framework.Assert.IsTrue(!map.IsEmpty());
			NUnit.Framework.Assert.AreEqual(2, map.AddValue("two"));
			NUnit.Framework.Assert.AreEqual(0, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual(null, map.GetCheckpointedValue());
			NUnit.Framework.Assert.AreEqual(3, map.AddValue("three"));
			NUnit.Framework.Assert.AreEqual(0, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual(null, map.GetCheckpointedValue());
			map.RemoveSequence(2);
			NUnit.Framework.Assert.AreEqual(0, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual(null, map.GetCheckpointedValue());
			map.RemoveSequence(1);
			NUnit.Framework.Assert.AreEqual(2, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual("two", map.GetCheckpointedValue());
			NUnit.Framework.Assert.AreEqual(4, map.AddValue("four"));
			NUnit.Framework.Assert.AreEqual(2, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual("two", map.GetCheckpointedValue());
			map.RemoveSequence(3);
			NUnit.Framework.Assert.AreEqual(3, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual("three", map.GetCheckpointedValue());
			map.RemoveSequence(4);
			NUnit.Framework.Assert.AreEqual(4, map.GetCheckpointedSequence());
			NUnit.Framework.Assert.AreEqual("four", map.GetCheckpointedValue());
			NUnit.Framework.Assert.IsTrue(map.IsEmpty());
		}
	}
}
