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
using Org.Apache.Http.Params;
using Sharpen;

namespace Org.Apache.Http.Params
{
	/// <since>4.3</since>
	[System.ObsoleteAttribute(@"(4.3) provided for compatibility with HttpParams . Do not use."
		)]
	public sealed class HttpParamConfig
	{
		private HttpParamConfig()
		{
		}

		public static SocketConfig GetSocketConfig(HttpParams @params)
		{
			return SocketConfig.Custom().SetSoTimeout(@params.GetIntParameter(CoreConnectionPNames
				.SoTimeout, 0)).SetSoReuseAddress(@params.GetBooleanParameter(CoreConnectionPNames
				.SoReuseaddr, false)).SetSoKeepAlive(@params.GetBooleanParameter(CoreConnectionPNames
				.SoKeepalive, false)).SetSoLinger(@params.GetIntParameter(CoreConnectionPNames.SoLinger
				, -1)).SetTcpNoDelay(@params.GetBooleanParameter(CoreConnectionPNames.TcpNodelay
				, true)).Build();
		}

		public static MessageConstraints GetMessageConstraints(HttpParams @params)
		{
			return MessageConstraints.Custom().SetMaxHeaderCount(@params.GetIntParameter(CoreConnectionPNames
				.MaxHeaderCount, -1)).SetMaxLineLength(@params.GetIntParameter(CoreConnectionPNames
				.MaxLineLength, -1)).Build();
		}

		public static ConnectionConfig GetConnectionConfig(HttpParams @params)
		{
			MessageConstraints messageConstraints = GetMessageConstraints(@params);
			string csname = (string)@params.GetParameter(CoreProtocolPNames.HttpElementCharset
				);
			return ConnectionConfig.Custom().SetCharset(csname != null ? Sharpen.Extensions.GetEncoding
				(csname) : null).SetMalformedInputAction((CodingErrorAction)@params.GetParameter
				(CoreProtocolPNames.HttpMalformedInputAction)).SetMalformedInputAction((CodingErrorAction
				)@params.GetParameter(CoreProtocolPNames.HttpUnmappableInputAction)).SetMessageConstraints
				(messageConstraints).Build();
		}
	}
}
