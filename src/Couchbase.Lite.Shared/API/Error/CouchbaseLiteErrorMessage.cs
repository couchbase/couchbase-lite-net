using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    internal static class CouchbaseLiteErrorMessage
    {
        //Database error messages
        internal static string INCORRECT_ENCRYPTION_KEY = "The provided encryption key was incorrect.";
        internal static string CREATE_DB_DIRECTORY_FAILED = "Unable to create database directory";
        //Database - Copy
        internal static string RESOLVE_DEFAULT_DIRECTORY_FAILED = "Failed to resolve a default directory! If you have overriden the IDefaultDirectoryResolver interface, please check it.  Otherwise please file a bug report.";
        internal static string INVALID_PATH = "Path.Combine failed to return a non-null value!";
        //Database - Close
        internal static string CLOSE_DB_FAILED_REPLICATORS = "Cannot close the database. Please stop all of the replicators before closing the database.";
        internal static string CLOSE_DB_FAILED_QUERY_LISTENERS = "Cannot close the database. Please remove all of the query listeners before closing the database.";
        //Database - Delete (Save with deletion =  true)
        internal static string DELETE_DB_FAILED_REPLICATORS = "Cannot delete the database. Please stop all of the replicators before closing the database.";
        internal static string DELETE_DB_FAILED_QUERY_LISTENERS = "Cannot delete the database. Please remove all of the query listeners before closing the database.";
        internal static string DELETE_DOC_FAILED_NOT_SAVED = "Cannot delete a document that has not yet been saved";
        //Database - Purge
        internal static string DOCUMENT_NOT_FOUND = "Document doesn't exist in the database.";
        internal static string DOCUMENT_ANOTHER_DATABASE = "Cannot operate on a document from another database";
        //Database - Save
        internal static string BLOB_DIFFERENT_DATABASE = "A document contains a blob that was saved to a different database; the save operation cannot complete.";
        internal static string BLOB_CONTENT_NULL = "No data available to write for install. Please ensure that all blobs in the document have non-null content.";
        //Database - Resolve Conflict
        internal static string RESOLVED_DOC_CONTENT_NULL = "Resolved document contains a null body";
        internal static string RESOLVED_DOC_FAILED_LITECORE = "LiteCore failed resolving conflict.";
        internal static string RESOLVED_DOC_WRONG_DB = "Resolved document db {0} is different from expected db {1}";
        internal static string DB_CLOSED = "Attempt to perform an operation on a closed database";
    }
}
