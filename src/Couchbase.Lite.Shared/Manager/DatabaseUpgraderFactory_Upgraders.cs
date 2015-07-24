//
//  DatabaseUpgraderFactory_Upgraders.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using SQLitePCL;
using Couchbase.Lite.Util;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using Couchbase.Lite.Internal;
using Sharpen;
using System.Diagnostics;
using System.IO;

namespace Couchbase.Lite.Db
{
    internal static partial class DatabaseUpgraderFactory
    {
        private class NoopUpgrader : IDatabaseUpgrader
        {
            private readonly Database _db;

            public int NumDocs
            {
                get {
                    return _db.DocumentCount;
                }
            }

            public int NumRevs
            {
                get {
                    return -1;
                }
            }

            public bool CanRemoveOldAttachmentsDir { get; set; }

            public NoopUpgrader(Database db, string path) 
            {
                _db = db;
            }

            #region IDatabaseUpgrader

            public Status Import()
            {
                return new Status(StatusCode.Ok);
            }

            public void Backout()
            {
                // no-op
            }

            #endregion

        }

        private class v1_upgrader : IDatabaseUpgrader
        {
            private const string TAG = "v1_upgrader";
            private readonly Database _db;
            private readonly string _path;
            private sqlite3 _sqlite;

            public int NumDocs { get; private set; }

            public int NumRevs { get; private set; }

            public bool CanRemoveOldAttachmentsDir { get; set; }

            public v1_upgrader(Database db, string path)
            {
                _db = db;
                _path = path;
                CanRemoveOldAttachmentsDir = true;
            }

            private static Status SqliteErrToStatus(int sqliteErr)
            {
                if (sqliteErr == raw.SQLITE_OK || sqliteErr == raw.SQLITE_DONE) {
                    return new Status(StatusCode.Ok);
                }

                Log.W(TAG, "Upgrade failed: SQLite error {0}", sqliteErr);
                switch (sqliteErr) {
                    case raw.SQLITE_NOTADB:
                        return new Status(StatusCode.BadRequest);
                    case raw.SQLITE_PERM:
                        return new Status(StatusCode.Forbidden);
                    case raw.SQLITE_CORRUPT:
                    case raw.SQLITE_IOERR:
                        return new Status(StatusCode.CorruptError);
                    case raw.SQLITE_CANTOPEN:
                        return new Status(StatusCode.NotFound);
                    default:
                        return new Status(StatusCode.DbError);
                }
            }

            private static int CollateRevIDs(object user_data, string s1, string s2)
            {
                throw new NotImplementedException();
            }

            private Status PrepareSQL(ref sqlite3_stmt stmt, string sql)
            {
                int err;
                if (stmt != null) {
                    err = raw.sqlite3_reset(stmt);
                } else {
                    err = raw.sqlite3_prepare_v2(_sqlite, sql, out stmt);
                }

                if (err != 0) {
                    Log.W(TAG, "Couldn't compile SQL `{0}` : {1}", sql, raw.sqlite3_errmsg(_sqlite));
                }

                return SqliteErrToStatus(err);
            }

