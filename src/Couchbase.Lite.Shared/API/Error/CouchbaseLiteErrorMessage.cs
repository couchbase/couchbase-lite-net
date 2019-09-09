using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    internal static class CouchbaseLiteErrorMessage
    {
        //Database error messages
        internal const string CreateDBDirectoryFailed = "Unable to create database directory";
        //Database - Copy
        internal const string ResolveDefaultDirectoryFailed = "Failed to resolve a default directory! If you have overriden the default directory, please check it.  Otherwise please file a bug report.";
        //Database - Close
        internal const string CloseDBFailedReplications = "Cannot close the database. Please stop all of the replicators before closing the database.";
        internal const string CloseDBFailedQueryListeners = "Cannot close the database. Please remove all of the query listeners before closing the database.";
        //Database - Delete (Save with deletion =  true)
        internal const string DeleteDBFailedReplications = "Cannot delete the database. Please stop all of the replicators before closing the database.";
        internal const string DeleteDBFailedQueryListeners = "Cannot delete the database. Please remove all of the query listeners before closing the database.";
        internal const string DeleteDocFailedNotSaved = "Cannot delete a document that has not yet been saved";
        //Database - Purge
        internal const string DocumentNotFound = "Document doesn't exist in the database.";
        internal const string DocumentAnotherDatabase = "Cannot operate on a document from another database";
        //Database - Save
        internal const string BlobDifferentDatabase = "A document contains a blob that was saved to a different database; the save operation cannot complete.";
        internal const string BlobContentNull = "No data available to write for install. Please ensure that all blobs in the document have non-null content.";
        //Database - Resolve Conflict
        internal const string ResolvedDocContainsNull = "Resolved document contains a null body";
        internal const string ResolvedDocFailedLiteCore = "LiteCore failed resolving conflict.";
        internal const string ResolvedDocWrongDb = "Resolved document db {0} is different from expected db {1}";
        internal const string DBClosed = "Attempt to perform an operation on a closed database";
        internal const string NoDocumentRevision = "No revision data on the document!";
        //Fragment - Value
        internal const string FragmentPathNotExist = "Specified fragment path does not exist in object, cannot set value";
        //ToCouchbaseObject(object value))
        internal const string InvalidCouchbaseObjType = "{0} is not a valid type. " +
                                                "You may only pass {1} " + //byte, sbyte, short, ushort, int, uint, long, ulong, float, double, bool, DateTimeOffset, Blob,
                                                "or one-dimensional arrays or dictionaries containing the above types";
        //ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) JsonConverter Override method
        internal const string InvalidValueToBeDeserialized = "Non-string or null key in data to be deserialized";
        //Blob - Content
        internal const string BlobContainsNoData = "Blob has no data available";
        //Blob (fileUrl is not a file URL (e.g. it is an HTTP URL))
        internal const string NotFileBasedURL = "{0} must be a file-based URL";
        //Blob ContentStream 
        internal const string BlobReadStreamNotOpen = "Stream is not open";
        //FileLogger Level (returns LogLevel obj) if LogFileConfiguration Config == null
        internal const string CannotSetLogLevel = "Cannot set logging level without a configuration";
        //URLEndpoint URLEndpoint([NotNull]Uri url)
        internal const string InvalidSchemeURLEndpoint = "Invalid scheme for URLEndpoint url ({0}); must be either ws or wss";
        internal const string InvalidEmbeddedCredentialsInURL = "Embedded credentials in a URL (username:password@url) are not allowed; use the BasicAuthenticator class instead";
        //Replicator Start()
        internal const string ReplicatorDisposed = "Replication cannot be started after disposal";
        //Replicator ResetCheckpoint()
        internal const string ReplicatorNotStopped = "Replicator is not stopped.Resetting checkpoint is only allowed when the replicator is in the stopped state.";
        //Query Parameters SetValue
        internal const string QueryParamNotAllowedContainCollections = "Query parameters are not allowed to contain collections";
        //Query Join
        internal const string MissASforJoin = "Missing AS clause for JOIN";
        internal const string MissONforJoin = "Missing ON statement for JOIN";
        //Query QueryExpression
        internal const string ExpressionsMustBeIExpressionOrString = "Expressions must either be IExpression or string";
        internal const string InvalidExpressionValueBetween = "Invalid expression value for expression1 of Between({0})";
        //Query QueryResultSet
        internal const string ResultSetAlreadyEnumerated = "This result set has already been enumerated, please re-run Execute() on the original query";
        //Query
        internal const string ExpressionsMustContainOnePlusElement = "{0} expressions must contain at least one element";
        internal const string DuplicateSelectResultName = "Duplicate select result named {0}";
        internal const string NoAliasInJoin = @"The default database must have an alias in order to use a JOIN statement 
                                                (Make sure your data source uses the As() function)";
        internal const string InvalidQueryDBNull = "Invalid query, Database == null";
        internal const string InvalidQueryMissingSelectOrFrom = "Invalid query, missing Select or From";
        //MArray MDict <--- in the future, we will replace them with LiteCore Mutable Fleece API
        internal const string CannotRemoveItemsFromNonMutableMArray = "Cannot remove items from a non-mutable MArray";
        internal const string CannotRemoveStartingFromIndexLessThan = "Cannot remove starting from an index less than 0 (got {0})";
        internal const string CannotRemoveRangeEndsBeforeItStarts = "Cannot remove a range that ends before it starts (got start= {0}, count = {1} )";
        internal const string RangeEndForRemoveExceedsArrayLength = "Range end for remove exceeds the length of the array(got start = {0}, count = {1} )";
        internal const string CannotSetItemsInNonMutableMArray = "Cannot set items in a non-mutable MArray";
        internal const string CannotClearNonMutableMArray = "Cannot clear a non-mutable MArray";
        internal const string CannotInsertItemsInNonMutableMArray = "Cannot insert items in a non-mutable MArray";
        internal const string CannotClearNonMutableMDict = "Cannot clear a non-mutable MDict";
        internal const string CannotSetItemsInNonMutableInMDict = "Cannot set items in a non-mutable MDict";
    }
}
