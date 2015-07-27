using System;
using System.Threading;
using Couchbase.Lite.Support;

namespace Couchbase.Lite.Internal
{
	internal class AttachmentRequest
	{
		public Attachment attachment { get; set; }
		public ManualResetEvent completeEvent { get; set; }
		public RemoteRequestProgress progress { get; set; }
	}
}

