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
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Config
{
	/// <summary>HTTP connection configuration.</summary>
	/// <remarks>HTTP connection configuration.</remarks>
	/// <since>4.3</since>
	public class ConnectionConfig : ICloneable
	{
		public static readonly Org.Apache.Http.Config.ConnectionConfig Default = new ConnectionConfig.Builder
			().Build();

		private readonly int bufferSize;

		private readonly int fragmentSizeHint;

		private readonly Encoding charset;

		private readonly CodingErrorAction malformedInputAction;

		private readonly CodingErrorAction unmappableInputAction;

		private readonly MessageConstraints messageConstraints;

		internal ConnectionConfig(int bufferSize, int fragmentSizeHint, Encoding charset, 
			CodingErrorAction malformedInputAction, CodingErrorAction unmappableInputAction, 
			MessageConstraints messageConstraints) : base()
		{
			this.bufferSize = bufferSize;
			this.fragmentSizeHint = fragmentSizeHint;
			this.charset = charset;
			this.malformedInputAction = malformedInputAction;
			this.unmappableInputAction = unmappableInputAction;
			this.messageConstraints = messageConstraints;
		}

		public virtual int GetBufferSize()
		{
			return bufferSize;
		}

		public virtual int GetFragmentSizeHint()
		{
			return fragmentSizeHint;
		}

		public virtual Encoding GetCharset()
		{
			return charset;
		}

        internal virtual CodingErrorAction GetMalformedInputAction()
		{
			return malformedInputAction;
		}

        internal virtual CodingErrorAction GetUnmappableInputAction()
		{
			return unmappableInputAction;
		}

		public virtual MessageConstraints GetMessageConstraints()
		{
			return messageConstraints;
		}

		/// <exception cref="Sharpen.CloneNotSupportedException"></exception>
		protected internal virtual Org.Apache.Http.Config.ConnectionConfig Clone()
		{
            return (Org.Apache.Http.Config.ConnectionConfig)MemberwiseClone();
		}

		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			builder.Append("[bufferSize=").Append(this.bufferSize).Append(", fragmentSizeHint="
				).Append(this.fragmentSizeHint).Append(", charset=").Append(this.charset).Append
				(", malformedInputAction=").Append(this.malformedInputAction).Append(", unmappableInputAction="
				).Append(this.unmappableInputAction).Append(", messageConstraints=").Append(this
				.messageConstraints).Append("]");
			return builder.ToString();
		}

		public static ConnectionConfig.Builder Custom()
		{
			return new ConnectionConfig.Builder();
		}

		public static ConnectionConfig.Builder Copy(Org.Apache.Http.Config.ConnectionConfig
			 config)
		{
			Args.NotNull(config, "Connection config");
			return new ConnectionConfig.Builder().SetCharset(config.GetCharset()).SetMalformedInputAction
				(config.GetMalformedInputAction()).SetUnmappableInputAction(config.GetUnmappableInputAction
				()).SetMessageConstraints(config.GetMessageConstraints());
		}

		public class Builder
		{
			private int bufferSize;

			private int fragmentSizeHint;

			private Encoding charset;

			private CodingErrorAction malformedInputAction;

			private CodingErrorAction unmappableInputAction;

			private MessageConstraints messageConstraints;

			internal Builder()
			{
				this.fragmentSizeHint = -1;
			}

			public virtual ConnectionConfig.Builder SetBufferSize(int bufferSize)
			{
				this.bufferSize = bufferSize;
				return this;
			}

			public virtual ConnectionConfig.Builder SetFragmentSizeHint(int fragmentSizeHint)
			{
				this.fragmentSizeHint = fragmentSizeHint;
				return this;
			}

			public virtual ConnectionConfig.Builder SetCharset(Encoding charset)
			{
				this.charset = charset;
				return this;
			}

            internal virtual ConnectionConfig.Builder SetMalformedInputAction(CodingErrorAction
				 malformedInputAction)
			{
				this.malformedInputAction = malformedInputAction;
				if (malformedInputAction != null && this.charset == null)
				{
					this.charset = Consts.Ascii;
				}
				return this;
			}

            internal virtual ConnectionConfig.Builder SetUnmappableInputAction(CodingErrorAction
				 unmappableInputAction)
			{
				this.unmappableInputAction = unmappableInputAction;
				if (unmappableInputAction != null && this.charset == null)
				{
					this.charset = Consts.Ascii;
				}
				return this;
			}

			public virtual ConnectionConfig.Builder SetMessageConstraints(MessageConstraints 
				messageConstraints)
			{
				this.messageConstraints = messageConstraints;
				return this;
			}

			public virtual ConnectionConfig Build()
			{
				Encoding cs = charset;
				if (cs == null && (malformedInputAction != null || unmappableInputAction != null))
				{
					cs = Consts.Ascii;
				}
				int bufSize = this.bufferSize > 0 ? this.bufferSize : 8 * 1024;
				int fragmentHintSize = this.fragmentSizeHint >= 0 ? this.fragmentSizeHint : bufSize;
				return new ConnectionConfig(bufSize, fragmentHintSize, cs, malformedInputAction, 
					unmappableInputAction, messageConstraints);
			}
		}

        #region ICloneable implementation

        object ICloneable.Clone ()
        {
            return base.MemberwiseClone();
        }

        #endregion
	}
}
