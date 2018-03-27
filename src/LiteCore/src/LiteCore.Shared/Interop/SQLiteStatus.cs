// 
//  SQLiteStatus.cs
// 
//  Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
namespace LiteCore.Interop
{
    public enum SQLiteStatus
    {
        Ok,  /* Successful result */
        Error,  /* SQL error or missing database */
        Internal,  /* Internal logic error in SQLite */
        Perm,  /* Access permission denied */
        Abort,  /* Callback routine requested an abort */
        Busy,  /* The database file is locked */
        Locked,  /* A table in the database is locked */
        NoMem,  /* A malloc() failed */
        ReadOnly,  /* Attempt to write a readonly database */
        Interrupt,  /* Operation terminated by sqlite3_interrupt()*/
        IoErr,  /* Some kind of disk I/O error occurred */
        Corrupt,  /* The database disk image is malformed */
        NotFound,  /* Unknown opcode in sqlite3_file_control() */
        Full,  /* Insertion failed because database is full */
        CantOpen,  /* Unable to open the database file */
        Protocol,  /* Database lock protocol error */
        Empty,  /* Database is empty */
        Schema,  /* The database schema changed */
        TooBig,  /* String or BLOB exceeds size limit */
        Constraint,  /* Abort due to constraint violation */
        Mismatch,  /* Data type mismatch */
        Misuse,  /* Library used incorrectly */
        NoLfs,  /* Uses OS features not supported on host */
        Auth,  /* Authorization denied */
        Format,  /* Auxiliary database format error */
        Range,  /* 2nd parameter to sqlite3_bind out of range */
        NotADb,  /* File opened that is not a database file */
        Notice,  /* Notifications from sqlite3_log() */
        Warning,  /* Warnings from sqlite3_log() */
        Row, /* sqlite3_step() has another row ready */
        Done, /* sqlite3_step() has finished executing */
    }
}
