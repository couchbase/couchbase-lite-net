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

using System.IO;
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>Signals that the target server failed to respond with a valid HTTP response.
	/// 	</summary>
	/// <remarks>Signals that the target server failed to respond with a valid HTTP response.
	/// 	</remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public class NoHttpResponseException : IOException
	{
		private const long serialVersionUID = -7658940387386078766L;

		/// <summary>Creates a new NoHttpResponseException with the specified detail message.
		/// 	</summary>
		/// <remarks>Creates a new NoHttpResponseException with the specified detail message.
		/// 	</remarks>
		/// <param name="message">exception message</param>
		public NoHttpResponseException(string message) : base(message)
		{
		}
	}
}
