//  CouchbaseLiteErrorMessage.cs
//
//  Copyright (c) 2019 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    internal static partial class CouchbaseLiteErrorMessage
    {
        internal const string CreateDBDirectoryFailed = "Unable to create database directory.";
        internal const string CloseDBFailedReplications = "Cannot close the database. Please stop all of the replicators before closing the database.";
        internal const string CloseDBFailedQueryListeners = "Cannot close the database. Please remove all of the query listeners before closing the database.";
        internal const string DeleteDBFailedReplications = "Cannot delete the database. Please stop all of the replicators before closing the database.";
        internal const string DeleteDBFailedQueryListeners = "Cannot delete the database. Please remove all of the query listeners before closing the database.";
        internal const string DeleteDocFailedNotSaved = "Cannot delete a document that has not yet been saved.";
        internal const string DocumentNotFound = "The document doesn't exist in the database.";
        internal const string DocumentAnotherDatabase = "Cannot operate on a document from another database.";
        internal const string BlobDifferentDatabase = "A document contains a blob that was saved to a different database. The save operation cannot complete.";
        internal const string BlobContentNull = "No data available to write for install. Please ensure that all blobs in the document have non-null content.";
        internal const string ResolvedDocContainsNull = "Resolved document has a null body.";
        internal const string ResolvedDocFailedLiteCore = "LiteCore failed resolving conflict.";
        internal const string ResolvedDocWrongDb = "Resolved document's database {0} is different from expected database {1}.";
        internal const string DBClosed = "Attempt to perform an operation on a closed database.";
        internal const string NoDocumentRevision = "No revision data on the document!";
        internal const string FragmentPathNotExist = "Specified fragment path does not exist in object; cannot set value.";
        internal const string InvalidCouchbaseObjType = "{0} is not a valid type. You may only pass {1}, Blob, a one-dimensional array or a dictionary whose members are one of the preceding types.";
        internal const string InvalidValueToBeDeserialized = "Non-string or null key in data to be deserialized.";
        internal const string BlobContainsNoData = "Blob has no data available.";
        internal const string NotFileBasedURL = "{0} must be a file-based URL.";
        internal const string BlobReadStreamNotOpen = "Stream is not open.";
        internal const string CannotSetLogLevel = "Cannot set logging level without a configuration.";
        internal const string InvalidSchemeURLEndpoint = "Invalid scheme for URLEndpoint url ({0}). It must be either 'ws:' or 'wss:'.";
        internal const string InvalidEmbeddedCredentialsInURL = "Embedded credentials in a URL (username:password@url) are not allowed. Use the BasicAuthenticator class instead.";
        internal const string ReplicatorNotStopped = "Replicator is not stopped. Resetting checkpoint is only allowed when the replicator is in the stopped state.";
        internal const string QueryParamNotAllowedContainCollections = "Query parameters are not allowed to contain collections.";
        internal const string MissASforJoin = "Missing AS clause for JOIN.";
        internal const string MissONforJoin = "Missing ON statement for JOIN.";
        internal const string ExpressionsMustBeIExpressionOrString = "Expressions must either be {0} or String.";
        internal const string InvalidExpressionValueBetween = "Invalid expression value for expression of Between({0}).";
        internal const string ResultSetAlreadyEnumerated = "This result set has already been enumerated. Please re-execute the original query.";
        internal const string ExpressionsMustContainOnePlusElement = "{0} expressions must contain at least one element.";
        internal const string DuplicateSelectResultName = "Duplicate select result named {0}.";
        internal const string NoAliasInJoin = "The default database must have an alias in order to use a JOIN statement (Make sure your data source uses the As() function).";
        internal const string InvalidQueryDBNull = "Invalid query: The database is null.";
        internal const string InvalidQueryMissingSelectOrFrom = "Invalid query: missing Select or From.";
        internal const string NoDocEditInReplicationFilter = "Documents from a replication filter cannot be edited.";
    }
}