            private Status ImportDoc(string docID, long docNumericID)
            {
                // CREATE TABLE revs (
                //  sequence INTEGER PRIMARY KEY AUTOINCREMENT,
                //  doc_id INTEGER NOT NULL REFERENCES docs(doc_id) ON DELETE CASCADE,
                //  revid TEXT NOT NULL COLLATE REVID,
                //  parent INTEGER REFERENCES revs(sequence) ON DELETE SET NULL,
                //  current BOOLEAN,
                //  deleted BOOLEAN DEFAULT 0,
                //  json BLOB,
                //  no_attachments BOOLEAN,
                //  UNIQUE (doc_id, revid) );
                sqlite3_stmt revQuery = null;
                Status status = PrepareSQL(ref revQuery, "SELECT sequence, revid, parent, current, deleted, json" +
                                " FROM revs WHERE doc_id=? ORDER BY sequence");
                if (status.IsError) {
                    return status;
                }

                raw.sqlite3_bind_int64(revQuery, 1, docNumericID);

                var tree = new Dictionary<long, IList<object>>();

                int err;
                while (raw.SQLITE_ROW == (err = raw.sqlite3_step(revQuery))) {
                    long sequence = raw.sqlite3_column_int64(revQuery, 0);
                    string revID = raw.sqlite3_column_text(revQuery, 1);
                    long parentSeq = raw.sqlite3_column_int64(revQuery, 2);
                    bool current = raw.sqlite3_column_int(revQuery, 3) != 0;

                    if (current) {
                        // Add a leaf revision:
                        bool deleted = raw.sqlite3_column_int(revQuery, 4) != 0;
                        IEnumerable<byte> json = raw.sqlite3_column_blob(revQuery, 5);
                        if (json == null) {
                            json = Encoding.UTF8.GetBytes("{}");
                        }

                        var nuJson = new List<byte>(json);
                        status = AddAttachmentsToSequence(sequence, nuJson);
                        if (status.IsError) {
                            raw.sqlite3_finalize(revQuery);
                            return status;
                        }

                        json = nuJson;
                        RevisionInternal rev = new RevisionInternal(docID, revID, deleted);
                        rev.SetJson(json);

                        var history = new List<string>();
                        history.Add(revID);
                        while (parentSeq > 0) {
                            var ancestor = tree.Get(parentSeq);
                            Debug.Assert(ancestor != null, String.Format("Couldn't find parent sequence of {0} (doc {1})", parentSeq, docID));
                            history.Add((string)ancestor[0]);
                            parentSeq = (long)ancestor[1];
                        }

                        Log.D(TAG, "Upgrading doc {0} history {1}", rev, Manager.GetObjectMapper().WriteValueAsString(history));
                        try {
                            _db.ForceInsert(rev, history, null, status);
                        } catch (CouchbaseLiteException e) {
                            status = e.CBLStatus;
                        }

                        if (status.IsError) {
                            raw.sqlite3_finalize(revQuery);
                            return status;
                        }

                        NumRevs++;
                    } else {
                        tree[sequence] = new List<object> { revID, parentSeq };
                    }
                }

                raw.sqlite3_finalize(revQuery);
                ++NumDocs;
                return SqliteErrToStatus(err);
            }

            private Status AddAttachmentsToSequence(long sequence, List<byte> json)
            {
                // CREATE TABLE attachments (
                //  sequence INTEGER NOT NULL REFERENCES revs(sequence) ON DELETE CASCADE,
                //  filename TEXT NOT NULL,
                //  key BLOB NOT NULL,
                //  type TEXT,
                //  length INTEGER NOT NULL,
                //  revpos INTEGER DEFAULT 0,
                //  encoding INTEGER DEFAULT 0,
                //  encoded_length INTEGER );
                sqlite3_stmt attQuery = null;
                Status status = PrepareSQL(ref attQuery, "SELECT filename, key, type, length,"
                                + " revpos, encoding, encoded_length FROM attachments WHERE sequence=?");
                if (status.IsError) {
                    return status;
                }

                raw.sqlite3_bind_int64(attQuery, 1, sequence);

                var attachments = new Dictionary<string, object>();

                int err;
                while (raw.SQLITE_ROW == (err = raw.sqlite3_step(attQuery))) {
                    string name = raw.sqlite3_column_text(attQuery, 0);
                    var key = raw.sqlite3_column_blob(attQuery, 1);
                    string mimeType = raw.sqlite3_column_text(attQuery, 2);
                    long length = raw.sqlite3_column_int64(attQuery, 3);
                    int revpos = raw.sqlite3_column_int(attQuery, 4);
                    int encoding = raw.sqlite3_column_int(attQuery, 5);
                    long encodedLength = raw.sqlite3_column_int64(attQuery, 6);

                    if (key.Length != SHA1.Create().HashSize / 8) {
                        raw.sqlite3_finalize(attQuery);
                        return new Status(StatusCode.CorruptError);
                    }

                    var blobKey = new BlobKey(key);
                    var att = new NonNullDictionary<string, object> {
                        { "type", mimeType },
                        { "digest", blobKey.Base64Digest() },
                        { "length", length },
                        { "revpos", revpos },
                        { "follows", true },
                        { "encoding", encoding != 0 ? "gzip" : null },
                        { "encoded_length", encoding != 0 ? (object)encodedLength : null }
                    };

                    attachments[name] = att;
                }

                raw.sqlite3_finalize(attQuery);
                if (err != raw.SQLITE_DONE) {
                    return SqliteErrToStatus(err);
                }

                if (attachments.Count > 0) {
                    // Splice attachment JSON into the document JSON:
                    var attJson = Manager.GetObjectMapper().WriteValueAsBytes(new Dictionary<string, object> { { "_attachments", attachments } });

                    if (json.Count > 2) {
                        json.Insert(json.Count - 1, (byte)',');
                    }

                    json.InsertRange(json.Count - 1, attJson.Skip(1).Take(attJson.Count() - 2));
                }

                return new Status(StatusCode.Ok);
            }

