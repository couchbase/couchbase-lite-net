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
using Sharpen;

namespace Org.Apache.Http
{
	/// <summary>Signals that an HTTP exception has occurred.</summary>
	/// <remarks>Signals that an HTTP exception has occurred.</remarks>
	/// <since>4.0</since>
	[System.Serializable]
	public class HttpException : Exception
	{
		private const long serialVersionUID = -5437299376222011036L;

		/// <summary>Creates a new HttpException with a <tt>null</tt> detail message.</summary>
		/// <remarks>Creates a new HttpException with a <tt>null</tt> detail message.</remarks>
		public HttpException() : base()
		{
		}

		/// <summary>Creates a new HttpException with the specified detail message.</summary>
		/// <remarks>Creates a new HttpException with the specified detail message.</remarks>
		/// <param name="message">the exception detail message</param>
		public HttpException(string message) : base(message)
		{
		}

		/// <summary>Creates a new HttpException with the specified detail message and cause.
		/// 	</summary>
		/// <remarks>Creates a new HttpException with the specified detail message and cause.
		/// 	</remarks>
		/// <param name="message">the exception detail message</param>
		/// <param name="cause">
		/// the <tt>Throwable</tt> that caused this exception, or <tt>null</tt>
		/// if the cause is unavailable, unknown, or not a <tt>Throwable</tt>
		/// </param>
		public HttpException(string message, Exception cause) : base(message)
		{
			Sharpen.Extensions.InitCause(this, cause);
		}
	}
}
