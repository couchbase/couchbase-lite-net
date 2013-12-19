using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using Couchbase.Lite.Internal;
using Sharpen;
using Couchbase.Lite.Util;

namespace Couchbase.Lite {

    public partial class SavedRevision : Revision {

    #region Constructors

        /// <summary>Constructor</summary>
        internal SavedRevision(Document document, RevisionInternal revision)
            : base(document) { RevisionInternal = revision; }

        /// <summary>Constructor</summary>
        internal SavedRevision(Database database, RevisionInternal revision)
            : this(database.GetDocument(revision.GetDocId()), revision) { }

    #endregion
    
    #region Non-public Members

        internal RevisionInternal RevisionInternal { get; private set; }

        private  Boolean CheckedProperties { get; set; }

        internal Boolean LoadProperties()
        {
            try
            {
                var loadRevision = Database.LoadRevisionBody(RevisionInternal, EnumSet.NoneOf<TDContentOptions>());
                if (loadRevision == null)
                {
                    Log.W(Database.Tag, "Couldn't load body/sequence of %s" + this);
                    return false;
                }
                RevisionInternal = loadRevision;
                return true;
            }
            catch (CouchbaseLiteException e)
            {
                throw new RuntimeException(e);
            }
        }

    #endregion

    #region Instance Members

        public override SavedRevision Parent {
            get {
                return Document.GetRevisionFromRev(Database.GetParentRevision(RevisionInternal));
            }
        }

        public override String ParentId {
            get {
                var parRev = Document.Database.GetParentRevision(RevisionInternal);
                if (parRev == null)
                {
                    return null;
                }
                return parRev.GetRevId();
            }
        }

        public override IEnumerable<SavedRevision> RevisionHistory {
            get {
                var revisions = new AList<SavedRevision>();
                var internalRevisions = Database.GetRevisionHistory(RevisionInternal);

                foreach (var internalRevision in internalRevisions)
                {
                    if (internalRevision.GetRevId().Equals(Id))
                    {
                        revisions.AddItem(this);
                    }
                    else
                    {
                        var revision = Document.GetRevisionFromRev(internalRevision);
                        revisions.AddItem(revision);
                    }
                }
                Collections.Reverse(revisions);
                return Collections.UnmodifiableList(revisions);
            }
        }

        public override String Id {
            get {
                return RevisionInternal.GetRevId();
            }
        }

        /// <summary>
        /// Sets if the <see cref="Couchbase.Lite.Revision"/> marks the deletion of its <see cref="Couchbase.Lite.Document"/>.
        /// </summary>
        /// <remarks>
        /// Does this revision mark the deletion of its document?
        /// (In other words, does it have a "_deleted" property?)
        /// </remarks>
        /// <value><c>true</c> if this instance is deletion; otherwise, <c>false</c>.</value>
        public override Boolean IsDeletion {
            get {
                return RevisionInternal.IsDeleted();
            }
        }

        public override IDictionary<String, Object> Properties {
            get {
                IDictionary<string, object> properties = RevisionInternal.GetProperties();
                if (properties == null && !CheckedProperties)
                {
                    if (LoadProperties() == true)
                    {
                        properties = RevisionInternal.GetProperties();
                    }
                    CheckedProperties = true;
                }
                return Collections.UnmodifiableMap(properties);
            }
        }

        public Boolean PropertiesAvailable { get { return RevisionInternal.GetProperties() != null; } }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.UnsavedRevision"/> whose properties and attachments are initially identical to this one.
        /// </summary>
        /// <remarks>
        /// Creates a new mutable child revision whose properties and attachments are initially identical
        /// to this one's, which you can modify and then save.
        /// </remarks>
        /// <returns>The revision.</returns>
        public UnsavedRevision CreateRevision() {
            var newRevision = new UnsavedRevision(Document, this);
            return newRevision;
        }

        /// <summary>Creates and saves a new revision with the given properties.</summary>
        /// <remarks>
        /// Creates and saves a new <see cref="Couchbase.Lite.Revision"/> with the specified properties. To succeed the specified properties must include a '_rev' property whose value maches the current %Revision's% id.
        /// This will fail with a 412 error if the receiver is not the current revision of the document.
        /// </remarks>
        /// <returns>
        /// The new <see cref="Couchbase.Lite.SavedRevision"/>.
        /// </returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public SavedRevision CreateRevision(Dictionary<String, Object> properties) {
            return Document.PutProperties(properties, RevisionInternal.GetRevId());           
        }

        /// <summary>Deletes the document by creating a new deletion-marker revision.</summary>
        /// <remarks>
        /// Creates and saves a new deletion <see cref="Couchbase.Lite.Revision"/> for the associated <see cref="Couchbase.Lite.Document"/>.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public SavedRevision DeleteDocument() { return CreateRevision(null); }

    #endregion
    
    }

    

}
