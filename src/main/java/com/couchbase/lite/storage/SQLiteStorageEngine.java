/**
 * Created by Wayne Carter.
 *
 * Copyright (c) 2012 Couchbase, Inc. All rights reserved.
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
 * except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the
 * License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
 * either express or implied. See the License for the specific language governing permissions
 * and limitations under the License.
 */

package com.couchbase.lite.storage;

public interface SQLiteStorageEngine {
    public static final int CONFLICT_NONE = 0;
    public static final int CONFLICT_IGNORE = 4;
    public static final int CONFLICT_REPLACE = 5;

    boolean open(String path);
    int getVersion();
    void setVersion(int version);
    boolean isOpen();
    void beginTransaction();
    void endTransaction();
    void setTransactionSuccessful();
    void execSQL(String sql) throws SQLException;
    void execSQL(String sql, Object[] bindArgs) throws SQLException;
    Cursor rawQuery(String sql, String[] selectionArgs);
    long insert(String table, String nullColumnHack, ContentValues values);
    long insertWithOnConflict(String table, String nullColumnHack, ContentValues initialValues, int conflictAlgorithm);
    int update(String table, ContentValues values, String whereClause, String[] whereArgs);
    int delete(String table, String whereClause, String[] whereArgs);
    void close();
}