            private Status ImportLocalDocs()
            {
                // CREATE TABLE localdocs (
                //  docid TEXT UNIQUE NOT NULL,
                //  revid TEXT NOT NULL COLLATE REVID,
                //  json BLOB );

                sqlite3_stmt localQuery = null;
                Status status = PrepareSQL(ref localQuery, "SELECT docid, json FROM localdocs");
                if (status.IsError) {
                    return status;
                }

                int err;
                while (raw.SQLITE_ROW == (err = raw.sqlite3_step(localQuery))) {
                    string docID = raw.sqlite3_column_text(localQuery, 0);
                    var data = raw.sqlite3_column_blob(localQuery, 1);
                    IDictionary<string, object> props = null;
                    try {
                        props = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(data);
                    } catch (CouchbaseLiteException) {
                    }

                    Log.D(TAG, "Upgrading local doc '{0}'", docID);
                    if (props != null) {
                        try {
                            _db.PutLocalDocument(docID, props);
                        } catch(CouchbaseLiteException e) {
                            Log.W(TAG, "Couldn't import local doc '{0}': {1}", docID, e.CBLStatus);
                        }
                    }
                }

                raw.sqlite3_finalize(localQuery);
                return SqliteErrToStatus(err);
            }

            private Status ImportInfo()
            {
                //TODO: Revisit this once pluggable storage is finished
                // CREATE TABLE info (key TEXT PRIMARY KEY, value TEXT);
                sqlite3_stmt infoQuery = null;
                var status = PrepareSQL(ref infoQuery, "SELECT key, value FROM info");
                if (status.IsError) {
                    return status;
                }

                int err = raw.sqlite3_step(infoQuery);
                if (err != raw.SQLITE_ROW) {
                    raw.sqlite3_finalize(infoQuery);
                    return SqliteErrToStatus(err);
                }

                string privateUUID = null, publicUUID = null;
                var key = raw.sqlite3_column_text(infoQuery, 0);
                var val = raw.sqlite3_column_text(infoQuery, 1);
                if (key.Equals("privateUUID")) {
                    privateUUID = val;
                } else if (key.Equals("publicUUID")) {
                    publicUUID = val;
                }

                err = raw.sqlite3_step(infoQuery);
                if (err != raw.SQLITE_ROW) {
                    raw.sqlite3_finalize(infoQuery);
                    return SqliteErrToStatus(err);
                }

                key = raw.sqlite3_column_text(infoQuery, 0);
                val = raw.sqlite3_column_text(infoQuery, 1);
                if (key.Equals("privateUUID")) {
                    privateUUID = val;
                } else if (key.Equals("publicUUID")) {
                    publicUUID = val;
                }

                raw.sqlite3_finalize(infoQuery);
                if (publicUUID == null || privateUUID == null) {
                    return new Status(StatusCode.CorruptError);
                }

                if (!_db.ReplaceUUIDs(privateUUID, publicUUID)) {
                    return new Status(StatusCode.DbError);
                }

                return new Status(StatusCode.Ok);
            }

