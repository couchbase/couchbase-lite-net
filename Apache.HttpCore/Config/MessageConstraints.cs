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
using Org.Apache.Http.Config;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Config
{
	/// <summary>HTTP Message constraints: line length and header count.</summary>
	/// <remarks>HTTP Message constraints: line length and header count.</remarks>
	/// <since>4.3</since>
	public class MessageConstraints : ICloneable
	{
		public static readonly Org.Apache.Http.Config.MessageConstraints Default = new MessageConstraints.Builder
			().Build();

		private readonly int maxLineLength;

		private readonly int maxHeaderCount;

		internal MessageConstraints(int maxLineLength, int maxHeaderCount) : base()
		{
			this.maxLineLength = maxLineLength;
			this.maxHeaderCount = maxHeaderCount;
		}

		public virtual int GetMaxLineLength()
		{
			return maxLineLength;
		}

		public virtual int GetMaxHeaderCount()
		{
			return maxHeaderCount;
		}

        #region ICloneable implementation

        object ICloneable.Clone ()
        {
            return MemberwiseClone();
        }

        #endregion


		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("[maxLineLength=").Append(maxLineLength).Append(", maxHeaderCount="
				).Append(maxHeaderCount).Append("]");
			return builder.ToString();
		}

		public static Org.Apache.Http.Config.MessageConstraints LineLen(int max)
		{
			return new Org.Apache.Http.Config.MessageConstraints(Args.NotNegative(max, "Max line length"
				), -1);
		}

		public static MessageConstraints.Builder Custom()
		{
			return new MessageConstraints.Builder();
		}

		public static MessageConstraints.Builder Copy(Org.Apache.Http.Config.MessageConstraints
			 config)
		{
			Args.NotNull(config, "Message constraints");
			return new MessageConstraints.Builder().SetMaxHeaderCount(config.GetMaxHeaderCount
				()).SetMaxLineLength(config.GetMaxLineLength());
		}

		public class Builder
		{
			private int maxLineLength;

			private int maxHeaderCount;

			internal Builder()
			{
				this.maxLineLength = -1;
				this.maxHeaderCount = -1;
			}

			public virtual MessageConstraints.Builder SetMaxLineLength(int maxLineLength)
			{
				this.maxLineLength = maxLineLength;
				return this;
			}

			public virtual MessageConstraints.Builder SetMaxHeaderCount(int maxHeaderCount)
			{
				this.maxHeaderCount = maxHeaderCount;
				return this;
			}

			public virtual MessageConstraints Build()
			{
				return new MessageConstraints(maxLineLength, maxHeaderCount);
			}
		}
	}
}
