using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Sharpen;
using Couchbase.Lite;

namespace Couchbase.Lite
{
	public class QueryCompletedEventArgs : EventArgs
	{
        public QueryEnumerator Rows { get; private set; }
        public Exception ErrorInfo { get; private set; }

        public QueryCompletedEventArgs(QueryEnumerator rows, Exception errorInfo) {
            Rows = rows;
            ErrorInfo = errorInfo;
        }
	}
}