            private Status MoveAttachmentsDir()
            {
                var oldAttachmentsPath = Path.Combine(Path.ChangeExtension(_db.Path, null), "attachments");
                var newAttachmentsPath = _db.AttachmentStorePath;
                if (oldAttachmentsPath.Equals(newAttachmentsPath)) {
                    Log.D(TAG, "Skip moving the attachments folder as no path change ('{0}' vs '{1}').", oldAttachmentsPath, newAttachmentsPath);
                    return new Status(StatusCode.Ok);
                }

                if (!Directory.Exists(oldAttachmentsPath)) {
                    return new Status(StatusCode.Ok);
                }

                Log.D(TAG, "Moving {0} to {1}", oldAttachmentsPath, newAttachmentsPath);
                Directory.Delete(newAttachmentsPath, true);
                Directory.CreateDirectory(newAttachmentsPath);

                try {
                    if (CanRemoveOldAttachmentsDir) {
                        // Need to ensure upper case
                        foreach(var file in Directory.GetFiles(oldAttachmentsPath)) {
                            var filename = Path.GetFileNameWithoutExtension(file).ToUpperInvariant();
                            var extension = Path.GetExtension(file);
                            var newPath = Path.Combine(newAttachmentsPath, filename + extension);
                            File.Move(file, newPath);
                        }

                        Directory.Delete(Path.ChangeExtension(_db.Path, null), true);
                    } else {
                        DirectoryCopy(oldAttachmentsPath, newAttachmentsPath);
                    }
                } catch(IOException e) {
                    if (!(e is DirectoryNotFoundException)) {
                        Log.W(TAG, "Upgrade failed:  Couldn't move attachments", e);
                        return new Status(StatusCode.Exception);
                    }
                }

                return new Status(StatusCode.Ok);
            }

            private static void DirectoryCopy(string sourceDirName, string destDirName)
            {
                // Get the subdirectories for the specified directory.
                DirectoryInfo dir = new DirectoryInfo(sourceDirName);
                DirectoryInfo[] dirs = dir.GetDirectories();

                // If the destination directory doesn't exist, create it. 
                if (!Directory.Exists(destDirName)) {
                    Directory.CreateDirectory(destDirName);
                }

                // Get the files in the directory and copy them to the new location.
                FileInfo[] files = dir.GetFiles();
                foreach (FileInfo file in files) {
                    string temppath = Path.Combine(destDirName, file.Name);
                    file.CopyTo(temppath, false);
                }
                    
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath);
                }
            }

            private static bool MoveSqliteFiles(string path, string destPath) {
                try {
                    if(File.Exists(path)) {
                        File.Move(path, destPath);
                    }

                    if(File.Exists(path + "-wal")) {
                        File.Move(path + "-wal", destPath + "-wal");
                    }

                    if(File.Exists(path + "-shm")) {
                        File.Move(path + "-shm", destPath + "-shm");
                    }
                } catch(IOException) {
                    return false;
                }

                return true;
            }

            #region IDatabaseUpgrader

