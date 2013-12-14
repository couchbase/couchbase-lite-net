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

using System.Text;
using Org.Apache.Http.Config;
using Org.Apache.Http.Impl;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>Connection support methods.</summary>
	/// <remarks>Connection support methods.</remarks>
	/// <since>4.3</since>
    internal sealed class ConnSupport
	{
        internal static CharsetDecoder CreateDecoder(ConnectionConfig cconfig)
		{
			if (cconfig == null)
			{
				return null;
			}
			Encoding charset = cconfig.GetCharset();
			CodingErrorAction malformed = cconfig.GetMalformedInputAction();
			CodingErrorAction unmappable = cconfig.GetUnmappableInputAction();
			if (charset != null)
			{
				return charset.NewDecoder().OnMalformedInput(malformed != null ? malformed : CodingErrorAction
					.Report).OnUnmappableCharacter(unmappable != null ? unmappable : CodingErrorAction
					.Report);
			}
			else
			{
				return null;
			}
		}

        internal static CharsetEncoder CreateEncoder(ConnectionConfig cconfig)
		{
			if (cconfig == null)
			{
				return null;
			}
			Encoding charset = cconfig.GetCharset();
			if (charset != null)
			{
				CodingErrorAction malformed = cconfig.GetMalformedInputAction();
				CodingErrorAction unmappable = cconfig.GetUnmappableInputAction();
				return charset.NewEncoder().OnMalformedInput(malformed != null ? malformed : CodingErrorAction
					.Report).OnUnmappableCharacter(unmappable != null ? unmappable : CodingErrorAction
					.Report);
			}
			else
			{
				return null;
			}
		}
	}
}
