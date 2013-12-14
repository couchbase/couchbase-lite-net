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
using System.Collections.Generic;
using Org.Apache.Http.Util;
using Sharpen;

namespace Org.Apache.Http.Protocol
{
	/// <summary>Maintains a map of objects keyed by a request URI pattern.</summary>
	/// <remarks>
	/// Maintains a map of objects keyed by a request URI pattern.
	/// <br />
	/// Patterns may have three formats:
	/// <ul>
	/// <li><code>*</code></li>
	/// <li><code>*&lt;uri&gt;</code></li>
	/// <li><code>&lt;uri&gt;*</code></li>
	/// </ul>
	/// <br />
	/// This class can be used to resolve an object matching a particular request
	/// URI.
	/// </remarks>
	/// <since>4.0</since>
	public class UriPatternMatcher<T>
	{
		private readonly IDictionary<string, T> map;

		public UriPatternMatcher() : base()
		{
			this.map = new Dictionary<string, T>();
		}

		/// <summary>Registers the given object for URIs matching the given pattern.</summary>
		/// <remarks>Registers the given object for URIs matching the given pattern.</remarks>
		/// <param name="pattern">the pattern to register the handler for.</param>
		/// <param name="obj">the object.</param>
		public virtual void Register(string pattern, T obj)
		{
			lock (this)
			{
				Args.NotNull(pattern, "URI request pattern");
				this.map.Put(pattern, obj);
			}
		}

		/// <summary>Removes registered object, if exists, for the given pattern.</summary>
		/// <remarks>Removes registered object, if exists, for the given pattern.</remarks>
		/// <param name="pattern">the pattern to unregister.</param>
		public virtual void Unregister(string pattern)
		{
			lock (this)
			{
				if (pattern == null)
				{
					return;
				}
				Sharpen.Collections.Remove(this.map, pattern);
			}
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) do not use")]
		public virtual void SetHandlers(IDictionary<string, T> map)
		{
			lock (this)
			{
				Args.NotNull(map, "Map of handlers");
				this.map.Clear();
				this.map.PutAll(map);
			}
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) do not use")]
		public virtual void SetObjects(IDictionary<string, T> map)
		{
			lock (this)
			{
				Args.NotNull(map, "Map of handlers");
				this.map.Clear();
				this.map.PutAll(map);
			}
		}

		[Obsolete]
		[System.ObsoleteAttribute(@"(4.1) do not use")]
		public virtual IDictionary<string, T> GetObjects()
		{
			lock (this)
			{
				return this.map;
			}
		}

		/// <summary>Looks up an object matching the given request path.</summary>
		/// <remarks>Looks up an object matching the given request path.</remarks>
		/// <param name="path">the request path</param>
		/// <returns>object or <code>null</code> if no match is found.</returns>
		public virtual T Lookup(string path)
		{
			lock (this)
			{
				Args.NotNull(path, "Request path");
				// direct match?
				T obj = this.map.Get(path);
				if (obj == null)
				{
					// pattern match?
					string bestMatch = null;
					foreach (string pattern in this.map.Keys)
					{
						if (MatchUriRequestPattern(pattern, path))
						{
							// we have a match. is it any better?
							if (bestMatch == null || (bestMatch.Length < pattern.Length) || (bestMatch.Length
								 == pattern.Length && pattern.EndsWith("*")))
							{
								obj = this.map.Get(pattern);
								bestMatch = pattern;
							}
						}
					}
				}
				return obj;
			}
		}

		/// <summary>Tests if the given request path matches the given pattern.</summary>
		/// <remarks>Tests if the given request path matches the given pattern.</remarks>
		/// <param name="pattern">the pattern</param>
		/// <param name="path">the request path</param>
		/// <returns>
		/// <code>true</code> if the request URI matches the pattern,
		/// <code>false</code> otherwise.
		/// </returns>
		protected internal virtual bool MatchUriRequestPattern(string pattern, string path
			)
		{
			if (pattern.Equals("*"))
			{
				return true;
			}
			else
			{
				return (pattern.EndsWith("*") && path.StartsWith(Sharpen.Runtime.Substring(pattern
					, 0, pattern.Length - 1))) || (pattern.StartsWith("*") && path.EndsWith(Sharpen.Runtime.Substring
					(pattern, 1, pattern.Length)));
			}
		}

		public override string ToString()
		{
			return this.map.ToString();
		}
	}
}