            public Status Import()
            {
                int version = DatabaseUpgraderFactory.SchemaVersion(_path);
                if (version < 0) {
                    Log.W(TAG, "Upgrade failed: Cannot determine database schema version");
                    return new Status(StatusCode.CorruptError);
                }

                // Open source (SQLite) database:
                var err = raw.sqlite3_open_v2(_path, out _sqlite, raw.SQLITE_OPEN_READWRITE, null);
                if (err > 0) {
                    return SqliteErrToStatus(err);
                }

                raw.sqlite3_create_collation(_sqlite, "JSON", raw.SQLITE_UTF8, CollateRevIDs);
                sqlite3_stmt stmt = null;
                var status = PrepareSQL(ref stmt, "SELECT name FROM sqlite_master WHERE type='table' AND name='maps'");

                err = raw.sqlite3_step(stmt);
                if (err == raw.SQLITE_ROW) {
                    sqlite3_stmt stmt2 = null;
                    status = PrepareSQL(ref stmt2, "SELECT * FROM maps");
                    while ((err = raw.sqlite3_step(stmt2)) == raw.SQLITE_ROW) {
                        int viewId = raw.sqlite3_column_int(stmt2, 0);
                        sqlite3_stmt stmt3 = null;
                        status = PrepareSQL(ref stmt3, "CREATE TABLE IF NOT EXISTS maps_" + viewId + 
                            " (sequence INTEGER NOT NULL REFERENCES revs(sequence) ON DELETE CASCADE," +
                            "key TEXT NOT NULL COLLATE JSON," +
                            "value TEXT," +
                            "fulltext_id INTEGER, " +
                            "bbox_id INTEGER, " +
                            "geokey BLOB)");
                        raw.sqlite3_step(stmt3);
                        raw.sqlite3_finalize(stmt3);
                        stmt3 = null;

                        var sequence = raw.sqlite3_column_int64(stmt2, 1);
                        var key = raw.sqlite3_column_text(stmt2, 2);
                        var value = raw.sqlite3_column_text(stmt2, 3);

                        var insertSql = String.Format("INSERT INTO maps_{0} (sequence, key, value) VALUES ({1}, {2}, {3}",
                                            viewId, sequence, key, value);
                        
                        status = PrepareSQL(ref stmt3, insertSql);
                        raw.sqlite3_step(stmt3);
                        raw.sqlite3_finalize(stmt3);
                    }

                    raw.sqlite3_finalize(stmt2);
                    stmt2 = null;
                    status = PrepareSQL(ref stmt2, "DROP TABLE maps");
                    raw.sqlite3_step(stmt2);
                    raw.sqlite3_finalize(stmt2);
                }

                raw.sqlite3_finalize(stmt);
                raw.sqlite3_close(_sqlite);
                if (err != raw.SQLITE_DONE) {
                    return SqliteErrToStatus(err);
                }

                if (version >= 101) {
                    return new Status(StatusCode.Ok);
                }

                Log.D(TAG, "Upgrading database v1.0 ({0}) to v1.1 at {1} ...", version, _path);

                // Rename the old database file for migration:
                var destPath = Path.ChangeExtension(_path, Manager.DatabaseSuffix + "-mgr");
                if (!MoveSqliteFiles(_path, destPath)) {
                    Log.W(TAG, "Upgrade failed: Cannot rename the old sqlite files");
                    MoveSqliteFiles(destPath, _path);

                    return new Status(StatusCode.InternalServerError);
                }

                err = raw.sqlite3_open_v2(destPath, out _sqlite, raw.SQLITE_OPEN_READONLY, null);
                if (err > 0) {
                    return SqliteErrToStatus(err);
                }

                raw.sqlite3_create_collation(_sqlite, "REVID", raw.SQLITE_UTF8, CollateRevIDs);

                // Open destination database:
                if (!_db.Open()) {
                    Log.W(TAG, "Upgrade failed: Couldn't open new db");
                    return new Status(StatusCode.DbError);
                }

                status = MoveAttachmentsDir();
                if (status.IsError) {
                    return status;
                }

                // Upgrade documents:
                // CREATE TABLE docs (doc_id INTEGER PRIMARY KEY, docid TEXT UNIQUE NOT NULL);
                sqlite3_stmt docQuery = null;
                status = PrepareSQL(ref docQuery, "SELECT doc_id, docid FROM docs");
                if (status.IsError) {
                    return status;
                }

                _db.RunInTransaction(() =>
                {
                    int transactionErr;
                    int count = 0;
                    while(raw.SQLITE_ROW == (transactionErr = raw.sqlite3_step(docQuery))) {
                        long docNumericID = raw.sqlite3_column_int64(docQuery, 0);
                        string docID = raw.sqlite3_column_text(docQuery, 1);
                        Status transactionStatus = ImportDoc(docID, docNumericID);
                        if(transactionStatus.IsError) {
                            status = transactionStatus;
                            return false;
                        }
                        
                        if((++count % 1000) == 0) {
                            Log.I(TAG, "Migrated {0} documents", count);
                        }
                    }

                    status = SqliteErrToStatus(transactionErr);
                    return transactionErr == raw.SQLITE_DONE;
                });

                raw.sqlite3_finalize(docQuery);
                if (status.IsError) {
                    return status;
                }

                status = ImportLocalDocs();
                if (status.IsError) {
                    return status;
                }

                status = ImportInfo();
                if (status.IsError) {
                    return status;
                }

                err = raw.sqlite3_close(_sqlite);
                _sqlite = null;
                File.Delete(destPath);
                File.Delete(destPath + "-wal");
                File.Delete(destPath + "-shm");

                return status;
            }

            public void Backout()
            {
                // Move attachments dir back to the old path
                var newAttachmentsPath = _db.AttachmentStorePath;
                if (Directory.Exists(newAttachmentsPath)) {
                    var oldAttachmentsPath = Path.ChangeExtension(_db.Path, null) + Path.PathSeparator + "attachments";
                    if (CanRemoveOldAttachmentsDir) {
                        try {
                            Directory.Move(newAttachmentsPath, oldAttachmentsPath);
                        } catch(IOException) {
                        }
                    }
                }

                _db.Delete();
            }

            #endregion

        }
    }
}

