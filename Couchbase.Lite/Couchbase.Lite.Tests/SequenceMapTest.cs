//
// SequenceMapTest.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
