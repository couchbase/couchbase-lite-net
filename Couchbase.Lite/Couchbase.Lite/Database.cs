using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class Database {

    #region Static Members
        //Properties
        public static CompileFilterDelegate FilterCompiler { get; set; }

    #endregion
    
    #region Instance Members
        //Properties
        public String Name { get { throw new NotImplementedException(); } }

        public Manager Manager { get { throw new NotImplementedException(); } }

        public int DocumentCount { get { throw new NotImplementedException(); } }

        public long LastSequenceNumber { get { throw new NotImplementedException(); } }

        public IEnumerable<Replication> AllReplications { get { throw new NotImplementedException(); } }

        //Methods
        public void Compact() { throw new NotImplementedException(); }

        public void Delete() { throw new NotImplementedException(); }

        public Document GetDocument(String id) { throw new NotImplementedException(); }

        public Document GetExistingDocument(String id) { throw new NotImplementedException(); }

        public Document CreateDocument() { throw new NotImplementedException(); }

        public Dictionary<String, Object> GetExistingLocalDocument(String id) { throw new NotImplementedException(); }

        public void PutLocalDocument(String id, Dictionary<String, Object> properties) { throw new NotImplementedException(); }

        public Boolean DeleteLocalDocument(String id) { throw new NotImplementedException(); }

        public Query CreateAllDocumentsQuery() { throw new NotImplementedException(); }

        public View GetView(String name) { throw new NotImplementedException(); }

        public View GetExistingView(String name) { throw new NotImplementedException(); }

        public ValidateDelegate GetValidation(String name) { throw new NotImplementedException(); }

        public void SetValidation(String name, ValidateDelegate validationDelegate) { throw new NotImplementedException(); }

        public FilterDelegate GetFilter(String name) { throw new NotImplementedException(); }

        public void SetFilter(String name, FilterDelegate filterDelegate) { throw new NotImplementedException(); }

        public void RunAsync(RunAsyncDelegate runAsyncDelegate) { throw new NotImplementedException(); }

        public Boolean RunInTransaction(RunInTransactionDelegate transactionDelegate) { throw new NotImplementedException(); }

        public Replication GetPushReplication(Uri url) { throw new NotImplementedException(); }

        public Replication GetPullReplication(Uri url) { throw new NotImplementedException(); }

        public event EventHandler<DatabaseChangeEventArgs> Change;

    #endregion
       
    #region Constants
        public const string Tag = "Database";

        public const string TagSql = "CBLSQL";
       
    #endregion

    #region Non-Public Instance Members

        internal BlobStoreWriter AttachmentWriter { get; set; }

        private  BlobStore Attachments { get; set; }

        public BlobStore GetAttachments()
        {
            return Attachments;
        }

        public virtual BlobStoreWriter GetAttachmentWriter()
        {
            return new BlobStoreWriter(GetAttachments());
        }



        internal Attachment GetAttachmentForSequence (long sequence, string filename)
        {
            System.Diagnostics.Debug.Assert((sequence > 0));
            System.Diagnostics.Debug.Assert((filename != null));

            throw new NotImplementedException();
//            Cursor cursor = null;
//            string[] args = new string[] { System.Convert.ToString(sequence), filename };
//            try
//            {
//                cursor = database.RawQuery("SELECT key, type FROM attachments WHERE sequence=? AND filename=?"
//                    , args);
//                if (!cursor.MoveToNext())
//                {
//                    throw new CouchbaseLiteException(Status.NotFound);
//                }
//                byte[] keyData = cursor.GetBlob(0);
//                //TODO add checks on key here? (ios version)
//                BlobKey key = new BlobKey(keyData);
//                InputStream contentStream = attachments.BlobStreamForKey(key);
//                if (contentStream == null)
//                {
//                    Log.E(Couchbase.Lite.Database.Tag, "Failed to load attachment");
//                    throw new CouchbaseLiteException(Status.InternalServerError);
//                }
//                else
//                {
//                    Attachment result = new Attachment(contentStream, cursor.GetString(1));
//                    result.SetGZipped(attachments.IsGZipped(key));
//                    return result;
//                }
//            }
//            catch (SQLException)
//            {
//                throw new CouchbaseLiteException(Status.InternalServerError);
//            }
//            finally
//            {
//                if (cursor != null)
//                {
//                    cursor.Close();
//                }
//            }
        }

        internal void RememberAttachmentWriter (BlobStoreWriter writer)
        {
            var digest = writer.MD5DigestString();
            PendingAttachmentsByDigest[digest] = writer;
        }

        IDictionary<String, BlobStoreWriter> _pendingAttachmentsByDigest;
        IDictionary<String, BlobStoreWriter> PendingAttachmentsByDigest {
            get {
                return _pendingAttachmentsByDigest ?? (_pendingAttachmentsByDigest = new Dictionary<String, BlobStoreWriter>());
            }
            set {
                _pendingAttachmentsByDigest = value;
            }
        }

    #endregion
    
    #region Delegates
        public delegate void RunAsyncDelegate(Database database);

        public delegate Boolean RunInTransactionDelegate();

        

        public delegate void ValidateDelegate(Revision newRevision, IValidationContext context);

        public delegate Boolean FilterDelegate(SavedRevision revision, Dictionary<String, Object> filterParams);

        public delegate FilterDelegate CompileFilterDelegate(String source, String language);

    #endregion
    
    #region EventArgs Subclasses
        public class DatabaseChangeEventArgs : EventArgs {

            //Properties
            public Database Source { get { throw new NotImplementedException(); } }

            public Boolean IsExternal { get { throw new NotImplementedException(); } }

            public IEnumerable<DocumentChange> Changes { get { throw new NotImplementedException(); } }

        }

    #endregion
    
    }

    public partial interface IValidationContext {

    #region Instance Members
        //Properties
        SavedRevision CurrentRevision { get; }

        IEnumerable<String> ChangedKeys { get; }

        //Methods
        void Reject();

        void Reject(String message);

        Boolean ValidateChanges(ValidateChangeDelegate changeValidator);

    #endregion
    
    #region Delegates
        

    #endregion
    
    }

    public delegate Boolean ValidateChangeDelegate(String key, Object oldValue, Object newValue);

    
}

