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
#if !NOSQLITE
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
                    return _db.GetDocumentCount();
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

            public void Import()
            {
                // no-op
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
            private string _oldAttachmentsPath;

            public int NumDocs { get; private set; }

            public int NumRevs { get; private set; }

            public bool CanRemoveOldAttachmentsDir { get; set; }

            public v1_upgrader(Database db, string path)
            {
                _db = db;
                _path = path;
                CanRemoveOldAttachmentsDir = true;
            }

            public v1_upgrader(Database db, sqlite3 sqlite)
            {
                _db = db;
                _sqlite = sqlite;
                CanRemoveOldAttachmentsDir = false;
            }

            internal void PrepareSQL(ref sqlite3_stmt stmt, string sql)
            {
                int err;
                if (stmt != null) {
                    err = raw.sqlite3_reset(stmt);
                } else {
                    err = raw.sqlite3_prepare_v2(_sqlite, sql, out stmt);
                }

                var status = SqliteErrToStatus(err);
                if (status.IsError) {
                    throw new CouchbaseLiteException(String.Format("Couldn't compile SQL `{0}` : ({1} / {2} / {3})", sql, 
                        raw.sqlite3_errcode(_sqlite), raw.sqlite3_extended_errcode(_sqlite), raw.sqlite3_errmsg(_sqlite)),
                        status.Code);
                }
            }

            private void ImportDoc(string docID, long docNumericID)
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
                PrepareSQL(ref revQuery, "SELECT sequence, revid, parent, current, deleted, json" +
                                " FROM revs WHERE doc_id=? ORDER BY sequence");

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
                        try {
                            AddAttachmentsToSequence(sequence, nuJson);
                        } catch(CouchbaseLiteException) {
                            Log.W(TAG, "Failed to add attachments to sequence {0}", sequence);
                            raw.sqlite3_finalize(revQuery);
                            throw;
                        } catch(Exception e) {
                            raw.sqlite3_finalize(revQuery);
                            throw new CouchbaseLiteException(String.Format(
                                "Error adding attachments to sequence {0}", sequence), e) { Code = StatusCode.DbError };
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
                            _db.ForceInsert(rev, history, null);
                        } catch (CouchbaseLiteException) {
                            Log.W(TAG, "Failed to insert revision {0} into target database", rev);
                            raw.sqlite3_finalize(revQuery);
                            throw;
                        } catch(Exception e) {
                            raw.sqlite3_finalize(revQuery);
                            throw new CouchbaseLiteException(String.Format(
                                "Error inserting revision {0} into target database", rev), e) { Code = StatusCode.Exception };
                        }

                        NumRevs++;
                    } else {
                        tree[sequence] = new List<object> { revID, parentSeq };
                    }
                }

                raw.sqlite3_finalize(revQuery);
                ++NumDocs;
                if (err != raw.SQLITE_OK) {
                    var s = SqliteErrToStatus(err);
                    if (s.IsError) {
                        throw new CouchbaseLiteException(s.Code);
                    }
                }
            }

            private void AddAttachmentsToSequence(long sequence, List<byte> json)
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
                try {
                    PrepareSQL(ref attQuery, "SELECT filename, key, type, length,"
                                + " revpos, encoding, encoded_length FROM attachments WHERE sequence=?");
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Failed to create SQLite query for attachments table in source database '{0}'", _path);
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException(String.Format(
                        "Error creating SQLite query for attachments table in source database '{0}'", _path),
                        e) { Code = StatusCode.DbError };
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
                        throw new CouchbaseLiteException(String.Format(
                            "Digest key length incorrect ({0})", Convert.ToBase64String(key)), StatusCode.CorruptError);
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
                    throw new CouchbaseLiteException(String.Format(
                        "Failed to finalize attachment query ({0}: {1})", err, raw.sqlite3_errmsg(_sqlite)),
                        SqliteErrToStatus(err).Code);
                }

                if (attachments.Count > 0) {
                    // Splice attachment JSON into the document JSON:
                    var attJson = Manager.GetObjectMapper().WriteValueAsBytes(new Dictionary<string, object> { { "_attachments", attachments } });

                    if (json.Count > 2) {
                        json.Insert(json.Count - 1, (byte)',');
                    }

                    json.InsertRange(json.Count - 1, attJson.Skip(1).Take(attJson.Count() - 2));
                }
            }

            internal void ImportLocalDocs()
            {
                // CREATE TABLE localdocs (
                //  docid TEXT UNIQUE NOT NULL,
                //  revid TEXT NOT NULL COLLATE REVID,
                //  json BLOB );

                sqlite3_stmt localQuery = null;
                PrepareSQL(ref localQuery, "SELECT docid, json FROM localdocs");

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
                            if(docID.StartsWith("_local/")) {
                                docID = docID.Substring(7);
                            }

                            _db.PutLocalDocument(docID, props);
                        } catch(CouchbaseLiteException e) {
                            Log.W(TAG, "Couldn't import local doc '{0}': {1}", docID, e.CBLStatus);
                        }
                    }
                }

                raw.sqlite3_finalize(localQuery);
                if (err != raw.SQLITE_OK) {
                    var s = SqliteErrToStatus(err);
                    if (s.IsError) {
                        throw new CouchbaseLiteException(s.Code);
                    }
                }
            }

            internal void ImportInfo()
            {
                //TODO: Revisit this once pluggable storage is finished
                // CREATE TABLE info (key TEXT PRIMARY KEY, value TEXT);
                sqlite3_stmt infoQuery = null;
                PrepareSQL(ref infoQuery, "SELECT key, value FROM info");

                int err = raw.sqlite3_step(infoQuery);
                if (err != raw.SQLITE_ROW) {
                    raw.sqlite3_finalize(infoQuery);
                    throw new CouchbaseLiteException(String.Format("SQLite error {0} ({1}) reading info table from source database '{2}'",
                        err, raw.sqlite3_errmsg(_sqlite), _path), SqliteErrToStatus(err).Code);
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
                    throw new CouchbaseLiteException(String.Format("SQLite error {0} ({1}) reading info table from source database '{2}'",
                        err, raw.sqlite3_errmsg(_sqlite), _path), SqliteErrToStatus(err).Code);
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
                    throw new CouchbaseLiteException("UUIDs missing from source database", StatusCode.CorruptError);
                }

                try {
                    _db.ReplaceUUIDs(privateUUID, publicUUID);
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Failed to replace UUIDs in database");
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException("Error replacing UUIDs in database", e) { Code = StatusCode.DbError };
                }
            }

            private void MoveAttachmentsDir()
            {
                _oldAttachmentsPath = Path.Combine(Path.ChangeExtension(_path, null), "attachments");
                var newAttachmentsPath = _db.AttachmentStorePath;
                if (_oldAttachmentsPath.Equals(newAttachmentsPath)) {
                    Log.D(TAG, "Skip moving the attachments folder as no path change ('{0}' vs '{1}').", _oldAttachmentsPath, newAttachmentsPath);
                    return;
                }

                if (!Directory.Exists(_oldAttachmentsPath)) {
                    _oldAttachmentsPath = Path.Combine(Path.GetDirectoryName(_path), Path.ChangeExtension(_path, null) + " attachments");
                    if (!Directory.Exists(_oldAttachmentsPath)) {
                        return;
                    }
                }
                    
                Log.D(TAG, "Moving {0} to {1}", _oldAttachmentsPath, newAttachmentsPath);
                Directory.Delete(newAttachmentsPath, true);
                Directory.CreateDirectory(newAttachmentsPath);

                try {
                    if (CanRemoveOldAttachmentsDir) {
                        // Need to ensure upper case
                        foreach(var file in Directory.GetFiles(_oldAttachmentsPath)) {
                            var filename = Path.GetFileNameWithoutExtension(file).ToUpperInvariant();
                            var extension = Path.GetExtension(file);
                            var newPath = Path.Combine(newAttachmentsPath, filename + extension);
                            File.Move(file, newPath);
                        }

                        Directory.Delete(Path.ChangeExtension(_db.DbDirectory, null), true);
                    } else {
                        DirectoryCopy(_oldAttachmentsPath, newAttachmentsPath);
                    }
                } catch(IOException e) {
                    if (!(e is DirectoryNotFoundException)) {
                        throw new CouchbaseLiteException("Upgrade failed:  Couldn't move attachments", StatusCode.Exception);
                    }
                }
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

            public void Import()
            {
                // Rename the old database file for migration:
                var destPath = Path.ChangeExtension(_path, Manager.DatabaseSuffixv1 + "-mgr");
                if (!MoveSqliteFiles(_path, destPath)) {
                    Log.W(TAG, "Upgrade failed: Cannot rename the old sqlite files");
                    MoveSqliteFiles(destPath, _path);
                    throw new CouchbaseLiteException(StatusCode.InternalServerError);
                }

                int version = DatabaseUpgraderFactory.SchemaVersion(destPath);
                if (version < 0) {
                    throw new CouchbaseLiteException("Cannot determine database schema version", StatusCode.CorruptError);
                }

                // Open source (SQLite) database:
                var err = raw.sqlite3_open_v2(destPath, out _sqlite, raw.SQLITE_OPEN_READWRITE, null);
                if (err > 0) {
                    throw new CouchbaseLiteException(SqliteErrToStatus(err).Code);
                }

                raw.sqlite3_create_collation(_sqlite, "JSON", raw.SQLITE_UTF8, CollateRevIDs);
                sqlite3_stmt stmt = null;
                PrepareSQL(ref stmt, "SELECT name FROM sqlite_master WHERE type='table' AND name='maps'");

                err = raw.sqlite3_step(stmt);
                if (err == raw.SQLITE_ROW) {
                    sqlite3_stmt stmt2 = null;
                    PrepareSQL(ref stmt2, "SELECT * FROM maps");
                    while ((err = raw.sqlite3_step(stmt2)) == raw.SQLITE_ROW) {
                        int viewId = raw.sqlite3_column_int(stmt2, 0);
                        sqlite3_stmt stmt3 = null;
                         PrepareSQL(ref stmt3, "CREATE TABLE IF NOT EXISTS maps_" + viewId + 
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

                        var insertSql = String.Format("INSERT INTO maps_{0} (sequence, key, value) VALUES (?, ?, ?)",
                                            viewId);
                        
                        PrepareSQL(ref stmt3, insertSql);
                        raw.sqlite3_bind_int64(stmt3, 0, sequence);
                        raw.sqlite3_bind_text(stmt3, 1, key);
                        raw.sqlite3_bind_text(stmt3, 2, value);
                        raw.sqlite3_step(stmt3);
                        raw.sqlite3_finalize(stmt3);
                    }

                    raw.sqlite3_finalize(stmt2);
                    stmt2 = null;
                    PrepareSQL(ref stmt2, "DROP TABLE maps");
                    raw.sqlite3_step(stmt2);
                    raw.sqlite3_finalize(stmt2);
                }

                raw.sqlite3_finalize(stmt);
                raw.sqlite3_close(_sqlite);
                if (err != raw.SQLITE_DONE) {
                    throw new CouchbaseLiteException(SqliteErrToStatus(err).Code);
                }

                if (version >= 101) {
                    _db.Delete();
                    MoveSqliteFiles(destPath, _path);
                    var secondaryUpgrade = new v11_upgrader(_db, _path);
                    secondaryUpgrade.Import();

                    var fromDir = Path.Combine(Path.GetDirectoryName(_path), _db.Name + Manager.DatabaseSuffix);
                    var toDir = _db.DbDirectory;
                    try {
                        Directory.Move(fromDir, toDir);
                    } catch (Exception ex) {
                        Log.W(TAG, "Failed to move directory '{0}' to '{1}' {2}", fromDir, toDir, ex.ToString());
                    }
                    return;
                }

                Log.D(TAG, "Upgrading database v1.0 ({0}) to v1.1 at {1} ...", version, _path);

                err = raw.sqlite3_open_v2(destPath, out _sqlite, raw.SQLITE_OPEN_READONLY, null);
                if (err > 0) {
                    throw new CouchbaseLiteException(SqliteErrToStatus(err).Code);
                }

                raw.sqlite3_create_collation(_sqlite, "REVID", raw.SQLITE_UTF8, CollateRevIDs);

                // Open destination database:
                try {
                    _db.Open();
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Upgrade failed: Couldn't open new db");
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException("Error during upgrade; couldn't open new db", e) { Code = StatusCode.Exception };
                }

                try {
                    MoveAttachmentsDir();
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Failed to move attachments directory for database at '{0}'", _path);
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException(String.Format(
                        "Error moving attachments directory for database at '{0}'", _path), e) { Code = StatusCode.Exception };
                }

                // Upgrade documents:
                // CREATE TABLE docs (doc_id INTEGER PRIMARY KEY, docid TEXT UNIQUE NOT NULL);
                sqlite3_stmt docQuery = null;
                PrepareSQL(ref docQuery, "SELECT doc_id, docid FROM docs");

                _db.RunInTransaction(() =>
                {
                    int transactionErr;
                    int count = 0;
                    while(raw.SQLITE_ROW == (transactionErr = raw.sqlite3_step(docQuery))) {
                        long docNumericID = raw.sqlite3_column_int64(docQuery, 0);
                        string docID = raw.sqlite3_column_text(docQuery, 1);
                        try {
                            ImportDoc(docID, docNumericID);
                        } catch(CouchbaseLiteException) {
                            Log.W(TAG, "Failed to import document #{0} ({1})", docNumericID, docID);
                            throw;
                        } catch(Exception e) {
                            throw new CouchbaseLiteException(String.Format("Error importing document #{0} ({1}",
                                docNumericID, docID), e) { Code = StatusCode.Exception };
                        }
                        
                        if((++count % 1000) == 0) {
                            Log.I(TAG, "Migrated {0} documents", count);
                        }
                    }
                        
                    return transactionErr == raw.SQLITE_DONE;
                });

                raw.sqlite3_finalize(docQuery);

                try {
                    ImportLocalDocs();
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Failed to import local docs for database '{0}'", _path);
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException(String.Format(
                        "Error importing local docs for database '{0}'", _path), e) { Code = StatusCode.Exception };
                }

                try {
                    ImportInfo();
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Failed to import info for database '{0}'", _path);
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException(String.Format(
                        "Error importing info for database '{0}'", _path), e) { Code = StatusCode.Exception };
                }

                raw.sqlite3_close(_sqlite);
                _sqlite = null;
                File.Delete(destPath);
                File.Delete(destPath + "-wal");
                File.Delete(destPath + "-shm");

            }

            public void Backout()
            {
                var destPath = Path.ChangeExtension(_path, Manager.DatabaseSuffixv1 + "-mgr");
                if (!File.Exists(destPath)) {
                    // Upgrader failed to even do the first step, nothing to backout
                    return;
                }

                // Move attachments dir back to the old path
                var newAttachmentsPath = _db.AttachmentStorePath;
                if (_oldAttachmentsPath != null && Directory.Exists(newAttachmentsPath)) {
                    if (CanRemoveOldAttachmentsDir) {
                        try {
                            Directory.CreateDirectory(_oldAttachmentsPath);
                        } catch(IOException) {
                        }
                    }
                }

                _db.Delete();
                MoveSqliteFiles(destPath, _path);

            }

            #endregion

        }

        private class v11_upgrader : IDatabaseUpgrader
        {
            private const string TAG = "v11_upgrader";
            private const string SUFFIX = ".cblite2";
            private readonly Database _db;
            private readonly string _path;

            public int NumDocs { get; private set; }

            public int NumRevs { get; private set; }

            public bool CanRemoveOldAttachmentsDir { get; set; }

            public v11_upgrader(Database db, string path)
            {
                _db = db;
                _path = path;
                CanRemoveOldAttachmentsDir = true;
            }
             
            public void Import()
            {
                var newPath = Path.Combine(Path.GetDirectoryName(_path), _db.Name + SUFFIX);
                if (Directory.Exists(newPath)) {
                    Log.W(TAG, "Upgrade to v1.2 failed ({0} already exists)", newPath);
                    throw new CouchbaseLiteException(StatusCode.PreconditionFailed);
                }

                Directory.CreateDirectory(newPath);
                var sqliteFilePath = Path.Combine(newPath, "db.sqlite3");
                try {
                    File.Copy(_path, sqliteFilePath);
                    if (File.Exists(_path + "-wal")) {
                        File.Copy(_path + "-wal", sqliteFilePath + "-wal");
                    }

                    if (File.Exists(_path + "-shm")) {
                        File.Copy(_path + "-shm", sqliteFilePath + "-shm");
                    }
                } catch(IOException e) {
                    Log.W(TAG, "Upgrade to v1.2 failed (Couldn't copy sqlite files: {0})", e.ToString());
                    throw new CouchbaseLiteException(StatusCode.InternalServerError);
                }

                var oldAttachmentsPath = Path.Combine(Path.GetDirectoryName(_path), _db.Name + " attachments");
                try {
                    if (Directory.Exists(oldAttachmentsPath)) {
                        var newAttachmentsPath = Path.Combine(newPath, "attachments");
                        Directory.CreateDirectory(newAttachmentsPath);

                        foreach (var att in Directory.GetFiles(oldAttachmentsPath)) {
                            File.Copy(att, Path.Combine(newAttachmentsPath, Path.GetFileName(att))); 
                        }
                    }
                } catch(IOException e) {
                    Log.W(TAG, "Upgrade to v1.2 failed (Couldn't copy attachment files: {0})", e.ToString());
                    throw new CouchbaseLiteException(StatusCode.InternalServerError);
                }

                File.Delete(_path);
                File.Delete(_path + "-wal");
                File.Delete(_path + "-shm");
                Directory.Delete(oldAttachmentsPath, true);
            }

            public void Backout()
            {
                var newPath = Path.Combine(Path.GetDirectoryName(_path), _db.Name + SUFFIX);
                Directory.Delete(newPath, true);
            }

        }

        private class v12_upgrader : IDatabaseUpgrader
        {
            private sqlite3 _sqlite;
            private Database _db;
            private string _path;
            private v1_upgrader _inner;

            public v12_upgrader(Database db, string path)
            {
                _path = Path.Combine(path, "db.sqlite3");
                _db = db;
            }

            private void UpdateAttachmentFollows(List<byte> json)
            {
                var obj = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(json);
                var attachments = obj.Get("_attachments").AsDictionary<string, object>();
                var newAttachments = new Dictionary<string, object>();
                foreach (var key in attachments.Keys) {
                    var attachmentData = attachments.Get(key).AsDictionary<string, object>();
                    attachmentData["follows"] = true;
                    attachmentData.Remove("stub");
                    newAttachments[key] = attachmentData;
                }

                obj["_attachments"] = newAttachments;

                var nuJson = Manager.GetObjectMapper().WriteValueAsBytes(obj);
                json.Clear();
                json.AddRange(nuJson);
            }

            private void ImportDoc(string docID, long docNumericID)
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
                _inner.PrepareSQL(ref revQuery, "SELECT sequence, revid, parent, current, deleted, json, no_attachments" +
                    " FROM revs WHERE doc_id=? ORDER BY sequence");

                raw.sqlite3_bind_int64(revQuery, 1, docNumericID);

                var tree = new Dictionary<long, IList<object>>();

                int err;
                while (raw.SQLITE_ROW == (err = raw.sqlite3_step(revQuery))) {
                    long sequence = raw.sqlite3_column_int64(revQuery, 0);
                    string revID = raw.sqlite3_column_text(revQuery, 1);
                    long parentSeq = raw.sqlite3_column_int64(revQuery, 2);
                    bool current = raw.sqlite3_column_int(revQuery, 3) != 0;
                    bool noAtts = raw.sqlite3_column_int(revQuery, 6) != 0;

                    if (current) {
                        // Add a leaf revision:
                        bool deleted = raw.sqlite3_column_int(revQuery, 4) != 0;
                        IEnumerable<byte> json = raw.sqlite3_column_blob(revQuery, 5);
                        if (json == null) {
                            json = Encoding.UTF8.GetBytes("{}");
                        }

                        var nuJson = json.ToList();
                        if (!noAtts) {
                            try {
                                UpdateAttachmentFollows(nuJson);
                            } catch(CouchbaseLiteException) {
                                Log.E(TAG, "Failed to process attachments");
                                throw;
                            } catch(Exception e) {
                                throw new CouchbaseLiteException("Error processing attachments", e);
                            }
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
                            _db.ForceInsert(rev, history, null);
                        } catch (CouchbaseLiteException) {
                            Log.W(TAG, "Failed to insert revision {0} into target database", rev);
                            raw.sqlite3_finalize(revQuery);
                            throw;
                        } catch(Exception e) {
                            raw.sqlite3_finalize(revQuery);
                            throw new CouchbaseLiteException(String.Format(
                                "Error inserting revision {0} into target database", rev), e) { Code = StatusCode.Exception };
                        }

                        NumRevs++;
                    } else {
                        tree[sequence] = new List<object> { revID, parentSeq };
                    }
                }

                raw.sqlite3_finalize(revQuery);
                ++NumDocs;
                if (err != raw.SQLITE_OK) {
                    var s = SqliteErrToStatus(err);
                    if (s.IsError) {
                        throw new CouchbaseLiteException(s.Code);
                    }
                }
            }

            #region IDatabaseUpgrader implementation

            public void Import()
            {
                // Open source (SQLite) database:
                var err = raw.sqlite3_open_v2(_path, out _sqlite, raw.SQLITE_OPEN_READONLY, null);
                _inner = new v1_upgrader(_db, _sqlite);
                if (err > 0) {
                    throw new CouchbaseLiteException(SqliteErrToStatus(err).Code);
                }

                raw.sqlite3_create_collation(_sqlite, "JSON", raw.SQLITE_UTF8, CollateRevIDs);
                raw.sqlite3_create_collation(_sqlite, "REVID", raw.SQLITE_UTF8, CollateRevIDs);

                // Open destination database:
                try {
                    _db.Open();
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Upgrade failed: Couldn't open new db");
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException("Error during upgrade; couldn't open new db", e) { Code = StatusCode.Exception };
                }

                // Upgrade documents:
                // CREATE TABLE docs (doc_id INTEGER PRIMARY KEY, docid TEXT UNIQUE NOT NULL);
                sqlite3_stmt docQuery = null;
                _inner.PrepareSQL(ref docQuery, "SELECT doc_id, docid FROM docs");

                _db.RunInTransaction(() =>
                {
                    int transactionErr;
                    int count = 0;
                    while(raw.SQLITE_ROW == (transactionErr = raw.sqlite3_step(docQuery))) {
                        long docNumericID = raw.sqlite3_column_int64(docQuery, 0);
                        string docID = raw.sqlite3_column_text(docQuery, 1);
                        try {
                            ImportDoc(docID, docNumericID);
                        } catch(CouchbaseLiteException) {
                            Log.W(TAG, "Failed to import document #{0} ({1})", docNumericID, docID);
                            throw;
                        } catch(Exception e) {
                            throw new CouchbaseLiteException(String.Format("Error importing document #{0} ({1}",
                                docNumericID, docID), e) { Code = StatusCode.Exception };
                        }

                        if((++count % 1000) == 0) {
                            Log.I(TAG, "Migrated {0} documents", count);
                        }
                    }

                    return transactionErr == raw.SQLITE_DONE;
                });

                raw.sqlite3_finalize(docQuery);

                try {
                    _inner.ImportLocalDocs();
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Failed to import local docs for database '{0}'", _path);
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException(String.Format(
                        "Error importing local docs for database '{0}'", _path), e) { Code = StatusCode.Exception };
                }

                try {
                    _inner.ImportInfo();
                } catch(CouchbaseLiteException) {
                    Log.W(TAG, "Failed to import info for database '{0}'", _path);
                    throw;
                } catch(Exception e) {
                    throw new CouchbaseLiteException(String.Format(
                        "Error importing info for database '{0}'", _path), e) { Code = StatusCode.Exception };
                }

                raw.sqlite3_close(_sqlite);
                _sqlite = null;

                foreach (var ext in new List<string> { "", "-wal", "-shm" }) {
                    File.Delete(_path + ext);
                }
            }

            public void Backout()
            {
                var directory = Path.GetDirectoryName(_path);
                File.Delete(Path.Combine(directory, "db.forest"));
                File.Delete(Path.Combine(directory, "db.forest.0"));
                File.Delete(Path.Combine(directory, "db.forest.meta"));
            }

            public int NumDocs { get; private set; }

            public int NumRevs { get; private set; }

            public bool CanRemoveOldAttachmentsDir {
                get { return false; }
                set { }
            }

            #endregion


        }
    }
}
#endif
