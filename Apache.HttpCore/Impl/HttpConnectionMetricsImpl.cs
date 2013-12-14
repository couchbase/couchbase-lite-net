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

using System.Collections.Generic;
using Org.Apache.Http;
using Org.Apache.Http.IO;
using Sharpen;

namespace Org.Apache.Http.Impl
{
	/// <summary>
	/// Default implementation of the
	/// <see cref="Org.Apache.Http.HttpConnectionMetrics">Org.Apache.Http.HttpConnectionMetrics
	/// 	</see>
	/// interface.
	/// </summary>
	/// <since>4.0</since>
	public class HttpConnectionMetricsImpl : HttpConnectionMetrics
	{
		public const string RequestCount = "http.request-count";

		public const string ResponseCount = "http.response-count";

		public const string SentBytesCount = "http.sent-bytes-count";

		public const string ReceivedBytesCount = "http.received-bytes-count";

		private readonly HttpTransportMetrics inTransportMetric;

		private readonly HttpTransportMetrics outTransportMetric;

		private long requestCount = 0;

		private long responseCount = 0;

		/// <summary>The cache map for all metrics values.</summary>
		/// <remarks>The cache map for all metrics values.</remarks>
		private IDictionary<string, object> metricsCache;

		public HttpConnectionMetricsImpl(HttpTransportMetrics inTransportMetric, HttpTransportMetrics
			 outTransportMetric) : base()
		{
			this.inTransportMetric = inTransportMetric;
			this.outTransportMetric = outTransportMetric;
		}

		public virtual long GetReceivedBytesCount()
		{
			if (this.inTransportMetric != null)
			{
				return this.inTransportMetric.GetBytesTransferred();
			}
			else
			{
				return -1;
			}
		}

		public virtual long GetSentBytesCount()
		{
			if (this.outTransportMetric != null)
			{
				return this.outTransportMetric.GetBytesTransferred();
			}
			else
			{
				return -1;
			}
		}

		public virtual long GetRequestCount()
		{
			return this.requestCount;
		}

		public virtual void IncrementRequestCount()
		{
			this.requestCount++;
		}

		public virtual long GetResponseCount()
		{
			return this.responseCount;
		}

		public virtual void IncrementResponseCount()
		{
			this.responseCount++;
		}

		public virtual object GetMetric(string metricName)
		{
			object value = null;
			if (this.metricsCache != null)
			{
				value = this.metricsCache.Get(metricName);
			}
			if (value == null)
			{
				if (RequestCount.Equals(metricName))
				{
					value = Sharpen.Extensions.ValueOf(requestCount);
				}
				else
				{
					if (ResponseCount.Equals(metricName))
					{
						value = Sharpen.Extensions.ValueOf(responseCount);
					}
					else
					{
						if (ReceivedBytesCount.Equals(metricName))
						{
							if (this.inTransportMetric != null)
							{
								return Sharpen.Extensions.ValueOf(this.inTransportMetric.GetBytesTransferred());
							}
							else
							{
								return null;
							}
						}
						else
						{
							if (SentBytesCount.Equals(metricName))
							{
								if (this.outTransportMetric != null)
								{
									return Sharpen.Extensions.ValueOf(this.outTransportMetric.GetBytesTransferred());
								}
								else
								{
									return null;
								}
							}
						}
					}
				}
			}
			return value;
		}

		public virtual void SetMetric(string metricName, object obj)
		{
			if (this.metricsCache == null)
			{
				this.metricsCache = new Dictionary<string, object>();
			}
			this.metricsCache.Put(metricName, obj);
		}

		public virtual void Reset()
		{
			if (this.outTransportMetric != null)
			{
				this.outTransportMetric.Reset();
			}
			if (this.inTransportMetric != null)
			{
				this.inTransportMetric.Reset();
			}
			this.requestCount = 0;
			this.responseCount = 0;
			this.metricsCache = null;
		}
	}
}
