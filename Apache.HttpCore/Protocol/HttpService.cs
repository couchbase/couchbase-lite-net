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
using Org.Apache.Http;
using Org.Apache.Http.Entity;
using Org.Apache.Http.Impl;
using Org.Apache.Http.Params;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>
	/// <tt>HttpService</tt> is a server side HTTP protocol handler based on
	/// the classic (blocking) I/O model.
	/// </summary>
	/// <remarks>
	/// <tt>HttpService</tt> is a server side HTTP protocol handler based on
	/// the classic (blocking) I/O model.
	/// <p/>
	/// <tt>HttpService</tt> relies on
	/// <see cref="HttpProcessor">HttpProcessor</see>
	/// to generate mandatory
	/// protocol headers for all outgoing messages and apply common, cross-cutting
	/// message transformations to all incoming and outgoing messages, whereas
	/// individual
	/// <see cref="HttpRequestHandler">HttpRequestHandler</see>
	/// s are expected to implement
	/// application specific content generation and processing.
	/// <p/>
	/// <tt>HttpService</tt> uses
	/// <see cref="HttpRequestHandlerMapper">HttpRequestHandlerMapper</see>
	/// to map
	/// matching request handler for a particular request URI of an incoming HTTP
	/// request.
	/// <p/>
	/// <tt>HttpService</tt> can use optional
	/// <see cref="HttpExpectationVerifier">HttpExpectationVerifier</see>
	/// to ensure that incoming requests meet server's expectations.
	/// </remarks>
	/// <since>4.0</since>
	public class HttpService
	{
		/// <summary>TODO: make all variables final in the next major version</summary>
		private volatile HttpParams @params = null;

		private volatile HttpProcessor processor = null;

		private volatile HttpRequestHandlerMapper handlerMapper = null;

		private volatile ConnectionReuseStrategy connStrategy = null;

		private volatile HttpResponseFactory responseFactory = null;

		private volatile HttpExpectationVerifier expectationVerifier = null;

		/// <summary>Create a new HTTP service.</summary>
		/// <remarks>Create a new HTTP service.</remarks>
		/// <param name="processor">the processor to use on requests and responses</param>
		/// <param name="connStrategy">the connection reuse strategy</param>
		/// <param name="responseFactory">the response factory</param>
		/// <param name="handlerResolver">the handler resolver. May be null.</param>
		/// <param name="expectationVerifier">the expectation verifier. May be null.</param>
		/// <param name="params">the HTTP parameters</param>
		/// <since>4.1</since>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use HttpService(HttpProcessor, Org.Apache.Http.ConnectionReuseStrategy, Org.Apache.Http.HttpResponseFactory, HttpRequestHandlerMapper, HttpExpectationVerifier)"
			)]
		public HttpService(HttpProcessor processor, ConnectionReuseStrategy connStrategy, 
			HttpResponseFactory responseFactory, HttpRequestHandlerResolver handlerResolver, 
			HttpExpectationVerifier expectationVerifier, HttpParams @params) : this(processor
			, connStrategy, responseFactory, new HttpService.HttpRequestHandlerResolverAdapter
			(handlerResolver), expectationVerifier)
		{
			// provided injected dependencies are immutable and deprecated methods are not used
			this.@params = @params;
		}

		/// <summary>Create a new HTTP service.</summary>
		/// <remarks>Create a new HTTP service.</remarks>
		/// <param name="processor">the processor to use on requests and responses</param>
		/// <param name="connStrategy">the connection reuse strategy</param>
		/// <param name="responseFactory">the response factory</param>
		/// <param name="handlerResolver">the handler resolver. May be null.</param>
		/// <param name="params">the HTTP parameters</param>
		/// <since>4.1</since>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) use HttpService(HttpProcessor, Org.Apache.Http.ConnectionReuseStrategy, Org.Apache.Http.HttpResponseFactory, HttpRequestHandlerMapper)"
			)]
		public HttpService(HttpProcessor processor, ConnectionReuseStrategy connStrategy, 
			HttpResponseFactory responseFactory, HttpRequestHandlerResolver handlerResolver, 
			HttpParams @params) : this(processor, connStrategy, responseFactory, new HttpService.HttpRequestHandlerResolverAdapter
			(handlerResolver), null)
		{
			this.@params = @params;
		}

		/// <summary>Create a new HTTP service.</summary>
		/// <remarks>Create a new HTTP service.</remarks>
		/// <param name="proc">the processor to use on requests and responses</param>
		/// <param name="connStrategy">the connection reuse strategy</param>
		/// <param name="responseFactory">the response factory</param>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) use HttpService(HttpProcessor, Org.Apache.Http.ConnectionReuseStrategy, Org.Apache.Http.HttpResponseFactory, HttpRequestHandlerResolver, Org.Apache.Http.Params.HttpParams)"
			)]
		public HttpService(HttpProcessor proc, ConnectionReuseStrategy connStrategy, HttpResponseFactory
			 responseFactory) : base()
		{
			SetHttpProcessor(proc);
			SetConnReuseStrategy(connStrategy);
			SetResponseFactory(responseFactory);
		}

		/// <summary>Create a new HTTP service.</summary>
		/// <remarks>Create a new HTTP service.</remarks>
		/// <param name="processor">the processor to use on requests and responses</param>
		/// <param name="connStrategy">
		/// the connection reuse strategy. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.DefaultConnectionReuseStrategy.Instance">Org.Apache.Http.Impl.DefaultConnectionReuseStrategy.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="responseFactory">
		/// the response factory. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.DefaultHttpResponseFactory.Instance">Org.Apache.Http.Impl.DefaultHttpResponseFactory.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="handlerMapper">the handler mapper. May be null.</param>
		/// <param name="expectationVerifier">the expectation verifier. May be null.</param>
		/// <since>4.3</since>
		public HttpService(HttpProcessor processor, ConnectionReuseStrategy connStrategy, 
			HttpResponseFactory responseFactory, HttpRequestHandlerMapper handlerMapper, HttpExpectationVerifier
			 expectationVerifier) : base()
		{
			this.processor = Args.NotNull(processor, "HTTP processor");
			this.connStrategy = connStrategy != null ? connStrategy : DefaultConnectionReuseStrategy
				.Instance;
			this.responseFactory = responseFactory != null ? responseFactory : DefaultHttpResponseFactory
				.Instance;
			this.handlerMapper = handlerMapper;
			this.expectationVerifier = expectationVerifier;
		}

		/// <summary>Create a new HTTP service.</summary>
		/// <remarks>Create a new HTTP service.</remarks>
		/// <param name="processor">the processor to use on requests and responses</param>
		/// <param name="connStrategy">
		/// the connection reuse strategy. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.DefaultConnectionReuseStrategy.Instance">Org.Apache.Http.Impl.DefaultConnectionReuseStrategy.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="responseFactory">
		/// the response factory. If <code>null</code>
		/// <see cref="Org.Apache.Http.Impl.DefaultHttpResponseFactory.Instance">Org.Apache.Http.Impl.DefaultHttpResponseFactory.Instance
		/// 	</see>
		/// will be used.
		/// </param>
		/// <param name="handlerMapper">the handler mapper. May be null.</param>
		/// <since>4.3</since>
		public HttpService(HttpProcessor processor, ConnectionReuseStrategy connStrategy, 
			HttpResponseFactory responseFactory, HttpRequestHandlerMapper handlerMapper) : this
			(processor, connStrategy, responseFactory, handlerMapper, null)
		{
		}

		/// <summary>Create a new HTTP service.</summary>
		/// <remarks>Create a new HTTP service.</remarks>
		/// <param name="processor">the processor to use on requests and responses</param>
		/// <param name="handlerMapper">the handler mapper. May be null.</param>
		/// <since>4.3</since>
		public HttpService(HttpProcessor processor, HttpRequestHandlerMapper handlerMapper
			) : this(processor, null, null, handlerMapper, null)
		{
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) set HttpProcessor using constructor")]
		public virtual void SetHttpProcessor(HttpProcessor processor)
		{
			Args.NotNull(processor, "HTTP processor");
			this.processor = processor;
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) set Org.Apache.Http.ConnectionReuseStrategy using constructor"
			)]
		public virtual void SetConnReuseStrategy(ConnectionReuseStrategy connStrategy)
		{
			Args.NotNull(connStrategy, "Connection reuse strategy");
			this.connStrategy = connStrategy;
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) set Org.Apache.Http.HttpResponseFactory using constructor"
			)]
		public virtual void SetResponseFactory(HttpResponseFactory responseFactory)
		{
			Args.NotNull(responseFactory, "Response factory");
			this.responseFactory = responseFactory;
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) set Org.Apache.Http.HttpResponseFactory using constructor"
			)]
		public virtual void SetParams(HttpParams @params)
		{
			this.@params = @params;
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) set HttpRequestHandlerResolver using constructor"
			)]
		public virtual void SetHandlerResolver(HttpRequestHandlerResolver handlerResolver
			)
		{
			this.handlerMapper = new HttpService.HttpRequestHandlerResolverAdapter(handlerResolver
				);
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) set HttpExpectationVerifier using constructor")]
		public virtual void SetExpectationVerifier(HttpExpectationVerifier expectationVerifier
			)
		{
			this.expectationVerifier = expectationVerifier;
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.3) no longer used.")]
		public virtual HttpParams GetParams()
		{
			return this.@params;
		}

		/// <summary>
		/// Handles receives one HTTP request over the given connection within the
		/// given execution context and sends a response back to the client.
		/// </summary>
		/// <remarks>
		/// Handles receives one HTTP request over the given connection within the
		/// given execution context and sends a response back to the client.
		/// </remarks>
		/// <param name="conn">the active connection to the client</param>
		/// <param name="context">the actual execution context.</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		public virtual void HandleRequest(HttpServerConnection conn, HttpContext context)
		{
			context.SetAttribute(HttpCoreContext.HttpConnection, conn);
			HttpResponse response = null;
			try
			{
				IHttpRequest request = conn.ReceiveRequestHeader();
				if (request is HttpEntityEnclosingRequest)
				{
					if (((HttpEntityEnclosingRequest)request).ExpectContinue())
					{
						response = this.responseFactory.NewHttpResponse(HttpVersion.Http11, HttpStatus.ScContinue
							, context);
						if (this.expectationVerifier != null)
						{
							try
							{
								this.expectationVerifier.Verify(request, response, context);
							}
							catch (HttpException ex)
							{
								response = this.responseFactory.NewHttpResponse(HttpVersion.Http10, HttpStatus.ScInternalServerError
									, context);
								HandleException(ex, response);
							}
						}
						if (response.GetStatusLine().GetStatusCode() < 200)
						{
							// Send 1xx response indicating the server expections
							// have been met
							conn.SendResponseHeader(response);
							conn.Flush();
							response = null;
							conn.ReceiveRequestEntity((HttpEntityEnclosingRequest)request);
						}
					}
					else
					{
						conn.ReceiveRequestEntity((HttpEntityEnclosingRequest)request);
					}
				}
				context.SetAttribute(HttpCoreContext.HttpRequest, request);
				if (response == null)
				{
					response = this.responseFactory.NewHttpResponse(HttpVersion.Http11, HttpStatus.ScOk
						, context);
					this.processor.Process(request, context);
					DoService(request, response, context);
				}
				// Make sure the request content is fully consumed
				if (request is HttpEntityEnclosingRequest)
				{
					HttpEntity entity = ((HttpEntityEnclosingRequest)request).GetEntity();
					EntityUtils.Consume(entity);
				}
			}
			catch (HttpException ex)
			{
				response = this.responseFactory.NewHttpResponse(HttpVersion.Http10, HttpStatus.ScInternalServerError
					, context);
				HandleException(ex, response);
			}
			context.SetAttribute(HttpCoreContext.HttpResponse, response);
			this.processor.Process(response, context);
			conn.SendResponseHeader(response);
			conn.SendResponseEntity(response);
			conn.Flush();
			if (!this.connStrategy.KeepAlive(response, context))
			{
				conn.Close();
			}
		}

		/// <summary>
		/// Handles the given exception and generates an HTTP response to be sent
		/// back to the client to inform about the exceptional condition encountered
		/// in the course of the request processing.
		/// </summary>
		/// <remarks>
		/// Handles the given exception and generates an HTTP response to be sent
		/// back to the client to inform about the exceptional condition encountered
		/// in the course of the request processing.
		/// </remarks>
		/// <param name="ex">the exception.</param>
		/// <param name="response">the HTTP response.</param>
		protected internal virtual void HandleException(HttpException ex, HttpResponse response
			)
		{
			if (ex is MethodNotSupportedException)
			{
				response.SetStatusCode(HttpStatus.ScNotImplemented);
			}
			else
			{
				if (ex is UnsupportedHttpVersionException)
				{
					response.SetStatusCode(HttpStatus.ScHttpVersionNotSupported);
				}
				else
				{
					if (ex is ProtocolException)
					{
						response.SetStatusCode(HttpStatus.ScBadRequest);
					}
					else
					{
						response.SetStatusCode(HttpStatus.ScInternalServerError);
					}
				}
			}
			string message = ex.Message;
			if (message == null)
			{
				message = ex.ToString();
			}
			byte[] msg = EncodingUtils.GetAsciiBytes(message);
			ByteArrayEntity entity = new ByteArrayEntity(msg);
			entity.SetContentType("text/plain; charset=US-ASCII");
			response.SetEntity(entity);
		}

		/// <summary>
		/// The default implementation of this method attempts to resolve an
		/// <see cref="HttpRequestHandler">HttpRequestHandler</see>
		/// for the request URI of the given request
		/// and, if found, executes its
		/// <see cref="HttpRequestHandler.Handle(Org.Apache.Http.IHttpRequest, Org.Apache.Http.HttpResponse, HttpContext)
		/// 	">HttpRequestHandler.Handle(Org.Apache.Http.IHttpRequest, Org.Apache.Http.HttpResponse, HttpContext)
		/// 	</see>
		/// method.
		/// <p>
		/// Super-classes can override this method in order to provide a custom
		/// implementation of the request processing logic.
		/// </summary>
		/// <param name="request">the HTTP request.</param>
		/// <param name="response">the HTTP response.</param>
		/// <param name="context">the execution context.</param>
		/// <exception cref="System.IO.IOException">in case of an I/O error.</exception>
		/// <exception cref="Org.Apache.Http.HttpException">
		/// in case of HTTP protocol violation or a processing
		/// problem.
		/// </exception>
		protected internal virtual void DoService(IHttpRequest request, HttpResponse response
			, HttpContext context)
		{
			HttpRequestHandler handler = null;
			if (this.handlerMapper != null)
			{
				handler = this.handlerMapper.Lookup(request);
			}
			if (handler != null)
			{
				handler.Handle(request, response, context);
			}
			else
			{
				response.SetStatusCode(HttpStatus.ScNotImplemented);
			}
		}

		/// <summary>Adaptor class to transition from HttpRequestHandlerResolver to HttpRequestHandlerMapper.
		/// 	</summary>
		/// <remarks>Adaptor class to transition from HttpRequestHandlerResolver to HttpRequestHandlerMapper.
		/// 	</remarks>
		private class HttpRequestHandlerResolverAdapter : HttpRequestHandlerMapper
		{
			private readonly HttpRequestHandlerResolver resolver;

			public HttpRequestHandlerResolverAdapter(HttpRequestHandlerResolver resolver)
			{
				this.resolver = resolver;
			}

			public virtual HttpRequestHandler Lookup(IHttpRequest request)
			{
				return resolver.Lookup(request.GetRequestLine().GetUri());
			}
		}
	}
}
