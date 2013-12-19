using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite.Internal;

namespace Couchbase.Lite {

    struct AttachmentMetadataKeys {
        internal static readonly String ContentType = "content_type";
        internal static readonly String Length = "length";
        internal static readonly String Follows = "follows";
        internal static readonly String Digest = "digest";
    }

    public partial class Attachment {

        #region Constants

        const int DefaultStreamChunkSize = 8192;

        #endregion

        #region Constructors

        internal Attachment(Stream contentStream, string contentType)
        {
            Metadata = new Dictionary<String, Object> {
                { AttachmentMetadataKeys.ContentType, contentType },
                { AttachmentMetadataKeys.Follows, true }
            };

            Body = contentStream;
        }

        internal Attachment(Revision revision, String name, IDictionary<String, Object> metadata)
        {
            Revision = revision;
            Name = name;
            Metadata = metadata;
            Compressed = false;
        }
        #endregion

        #region Non-Public Instance Members

        /// <summary>
        /// Content stream is gzip encoded.
        /// </summary>
        /// <value><c>true</c> if compressed; otherwise, <c>false</c>.</value>
        internal bool Compressed { get; set; }

        protected Stream Body { get; set; }

        /// <summary>
        /// Goes through an _attachments dictionary and replaces any values that are Attachment objects
        /// with proper JSON metadata dicts.
        /// </summary>
        /// <remarks>
        /// Goes through an _attachments dictionary and replaces any values that are Attachment objects
        /// with proper JSON metadata dicts. It registers the attachment bodies with the blob store and sets
        /// the metadata 'digest' and 'follows' properties accordingly.
        /// </remarks>
        internal static IDictionary<string, object> InstallAttachmentBodies(IDictionary<String, Object> attachments, Database database)
        {
            var updatedAttachments = new Dictionary<string, object>();
            foreach (string name in attachments.Keys)
            {
                object value;
                attachments.TryGetValue(name, out value);

                if (value is Attachment)
                {
                    var attachment = (Attachment)value;
                    var metadataMutable = new Dictionary<string, object>();
                    metadataMutable.Concat(attachment.Metadata);
                    var body = attachment.Body;
                    if (body != null)
                    {
                        // Copy attachment body into the database's blob store:
                        var writer = BlobStoreWriterForBody(body, database);
                        metadataMutable["length"] = writer.GetLength();
                        metadataMutable["digest"] = writer.MD5DigestString();
                        metadataMutable["follows"] = true;
                        database.RememberAttachmentWriter(writer);
                    }
                    updatedAttachments[name] = metadataMutable;
                }
                else
                {
                    if (value is AttachmentInternal)
                    {
                        throw new ArgumentException("AttachmentInternal objects not expected here.  Could indicate a bug"
                        );
                    }
                }
            }
            return updatedAttachments;
        }

        internal static BlobStoreWriter BlobStoreWriterForBody(Stream body, Database database)
        {
            var writer = database.AttachmentWriter;
            writer.Read(body);
            writer.Finish();
            return writer;
        }

        #endregion

        #region Instance Members

        /// <summary>Get the owning document revision.</summary>
        public Revision Revision { get; internal set; }

        /// <summary>Get the owning document.</summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public Document Document {
            get {
                if (Revision == null)
                    throw new CouchbaseLiteException("Revision must not be null.");
                return Revision.Document;
            } 
        }

        /// <summary>Get the filename.</summary>
        public String Name { get ; internal set; }

        /// <summary>Get the MIME type of the contents.</summary>
        public String ContentType {
            get {
                Object contentType;

                if (!Metadata.TryGetValue(AttachmentMetadataKeys.ContentType, out contentType))
                    throw new CouchbaseLiteException("Metadata must contain a key-value pair for {0}.", AttachmentMetadataKeys.ContentType);

                if (!(contentType is String))
                    throw new CouchbaseLiteException("The {0} key in Metadata must contain a string value.", AttachmentMetadataKeys.ContentType);

                return (String)contentType; 
            }
        }

        /// <summary>Get the content (aka 'body') data.</summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public Stream ContentStream { 
            get {
                if (Body != null) {
                    Body.Reset();
                    return Body;
                }

                if (Revision == null)
                    throw new CouchbaseLiteException("Revision must not be null when retrieving attachment content");

                if (Name == null)
                    throw new CouchbaseLiteException("Name must not be null when retrieving attachment content");

                var attachment = Revision.Database.GetAttachmentForSequence(
                    Revision.Sequence,
                    Name
                    );

                if (attachment == null)
                    throw new CouchbaseLiteException("Could not retrieve an attachment for revision sequence {0}.", Revision.Sequence);

                Body = attachment.ContentStream;
                Body.Reset();

                return Body;
            }
        }

        /// <summary>Get the content (aka 'body') data.</summary>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public IEnumerable<Byte> Content { 
            get {
                var stream = ContentStream;
                var bytes = new byte[stream.Length];

                ContentStream.Reset();

                for(var i = 0; i < stream.Length; i++) {
                    stream.Read(bytes, i, DefaultStreamChunkSize);
                }

                return bytes;
            }
        }

        /// <summary>Get the length in bytes of the contents.</summary>
        public Int64 Length {
            get {
                Object length;
                var success = Metadata.TryGetValue(AttachmentMetadataKeys.Length, out length);
                return success ? (Int64)length : 0;
            }
        }

        /// <summary>The CouchbaseLite metadata about the attachment, that lives in the document.
        public IDictionary<String, Object> Metadata { get ; private set; }

        #endregion
        
    }

}

