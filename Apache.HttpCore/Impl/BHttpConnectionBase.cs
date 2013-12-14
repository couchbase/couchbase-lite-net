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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Org.Apache.Http;
using Org.Apache.Http.Config;
using Org.Apache.Http.Entity;
using Org.Apache.Http.IO;
using Org.Apache.Http.Impl;
using Org.Apache.Http.Impl.Entity;
using Org.Apache.Http.Impl.IO;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;
#if DefaultBHttpClientConnectionFactory
namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// This class serves as a base for all
	/// <see cref="Org.Apache.Http.HttpConnection">Org.Apache.Http.HttpConnection</see>
	/// implementations and provides
	/// functionality common to both client and server HTTP connections.
	/// </summary>
	/// <since>4.0</since>
	public class BHttpConnectionBase : HttpConnection, HttpInetConnection
	{
		private readonly SessionInputBufferImpl inbuffer;

		private readonly SessionOutputBufferImpl outbuffer;

		private readonly HttpConnectionMetricsImpl connMetrics;

		private readonly ContentLengthStrategy incomingContentStrategy;

		private readonly ContentLengthStrategy outgoingContentStrategy;

		private volatile bool open;

		private volatile Socket socket;

		/// <summary>Creates new instance of BHttpConnectionBase.</summary>
		/// <remarks>Creates new instance of BHttpConnectionBase.</remarks>
		/// <param name="buffersize">buffer size. Must be a positive number.</param>
		/// <param name="fragmentSizeHint">fragment size hint.</param>
		/// <param name="chardecoder">
		/// decoder to be used for decoding HTTP protocol elements.
		/// If <code>null</code> simple type cast will be used for byte to char conversion.
		/// </param>
		/// <param name="charencoder">
		/// encoder to be used for encoding HTTP protocol elements.
		/// If <code>null</code> simple type cast will be used for char to byte conversion.
		/// </param>
		/// <param name="constraints">
		/// Message constraints. If <code>null</code>
		/// <see cref="Org.Apache.Http.Config.MessageConstraints.Default">Org.Apache.Http.Config.MessageConstraints.Default
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="incomingContentStrategy">
		/// incoming content length strategy. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.Entity.LaxContentLengthStrategy.Instance">Org.Apache.Http.Impl.Entity.LaxContentLengthStrategy.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="outgoingContentStrategy">
		/// outgoing content length strategy. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy.Instance">Org.Apache.Http.Impl.Entity.StrictContentLengthStrategy.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		internal BHttpConnectionBase(int buffersize, int fragmentSizeHint, CharsetDecoder
			 chardecoder, CharsetEncoder charencoder, MessageConstraints constraints, ContentLengthStrategy
			 incomingContentStrategy, ContentLengthStrategy outgoingContentStrategy) : base(
			)
		{
			Args.Positive(buffersize, "Buffer size");
			HttpTransportMetricsImpl inTransportMetrics = new HttpTransportMetricsImpl();
			HttpTransportMetricsImpl outTransportMetrics = new HttpTransportMetricsImpl();
			this.inbuffer = new SessionInputBufferImpl(inTransportMetrics, buffersize, -1, constraints
				 != null ? constraints : MessageConstraints.Default, chardecoder);
			this.outbuffer = new SessionOutputBufferImpl(outTransportMetrics, buffersize, fragmentSizeHint
				, charencoder);
			this.connMetrics = new HttpConnectionMetricsImpl(inTransportMetrics, outTransportMetrics
				);
			this.incomingContentStrategy = incomingContentStrategy != null ? incomingContentStrategy
				 : LaxContentLengthStrategy.Instance;
			this.outgoingContentStrategy = outgoingContentStrategy != null ? outgoingContentStrategy
				 : StrictContentLengthStrategy.Instance;
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual void EnsureOpen()
		{
			Asserts.Check(this.open, "Connection is not open");
			if (!this.inbuffer.IsBound())
			{
				this.inbuffer.Bind(GetSocketInputStream(this.socket));
			}
			if (!this.outbuffer.IsBound())
			{
				this.outbuffer.Bind(GetSocketOutputStream(this.socket));
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual InputStream GetSocketInputStream(Socket socket)
		{
			return socket.GetInputStream();
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual OutputStream GetSocketOutputStream(Socket socket)
		{
			return socket.GetOutputStream();
		}

		/// <summary>
		/// Binds this connection to the given
		/// <see cref="System.Net.Sockets.Socket">System.Net.Sockets.Socket</see>
		/// . This socket will be
		/// used by the connection to send and receive data.
		/// <p/>
		/// After this method's execution the connection status will be reported
		/// as open and the
		/// <see cref="IsOpen()">IsOpen()</see>
		/// will return <code>true</code>.
		/// </summary>
		/// <param name="socket">the socket.</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		protected internal virtual void Bind(Socket socket)
		{
			Args.NotNull(socket, "Socket");
			this.socket = socket;
			this.open = true;
			this.inbuffer.Bind(null);
			this.outbuffer.Bind(null);
		}

		protected internal virtual SessionInputBuffer GetSessionInputBuffer()
		{
			return this.inbuffer;
		}

		protected internal virtual SessionOutputBuffer GetSessionOutputBuffer()
		{
			return this.outbuffer;
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual void DoFlush()
		{
			this.outbuffer.Flush();
		}

		public virtual bool IsOpen()
		{
			return this.open;
		}

		protected internal virtual Socket GetSocket()
		{
			return this.socket;
		}

		protected internal virtual OutputStream CreateOutputStream(long len, SessionOutputBuffer
			 outbuffer)
		{
			if (len == ContentLengthStrategy.Chunked)
			{
				return new ChunkedOutputStream(2048, outbuffer);
			}
			else
			{
				if (len == ContentLengthStrategy.Identity)
				{
					return new IdentityOutputStream(outbuffer);
				}
				else
				{
					return new ContentLengthOutputStream(outbuffer, len);
				}
			}
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		protected internal virtual OutputStream PrepareOutput(HttpMessage message)
		{
			long len = this.outgoingContentStrategy.DetermineLength(message);
			return CreateOutputStream(len, this.outbuffer);
		}

		protected internal virtual InputStream CreateInputStream(long len, SessionInputBuffer
			 inbuffer)
		{
			if (len == ContentLengthStrategy.Chunked)
			{
				return new ChunkedInputStream(inbuffer);
			}
			else
			{
				if (len == ContentLengthStrategy.Identity)
				{
					return new IdentityInputStream(inbuffer);
				}
				else
				{
					return new ContentLengthInputStream(inbuffer, len);
				}
			}
		}

		/// <exception cref="Org.Apache.Http.HttpException"></exception>
		protected internal virtual HttpEntity PrepareInput(HttpMessage message)
		{
			BasicHttpEntity entity = new BasicHttpEntity();
			long len = this.incomingContentStrategy.DetermineLength(message);
			InputStream instream = CreateInputStream(len, this.inbuffer);
			if (len == ContentLengthStrategy.Chunked)
			{
				entity.SetChunked(true);
				entity.SetContentLength(-1);
				entity.SetContent(instream);
			}
			else
			{
				if (len == ContentLengthStrategy.Identity)
				{
					entity.SetChunked(false);
					entity.SetContentLength(-1);
					entity.SetContent(instream);
				}
				else
				{
					entity.SetChunked(false);
					entity.SetContentLength(len);
					entity.SetContent(instream);
				}
			}
			Header contentTypeHeader = message.GetFirstHeader(HTTP.ContentType);
			if (contentTypeHeader != null)
			{
				entity.SetContentType(contentTypeHeader);
			}
			Header contentEncodingHeader = message.GetFirstHeader(HTTP.ContentEncoding);
			if (contentEncodingHeader != null)
			{
				entity.SetContentEncoding(contentEncodingHeader);
			}
			return entity;
		}

		public virtual IPAddress GetLocalAddress()
		{
			if (this.socket != null)
			{
				return this.socket.GetLocalAddress();
			}
			else
			{
				return null;
			}
		}

		public virtual int GetLocalPort()
		{
			if (this.socket != null)
			{
				return this.socket.GetLocalPort();
			}
			else
			{
				return -1;
			}
		}

		public virtual IPAddress GetRemoteAddress()
		{
			if (this.socket != null)
			{
				return this.socket.GetInetAddress();
			}
			else
			{
				return null;
			}
		}

		public virtual int GetRemotePort()
		{
			if (this.socket != null)
			{
				return this.socket.GetPort();
			}
			else
			{
				return -1;
			}
		}

		public virtual void SetSocketTimeout(int timeout)
		{
			if (this.socket != null)
			{
				try
				{
					this.socket.ReceiveTimeout = timeout;
				}
				catch (SocketException)
				{
				}
			}
		}

		// It is not quite clear from the Sun's documentation if there are any
		// other legitimate cases for a socket exception to be thrown when setting
		// SO_TIMEOUT besides the socket being already closed
		public virtual int GetSocketTimeout()
		{
			if (this.socket != null)
			{
				try
				{
					return this.socket.ReceiveTimeout;
				}
				catch (SocketException)
				{
					return -1;
				}
			}
			else
			{
				return -1;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Shutdown()
		{
			this.open = false;
			Socket tmpsocket = this.socket;
			if (tmpsocket != null)
			{
				tmpsocket.Close();
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual void Close()
		{
			if (!this.open)
			{
				return;
			}
			this.open = false;
			Socket sock = this.socket;
			try
			{
				this.inbuffer.Clear();
				this.outbuffer.Flush();
				try
				{
					try
					{
						sock.ShutdownOutput();
					}
					catch (IOException)
					{
					}
					try
					{
						sock.ShutdownInput();
					}
					catch (IOException)
					{
					}
				}
				catch (NotSupportedException)
				{
				}
			}
			finally
			{
				// if one isn't supported, the other one isn't either
				sock.Close();
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		private int FillInputBuffer(int timeout)
		{
			int oldtimeout = this.socket.ReceiveTimeout;
			try
			{
				this.socket.ReceiveTimeout = timeout;
				return this.inbuffer.FillBuffer();
			}
			finally
			{
				this.socket.ReceiveTimeout = oldtimeout;
			}
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual bool AwaitInput(int timeout)
		{
			if (this.inbuffer.HasBufferedData())
			{
				return true;
			}
			FillInputBuffer(timeout);
			return this.inbuffer.HasBufferedData();
		}

		public virtual bool IsStale()
		{
			if (!IsOpen())
			{
				return true;
			}
			try
			{
				int bytesRead = FillInputBuffer(1);
				return bytesRead < 0;
			}
			catch (SocketTimeoutException)
			{
				return false;
			}
			catch (IOException)
			{
				return true;
			}
		}

		protected internal virtual void IncrementRequestCount()
		{
			this.connMetrics.IncrementRequestCount();
		}

		protected internal virtual void IncrementResponseCount()
		{
			this.connMetrics.IncrementResponseCount();
		}

		public virtual HttpConnectionMetrics GetMetrics()
		{
			return this.connMetrics;
		}

		public override string ToString()
		{
			if (this.socket != null)
			{
				StringBuilder buffer = new StringBuilder();
				EndPoint remoteAddress = this.socket.RemoteEndPoint;
				EndPoint localAddress = this.socket.GetLocalSocketAddress();
				if (remoteAddress != null && localAddress != null)
				{
					NetUtils.FormatAddress(buffer, localAddress);
					buffer.Append("<->");
					NetUtils.FormatAddress(buffer, remoteAddress);
				}
				return buffer.ToString();
			}
			else
			{
				return "[Not bound]";
			}
		}
	}
}
#endif