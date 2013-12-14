/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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

using System;
using System.Text;
using Sharpen;

namespace Org.Apache.Http.Message
{
	/// <summary>
	/// This class represents a context of a parsing operation:
	/// <ul>
	/// <li>the current position the parsing operation is expected to start at</li>
	/// <li>the bounds limiting the scope of the parsing operation</li>
	/// </ul>
	/// </summary>
	/// <since>4.0</since>
	public class ParserCursor
	{
		private readonly int lowerBound;

		private readonly int upperBound;

		private int pos;

		public ParserCursor(int lowerBound, int upperBound) : base()
		{
			if (lowerBound < 0)
			{
				throw new IndexOutOfRangeException("Lower bound cannot be negative");
			}
			if (lowerBound > upperBound)
			{
				throw new IndexOutOfRangeException("Lower bound cannot be greater then upper bound"
					);
			}
			this.lowerBound = lowerBound;
			this.upperBound = upperBound;
			this.pos = lowerBound;
		}

		public virtual int GetLowerBound()
		{
			return this.lowerBound;
		}

		public virtual int GetUpperBound()
		{
			return this.upperBound;
		}

		public virtual int GetPos()
		{
			return this.pos;
		}

		public virtual void UpdatePos(int pos)
		{
			if (pos < this.lowerBound)
			{
				throw new IndexOutOfRangeException("pos: " + pos + " < lowerBound: " + this.lowerBound
					);
			}
			if (pos > this.upperBound)
			{
				throw new IndexOutOfRangeException("pos: " + pos + " > upperBound: " + this.upperBound
					);
			}
			this.pos = pos;
		}

		public virtual bool AtEnd()
		{
			return this.pos >= this.upperBound;
		}

		public override string ToString()
		{
			StringBuilder buffer = new StringBuilder();
			buffer.Append('[');
			buffer.Append(Sharpen.Extensions.ToString(this.lowerBound));
			buffer.Append('>');
			buffer.Append(Sharpen.Extensions.ToString(this.pos));
			buffer.Append('>');
			buffer.Append(Sharpen.Extensions.ToString(this.upperBound));
			buffer.Append(']');
			return buffer.ToString();
		}
	}
}
