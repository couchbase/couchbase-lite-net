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
using Org.Apache.Http;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// <tt>HttpRequestExecutor</tt> is a client side HTTP protocol handler based
	/// on the blocking (classic) I/O model.
	/// </summary>
	/// <remarks>
	/// <tt>HttpRequestExecutor</tt> is a client side HTTP protocol handler based
	/// on the blocking (classic) I/O model.
	/// <p/>
	/// <tt>HttpRequestExecutor</tt> relies on
	/// <see cref="HttpProcessor">HttpProcessor</see>
	/// to generate
	/// mandatory protocol headers for all outgoing messages and apply common,
	/// cross-cutting message transformations to all incoming and outgoing messages.
	/// Application specific processing can be implemented outside
	/// <tt>HttpRequestExecutor</tt> once the request has been executed and
	/// a response has been received.
	/// </remarks>
	/// <since>4.0</since>
	public class HttpRequestExecutor
	{
		public const int DefaultWaitForContinue = 3000;

		private readonly int waitForContinue;

		/// <summary>Creates new instance of HttpRequestExecutor.</summary>
		/// <remarks>Creates new instance of HttpRequestExecutor.</remarks>
		/// <since>4.3</since>
		public HttpRequestExecutor(int waitForContinue) : base()
		{
			this.waitForContinue = Args.Positive(waitForContinue, "Wait for continue time");
		}

		public HttpRequestExecutor() : this(DefaultWaitForContinue)
		{
		}

		/// <summary>Decide whether a response comes with an entity.</summary>
		/// <remarks>
		/// Decide whether a response comes with an entity.
		/// The implementation in this class is based on RFC 2616.
		/// <br/>
		/// Derived executors can override this method to handle
		/// methods and response codes not specified in RFC 2616.
		/// </remarks>
		/// <param name="request">the request, to obtain the executed method</param>
		/// <param name="response">the response, to obtain the status code</param>
		protected internal virtual bool CanResponseHaveBody(IHttpRequest request, HttpResponse
			 response)
		{
			if (Sharpen.Runtime.EqualsIgnoreCase("HEAD", request.GetRequestLine().GetMethod()
				))
			{
				return false;
			}
			int status = response.GetStatusLine().GetStatusCode();
			return status >= HttpStatus.ScOk && status != HttpStatus.ScNoContent && status !=
				 HttpStatus.ScNotModified && status != HttpStatus.ScResetContent;
		}

		/// <summary>Sends the request and obtain a response.</summary>
		/// <remarks>Sends the request and obtain a response.</remarks>
		/// <param name="request">the request to execute.</param>
		/// <param name="conn">the connection over which to execute the request.</param>
		/// <returns>the response to the request.</returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		public virtual HttpResponse Execute(IHttpRequest request, HttpClientConnection conn
			, HttpContext context)
		{
			Args.NotNull(request, "HTTP request");
			Args.NotNull(conn, "Client connection");
			Args.NotNull(context, "HTTP context");
			try
			{
				HttpResponse response = DoSendRequest(request, conn, context);
				if (response == null)
				{
					response = DoReceiveResponse(request, conn, context);
				}
				return response;
			}
			catch (IOException ex)
			{
				CloseConnection(conn);
				throw;
			}
			catch (HttpException ex)
			{
				CloseConnection(conn);
				throw;
			}
			catch (RuntimeException ex)
			{
				CloseConnection(conn);
				throw;
			}
		}

		private static void CloseConnection(HttpClientConnection conn)
		{
			try
			{
				conn.Close();
			}
			catch (IOException)
			{
			}
		}

		/// <summary>
		/// Pre-process the given request using the given protocol processor and
		/// initiates the process of request execution.
		/// </summary>
		/// <remarks>
		/// Pre-process the given request using the given protocol processor and
		/// initiates the process of request execution.
		/// </remarks>
		/// <param name="request">the request to prepare</param>
		/// <param name="processor">the processor to use</param>
		/// <param name="context">the context for sending the request</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		public virtual void PreProcess(IHttpRequest request, HttpProcessor processor, HttpContext
			 context)
		{
			Args.NotNull(request, "HTTP request");
			Args.NotNull(processor, "HTTP processor");
			Args.NotNull(context, "HTTP context");
			context.SetAttribute(HttpCoreContext.HttpRequest, request);
			processor.Process(request, context);
		}

		/// <summary>Send the given request over the given connection.</summary>
		/// <remarks>
		/// Send the given request over the given connection.
		/// <p>
		/// This method also handles the expect-continue handshake if necessary.
		/// If it does not have to handle an expect-continue handshake, it will
		/// not use the connection for reading or anything else that depends on
		/// data coming in over the connection.
		/// </remarks>
		/// <param name="request">
		/// the request to send, already
		/// <see cref="PreProcess(Org.Apache.Http.IHttpRequest, HttpProcessor, HttpContext)">preprocessed
		/// 	</see>
		/// </param>
		/// <param name="conn">
		/// the connection over which to send the request,
		/// already established
		/// </param>
		/// <param name="context">the context for sending the request</param>
		/// <returns>
		/// a terminal response received as part of an expect-continue
		/// handshake, or
		/// <code>null</code> if the expect-continue handshake is not used
		/// </returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		protected internal virtual HttpResponse DoSendRequest(IHttpRequest request, HttpClientConnection
			 conn, HttpContext context)
		{
			Args.NotNull(request, "HTTP request");
			Args.NotNull(conn, "Client connection");
			Args.NotNull(context, "HTTP context");
			HttpResponse response = null;
			context.SetAttribute(HttpCoreContext.HttpConnection, conn);
			context.SetAttribute(HttpCoreContext.HttpReqSent, false);
			conn.SendRequestHeader(request);
			if (request is HttpEntityEnclosingRequest)
			{
				// Check for expect-continue handshake. We have to flush the
				// headers and wait for an 100-continue response to handle it.
				// If we get a different response, we must not send the entity.
				bool sendentity = true;
				ProtocolVersion ver = request.GetRequestLine().GetProtocolVersion();
				if (((HttpEntityEnclosingRequest)request).ExpectContinue() && !ver.LessEquals(HttpVersion
					.Http10))
				{
					conn.Flush();
					// As suggested by RFC 2616 section 8.2.3, we don't wait for a
					// 100-continue response forever. On timeout, send the entity.
					if (conn.IsResponseAvailable(this.waitForContinue))
					{
						response = conn.ReceiveResponseHeader();
						if (CanResponseHaveBody(request, response))
						{
							conn.ReceiveResponseEntity(response);
						}
						int status = response.GetStatusLine().GetStatusCode();
						if (status < 200)
						{
							if (status != HttpStatus.ScContinue)
							{
								throw new ProtocolException("Unexpected response: " + response.GetStatusLine());
							}
							// discard 100-continue
							response = null;
						}
						else
						{
							sendentity = false;
						}
					}
				}
				if (sendentity)
				{
					conn.SendRequestEntity((HttpEntityEnclosingRequest)request);
				}
			}
			conn.Flush();
			context.SetAttribute(HttpCoreContext.HttpReqSent, true);
			return response;
		}

		/// <summary>Waits for and receives a response.</summary>
		/// <remarks>
		/// Waits for and receives a response.
		/// This method will automatically ignore intermediate responses
		/// with status code 1xx.
		/// </remarks>
		/// <param name="request">the request for which to obtain the response</param>
		/// <param name="conn">the connection over which the request was sent</param>
		/// <param name="context">the context for receiving the response</param>
		/// <returns>the terminal response, not yet post-processed</returns>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		protected internal virtual HttpResponse DoReceiveResponse(IHttpRequest request, HttpClientConnection
			 conn, HttpContext context)
		{
			Args.NotNull(request, "HTTP request");
			Args.NotNull(conn, "Client connection");
			Args.NotNull(context, "HTTP context");
			HttpResponse response = null;
			int statusCode = 0;
			while (response == null || statusCode < HttpStatus.ScOk)
			{
				response = conn.ReceiveResponseHeader();
				if (CanResponseHaveBody(request, response))
				{
					conn.ReceiveResponseEntity(response);
				}
				statusCode = response.GetStatusLine().GetStatusCode();
			}
			// while intermediate response
			return response;
		}

		/// <summary>
		/// Post-processes the given response using the given protocol processor and
		/// completes the process of request execution.
		/// </summary>
		/// <remarks>
		/// Post-processes the given response using the given protocol processor and
		/// completes the process of request execution.
		/// <p>
		/// This method does <i>not</i> read the response entity, if any.
		/// The connection over which content of the response entity is being
		/// streamed from cannot be reused until
		/// <see cref="Org.Apache.Http.Util.EntityUtils.Consume(Org.Apache.Http.HttpEntity)">Org.Apache.Http.Util.EntityUtils.Consume(Org.Apache.Http.HttpEntity)
		/// 	</see>
		/// has been invoked.
		/// </remarks>
		/// <param name="response">the response object to post-process</param>
		/// <param name="processor">the processor to use</param>
		/// <param name="context">the context for post-processing the response</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		public virtual void PostProcess(HttpResponse response, HttpProcessor processor, HttpContext
			 context)
		{
			Args.NotNull(response, "HTTP response");
			Args.NotNull(processor, "HTTP processor");
			Args.NotNull(context, "HTTP context");
			context.SetAttribute(HttpCoreContext.HttpResponse, response);
			processor.Process(response, context);
		}
	}
}
