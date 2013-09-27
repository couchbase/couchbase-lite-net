package com.couchbase.cblite.internal;

import android.database.Cursor;
import android.database.SQLException;
import android.database.sqlite.SQLiteDatabase;
import android.util.Log;

import com.couchbase.cblite.CBLDatabase;
import com.couchbase.cblite.CBLServer;
import com.couchbase.cblite.CBLStatus;
import com.couchbase.cblite.CBLiteException;

import java.util.Map;

public class CBLDatabaseInternal {

    private CBLDatabase cblDatabase;
    private SQLiteDatabase sqliteDb;

    public CBLDatabaseInternal(CBLDatabase cblDatabase, SQLiteDatabase sqliteDb) {
        this.cblDatabase = cblDatabase;
        this.sqliteDb = sqliteDb;
    }

    public CBLRevisionInternal getLocalDocument(String docID, String revID) {

        CBLRevisionInternal result = null;
        Cursor cursor = null;
        try {
            String[] args = { docID };
            cursor = sqliteDb.rawQuery("SELECT revid, json FROM localdocs WHERE docid=?", args);
            if(cursor.moveToFirst()) {
                String gotRevID = cursor.getString(0);
                if(revID != null && (!revID.equals(gotRevID))) {
                    return null;
                }
                byte[] json = cursor.getBlob(1);
                Map<String,Object> properties = null;
                try {
                    properties = CBLServer.getObjectMapper().readValue(json, Map.class);
                    properties.put("_id", docID);
                    properties.put("_rev", gotRevID);
                    result = new CBLRevisionInternal(docID, gotRevID, false, cblDatabase);
                    result.setProperties(properties);
                } catch (Exception e) {
                    Log.w(CBLDatabase.TAG, "Error parsing local doc JSON", e);
                    return null;
                }

            }
            return result;
        } catch (SQLException e) {
            Log.e(CBLDatabase.TAG, "Error getting local document", e);
            return null;
        } finally {
            if(cursor != null) {
                cursor.close();
            }
        }

    }


    public void deleteLocalDocument(String docID, String revID) throws CBLiteException {
        if(docID == null) {
            throw new CBLiteException(CBLStatus.BAD_REQUEST);
        }
        if(revID == null) {
            // Didn't specify a revision to delete: 404 or a 409, depending
            if (getLocalDocument(docID, null) != null) {
                throw new CBLiteException(CBLStatus.CONFLICT);
            }
            else {
                throw new CBLiteException(CBLStatus.NOT_FOUND);
            }
        }
        String[] whereArgs = { docID, revID };
        try {
            int rowsDeleted = sqliteDb.delete("localdocs", "docid=? AND revid=?", whereArgs);
            if(rowsDeleted == 0) {
                if (getLocalDocument(docID, null) != null) {
                    throw new CBLiteException(CBLStatus.CONFLICT);
                }
                else {
                    throw new CBLiteException(CBLStatus.NOT_FOUND);
                }
            }
        } catch (SQLException e) {
            throw new CBLiteException(e, CBLStatus.INTERNAL_SERVER_ERROR);
        }
    }

}