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

using Org.Apache.Http;
using Org.Apache.Http.Entity;
using Org.Apache.Http.Impl.Entity;
using Sharpen;

namespace Org.Apache.Http.Impl.Entity
{
	/// <summary>
	/// Decorator for
	/// <see cref="Org.Apache.Http.Entity.ContentLengthStrategy">Org.Apache.Http.Entity.ContentLengthStrategy
	/// 	</see>
	/// implementations that disallows the use of
	/// identity transfer encoding.
	/// </summary>
	/// <since>4.2</since>
	public class DisallowIdentityContentLengthStrategy : ContentLengthStrategy
	{
		public static readonly Org.Apache.Http.Impl.Entity.DisallowIdentityContentLengthStrategy
			 Instance = new Org.Apache.Http.Impl.Entity.DisallowIdentityContentLengthStrategy
			(new LaxContentLengthStrategy(0));

		private readonly ContentLengthStrategy contentLengthStrategy;

		public DisallowIdentityContentLengthStrategy(ContentLengthStrategy contentLengthStrategy
			) : base()
		{
			this.contentLengthStrategy = contentLengthStrategy;
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		public override long DetermineLength(HttpMessage message)
		{
			long result = this.contentLengthStrategy.DetermineLength(message);
			if (result == ContentLengthStrategy.Identity)
			{
				throw new ProtocolException("Identity transfer encoding cannot be used");
			}
			return result;
		}
	}
}
