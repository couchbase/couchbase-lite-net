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
using System.Text;
using Org.Apache.Http;
using Org.Apache.Http.Entity;
using Org.Apache.Http.Protocol;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Util
{
	/// <summary>
	/// Static helpers for dealing with
	/// <see cref="Org.Apache.Http.HttpEntity">Org.Apache.Http.HttpEntity</see>
	/// s.
	/// </summary>
	/// <since>4.0</since>
	public sealed class EntityUtils
	{
		private EntityUtils()
		{
		}

		/// <summary>
		/// Ensures that the entity content is fully consumed and the content stream, if exists,
		/// is closed.
		/// </summary>
		/// <remarks>
		/// Ensures that the entity content is fully consumed and the content stream, if exists,
		/// is closed. The process is done, <i>quietly</i> , without throwing any IOException.
		/// </remarks>
		/// <param name="entity">the entity to consume.</param>
		/// <since>4.2</since>
		public static void ConsumeQuietly(HttpEntity entity)
		{
			try
			{
				Consume(entity);
			}
			catch (IOException)
			{
			}
		}

		/// <summary>
		/// Ensures that the entity content is fully consumed and the content stream, if exists,
		/// is closed.
		/// </summary>
		/// <remarks>
		/// Ensures that the entity content is fully consumed and the content stream, if exists,
		/// is closed.
		/// </remarks>
		/// <param name="entity">the entity to consume.</param>
		/// <exception cref="System.IO.IOException">if an error occurs reading the input stream
		/// 	</exception>
		/// <since>4.1</since>
		public static void Consume(HttpEntity entity)
		{
			if (entity == null)
			{
				return;
			}
			if (entity.IsStreaming())
			{
				InputStream instream = entity.GetContent();
				if (instream != null)
				{
					instream.Close();
				}
			}
		}

		/// <summary>Updates an entity in a response by first consuming an existing entity, then setting the new one.
		/// 	</summary>
		/// <remarks>Updates an entity in a response by first consuming an existing entity, then setting the new one.
		/// 	</remarks>
		/// <param name="response">the response with an entity to update; must not be null.</param>
		/// <param name="entity">the entity to set in the response.</param>
		/// <exception cref="System.IO.IOException">
		/// if an error occurs while reading the input stream on the existing
		/// entity.
		/// </exception>
		/// <exception cref="System.ArgumentException">if response is null.</exception>
		/// <since>4.3</since>
		public static void UpdateEntity(HttpResponse response, HttpEntity entity)
		{
			Args.NotNull(response, "Response");
			Consume(response.GetEntity());
			response.SetEntity(entity);
		}

		/// <summary>Read the contents of an entity and return it as a byte array.</summary>
		/// <remarks>Read the contents of an entity and return it as a byte array.</remarks>
		/// <param name="entity">the entity to read from=</param>
		/// <returns>
		/// byte array containing the entity content. May be null if
		/// <see cref="Org.Apache.Http.HttpEntity.GetContent()">Org.Apache.Http.HttpEntity.GetContent()
		/// 	</see>
		/// is null.
		/// </returns>
		/// <exception cref="System.IO.IOException">if an error occurs reading the input stream
		/// 	</exception>
		/// <exception cref="System.ArgumentException">if entity is null or if content length &gt; Integer.MAX_VALUE
		/// 	</exception>
		public static byte[] ToByteArray(HttpEntity entity)
		{
			Args.NotNull(entity, "Entity");
			InputStream instream = entity.GetContent();
			if (instream == null)
			{
				return null;
			}
			try
			{
				Args.Check(entity.GetContentLength() <= int.MaxValue, "HTTP entity too large to be buffered in memory"
					);
				int i = (int)entity.GetContentLength();
				if (i < 0)
				{
					i = 4096;
				}
				ByteArrayBuffer buffer = new ByteArrayBuffer(i);
				byte[] tmp = new byte[4096];
				int l;
				while ((l = instream.Read(tmp)) != -1)
				{
					buffer.Append(tmp, 0, l);
				}
				return buffer.ToByteArray();
			}
			finally
			{
				instream.Close();
			}
		}

		/// <summary>Obtains character set of the entity, if known.</summary>
		/// <remarks>Obtains character set of the entity, if known.</remarks>
		/// <param name="entity">must not be null</param>
		/// <returns>the character set, or null if not found</returns>
		/// <exception cref="Org.Apache.Http.ParseException">if header elements cannot be parsed
		/// 	</exception>
		/// <exception cref="System.ArgumentException">if entity is null</exception>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1.3) use Org.Apache.Http.Entity.ContentType.GetOrDefault(Org.Apache.Http.HttpEntity)"
			)]
		public static string GetContentCharSet(HttpEntity entity)
		{
			Args.NotNull(entity, "Entity");
			string charset = null;
			if (entity.GetContentType() != null)
			{
				HeaderElement[] values = entity.GetContentType().GetElements();
				if (values.Length > 0)
				{
					NameValuePair param = values[0].GetParameterByName("charset");
					if (param != null)
					{
						charset = param.GetValue();
					}
				}
			}
			return charset;
		}

		/// <summary>Obtains MIME type of the entity, if known.</summary>
		/// <remarks>Obtains MIME type of the entity, if known.</remarks>
		/// <param name="entity">must not be null</param>
		/// <returns>the character set, or null if not found</returns>
		/// <exception cref="Org.Apache.Http.ParseException">if header elements cannot be parsed
		/// 	</exception>
		/// <exception cref="System.ArgumentException">if entity is null</exception>
		/// <since>4.1</since>
		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1.3) use Org.Apache.Http.Entity.ContentType.GetOrDefault(Org.Apache.Http.HttpEntity)"
			)]
		public static string GetContentMimeType(HttpEntity entity)
		{
			Args.NotNull(entity, "Entity");
			string mimeType = null;
			if (entity.GetContentType() != null)
			{
				HeaderElement[] values = entity.GetContentType().GetElements();
				if (values.Length > 0)
				{
					mimeType = values[0].GetName();
				}
			}
			return mimeType;
		}

		/// <summary>
		/// Get the entity content as a String, using the provided default character set
		/// if none is found in the entity.
		/// </summary>
		/// <remarks>
		/// Get the entity content as a String, using the provided default character set
		/// if none is found in the entity.
		/// If defaultCharset is null, the default "ISO-8859-1" is used.
		/// </remarks>
		/// <param name="entity">must not be null</param>
		/// <param name="defaultCharset">character set to be applied if none found in the entity
		/// 	</param>
		/// <returns>
		/// the entity content as a String. May be null if
		/// <see cref="Org.Apache.Http.HttpEntity.GetContent()">Org.Apache.Http.HttpEntity.GetContent()
		/// 	</see>
		/// is null.
		/// </returns>
		/// <exception cref="Org.Apache.Http.ParseException">if header elements cannot be parsed
		/// 	</exception>
		/// <exception cref="System.ArgumentException">if entity is null or if content length &gt; Integer.MAX_VALUE
		/// 	</exception>
		/// <exception cref="System.IO.IOException">if an error occurs reading the input stream
		/// 	</exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public static string ToString(HttpEntity entity, Encoding defaultCharset)
		{
			Args.NotNull(entity, "Entity");
			InputStream instream = entity.GetContent();
			if (instream == null)
			{
				return null;
			}
			try
			{
				Args.Check(entity.GetContentLength() <= int.MaxValue, "HTTP entity too large to be buffered in memory"
					);
				int i = (int)entity.GetContentLength();
				if (i < 0)
				{
					i = 4096;
				}
				Encoding charset = null;
				try
				{
					ContentType contentType = ContentType.Get(entity);
					if (contentType != null)
					{
						charset = contentType.GetCharset();
					}
				}
				catch (UnsupportedCharsetException ex)
				{
					throw new UnsupportedEncodingException(ex.Message);
				}
				if (charset == null)
				{
					charset = defaultCharset;
				}
				if (charset == null)
				{
					charset = HTTP.DefContentCharset;
				}
				StreamReader reader = new InputStreamReader(instream, charset);
				CharArrayBuffer buffer = new CharArrayBuffer(i);
				char[] tmp = new char[1024];
				int l;
				while ((l = reader.Read(tmp)) != -1)
				{
					buffer.Append(tmp, 0, l);
				}
				return buffer.ToString();
			}
			finally
			{
				instream.Close();
			}
		}

		/// <summary>
		/// Get the entity content as a String, using the provided default character set
		/// if none is found in the entity.
		/// </summary>
		/// <remarks>
		/// Get the entity content as a String, using the provided default character set
		/// if none is found in the entity.
		/// If defaultCharset is null, the default "ISO-8859-1" is used.
		/// </remarks>
		/// <param name="entity">must not be null</param>
		/// <param name="defaultCharset">character set to be applied if none found in the entity
		/// 	</param>
		/// <returns>
		/// the entity content as a String. May be null if
		/// <see cref="Org.Apache.Http.HttpEntity.GetContent()">Org.Apache.Http.HttpEntity.GetContent()
		/// 	</see>
		/// is null.
		/// </returns>
		/// <exception cref="Org.Apache.Http.ParseException">if header elements cannot be parsed
		/// 	</exception>
		/// <exception cref="System.ArgumentException">if entity is null or if content length &gt; Integer.MAX_VALUE
		/// 	</exception>
		/// <exception cref="System.IO.IOException">if an error occurs reading the input stream
		/// 	</exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public static string ToString(HttpEntity entity, string defaultCharset)
		{
			return ToString(entity, defaultCharset != null ? Sharpen.Extensions.GetEncoding(defaultCharset
				) : null);
		}

		/// <summary>Read the contents of an entity and return it as a String.</summary>
		/// <remarks>
		/// Read the contents of an entity and return it as a String.
		/// The content is converted using the character set from the entity (if any),
		/// failing that, "ISO-8859-1" is used.
		/// </remarks>
		/// <param name="entity">the entity to convert to a string; must not be null</param>
		/// <returns>String containing the content.</returns>
		/// <exception cref="Org.Apache.Http.ParseException">if header elements cannot be parsed
		/// 	</exception>
		/// <exception cref="System.ArgumentException">if entity is null or if content length &gt; Integer.MAX_VALUE
		/// 	</exception>
		/// <exception cref="System.IO.IOException">if an error occurs reading the input stream
		/// 	</exception>
		/// <exception cref="Sharpen.UnsupportedCharsetException">
		/// Thrown when the named charset is not available in
		/// this instance of the Java virtual machine
		/// </exception>
		public static string ToString(HttpEntity entity)
		{
			return ToString(entity, (Encoding)null);
		}
	}
}
