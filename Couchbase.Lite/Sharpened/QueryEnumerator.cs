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
using Couchbase.Lite;
using Couchbase.Lite.Internal;
using Sharpen;

namespace Couchbase.Lite
{
	/// <summary>Enumerator on a Query's result rows.</summary>
	/// <remarks>
	/// Enumerator on a Query's result rows.
	/// The objects returned are instances of QueryRow.
	/// </remarks>
	public class QueryEnumerator : IEnumerator<QueryRow>
	{
		private Database database;

		private IList<QueryRow> rows;

		private int nextRow;

		private long sequenceNumber;

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal QueryEnumerator(Database database, IList<QueryRow> rows, long sequenceNumber
			)
		{
			this.database = database;
			this.rows = rows;
			this.sequenceNumber = sequenceNumber;
			// Fill in the rows' database reference now
			foreach (QueryRow row in rows)
			{
				row.SetDatabase(database);
			}
		}

		/// <summary>Constructor</summary>
		[InterfaceAudience.Private]
		internal QueryEnumerator(Couchbase.Lite.QueryEnumerator other)
		{
			this.database = other.database;
			this.rows = other.rows;
			this.sequenceNumber = other.sequenceNumber;
		}

		/// <summary>Gets the number of rows in the QueryEnumerator.</summary>
		/// <remarks>Gets the number of rows in the QueryEnumerator.</remarks>
		[InterfaceAudience.Public]
		public virtual int GetCount()
		{
			return rows.Count;
		}

		/// <summary>Gets the Database's current sequence number at the time the View was generated for the results.
		/// 	</summary>
		/// <remarks>Gets the Database's current sequence number at the time the View was generated for the results.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual long GetSequenceNumber()
		{
			return sequenceNumber;
		}

		/// <summary>
		/// Gets the next QueryRow from the results, or null
		/// if there are no more results.
		/// </summary>
		/// <remarks>
		/// Gets the next QueryRow from the results, or null
		/// if there are no more results.
		/// </remarks>
		[InterfaceAudience.Public]
		public override QueryRow Next()
		{
			if (nextRow >= rows.Count)
			{
				return null;
			}
			return rows[nextRow++];
		}

		/// <summary>Gets the QueryRow at the specified index in the results.</summary>
		/// <remarks>Gets the QueryRow at the specified index in the results.</remarks>
		[InterfaceAudience.Public]
		public virtual QueryRow GetRow(int index)
		{
			return rows[index];
		}

		[InterfaceAudience.Public]
		public override bool Equals(object o)
		{
			if (this == o)
			{
				return true;
			}
			if (o == null || GetType() != o.GetType())
			{
				return false;
			}
			Couchbase.Lite.QueryEnumerator that = (Couchbase.Lite.QueryEnumerator)o;
			if (rows != null ? !rows.Equals(that.rows) : that.rows != null)
			{
				return false;
			}
			return true;
		}

		/// <summary>Required to satisfy java Iterator interface</summary>
		[InterfaceAudience.Public]
		public override bool HasNext()
		{
			return nextRow < rows.Count;
		}

		/// <summary>Required to satisfy java Iterator interface</summary>
		[InterfaceAudience.Public]
		public override void Remove()
		{
			throw new NotSupportedException("QueryEnumerator does not allow remove() to be called"
				);
		}

		/// <summary>True if the database has changed since the view was generated.</summary>
		/// <remarks>True if the database has changed since the view was generated.</remarks>
		[InterfaceAudience.Public]
		public virtual bool IsStale()
		{
			return sequenceNumber < database.GetLastSequenceNumber();
		}

		/// <summary>Resets the enumeration so the next call to -nextObject or -nextRow will return the first row.
		/// 	</summary>
		/// <remarks>Resets the enumeration so the next call to -nextObject or -nextRow will return the first row.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual void Reset()
		{
			nextRow = 0;
		}
	}
}
