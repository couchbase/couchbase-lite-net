/**
 * Original iOS version by  Jens Alfke
 * Ported to Android by Marty Schoch
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

package com.couchbase.cblite;

import java.util.ArrayList;
import java.util.EnumSet;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import com.couchbase.cblite.CBLDatabase.TDContentOptions;
import com.couchbase.cblite.internal.CBLRevisionInternal;

import android.content.ContentValues;
import android.database.Cursor;
import android.database.SQLException;
import android.database.sqlite.SQLiteDatabase;
import android.util.Log;

/**
 * Represents a view available in a database.
 */
public class CBLView {

    public static final int REDUCE_BATCH_SIZE = 100;

    public enum TDViewCollation {
        TDViewCollationUnicode, TDViewCollationRaw, TDViewCollationASCII
    }

    private CBLDatabase database;
    private String name;
    private int viewId;
    private CBLMapFunction mapBlock;
    private CBLReduceFunction reduceBlock;
    private TDViewCollation collation;
    private static CBLViewCompiler compiler;

    public CBLView(CBLDatabase database, String name) {
        this.database = database;
        this.name = name;
        this.viewId = -1; // means 'unknown'
        this.collation = TDViewCollation.TDViewCollationUnicode;
    }

    /**
     * Get the database that owns this view.
     */
    public CBLDatabase getDatabase() {
        return database;
    };

    /**
     * Get the name of the view.
     */
    public String getName() {
        return name;
    }

    /**
     * The map function that controls how index rows are created from documents.
     */
    public CBLMapFunction getMap() {
        return mapBlock;
    }

    /**
     * The optional reduce function, which aggregates together multiple rows.
     */
    public CBLReduceFunction getReduce() {
        return reduceBlock;
    }

    /**
     * Is the view's index currently out of date?
     */
    public boolean isStale() {
        return (getLastSequenceIndexed() < database.getLastSequenceNumber());
    }

    /**
     * Creates a new query object for this view. The query can be customized and then executed.
     */
    public CBLQuery createQuery() {
        return new CBLQuery(getDatabase(), this);
    }

    public int getViewId() {
        if (viewId < 0) {
            String sql = "SELECT view_id FROM views WHERE name=?";
            String[] args = { name };
            Cursor cursor = null;
            try {
                cursor = database.getSqliteDb().rawQuery(sql, args);
                if (cursor.moveToFirst()) {
                    viewId = cursor.getInt(0);
                } else {
                    viewId = 0;
                }
            } catch (SQLException e) {
                Log.e(CBLDatabase.TAG, "Error getting view id", e);
                viewId = 0;
            } finally {
                if (cursor != null) {
                    cursor.close();
                }
            }
        }
        return viewId;
    }

    /**
     * Get the last sequence number indexed so far.
     */
    public long getLastSequenceIndexed() {
        String sql = "SELECT lastSequence FROM views WHERE name=?";
        String[] args = { name };
        Cursor cursor = null;
        long result = -1;
        try {
            cursor = database.getSqliteDb().rawQuery(sql, args);
            if (cursor.moveToFirst()) {
                result = cursor.getLong(0);
            }
        } catch (Exception e) {
            Log.e(CBLDatabase.TAG, "Error getting last sequence indexed");
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }
        return result;
    }

    /**
     * Defines a view that has no reduce function.

     * @param mapBlock
     * @param version
     * @return
     */
    public boolean setMap(CBLMapFunction mapBlock, String version) {
        return setMapAndReduce(mapBlock, null, version);
    }

    /**
     * Defines a view's functions.
     *
     * The view's definition is given as a class that conforms to the CBLMapFunction or
     * CBLReduceFunction interface (or null to delete the view). The body of the block
     * should call the 'emit' object (passed in as a paramter) for every key/value pair
     * it wants to write to the view.
     *
     * Since the function itself is obviously not stored in the database (only a unique
     * string idenfitying it), you must re-define the view on every launch of the app!
     * If the database needs to rebuild the view but the function hasn't been defined yet,
     * it will fail and the view will be empty, causing weird problems later on.
     *
     * It is very important that this block be a law-abiding map function! As in other
     * languages, it must be a "pure" function, with no side effects, that always emits
     * the same values given the same input document. That means that it should not access
     * or change any external state; be careful, since callbacks make that so easy that you
     * might do it inadvertently!  The callback may be called on any thread, or on
     * multiple threads simultaneously. This won't be a problem if the code is "pure" as
     * described above, since it will as a consequence also be thread-safe.
     *
     * @param mapBlock
     * @param reduceBlock
     * @param version
     * @return
     */
    public boolean setMapAndReduce(CBLMapFunction mapBlock,
                                   CBLReduceFunction reduceBlock, String version) {
        assert (mapBlock != null);
        assert (version != null);

        this.mapBlock = mapBlock;
        this.reduceBlock = reduceBlock;

        if(!database.open()) {
            return false;
        }

        // Update the version column in the database. This is a little weird looking
        // because we want to
        // avoid modifying the database if the version didn't change, and because the
        // row might not exist yet.
        SQLiteDatabase database = this.database.getSqliteDb();

        // Older Android doesnt have reliable insert or ignore, will to 2 step
        // FIXME review need for change to execSQL, manual call to changes()

        String sql = "SELECT name, version FROM views WHERE name=?";
        String[] args = { name };
        Cursor cursor = null;

        try {
            cursor = this.database.getSqliteDb().rawQuery(sql, args);
            if (!cursor.moveToFirst()) {
                // no such record, so insert
                ContentValues insertValues = new ContentValues();
                insertValues.put("name", name);
                insertValues.put("version", version);
                database.insert("views", null, insertValues);
                return true;
            }

            ContentValues updateValues = new ContentValues();
            updateValues.put("version", version);
            updateValues.put("lastSequence", 0);

            String[] whereArgs = { name, version };
            int rowsAffected = database.update("views", updateValues,
                    "name=? AND version!=?", whereArgs);

            return (rowsAffected > 0);
        } catch (SQLException e) {
            Log.e(CBLDatabase.TAG, "Error setting map block", e);
            return false;
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }

    }

    /**
     * Deletes the view's persistent index. It will be regenerated on the next query.
     */
    public void removeIndex() {
        if (getViewId() < 0) {
            return;
        }

        boolean success = false;
        try {
            database.beginTransaction();

            String[] whereArgs = { Integer.toString(getViewId()) };
            database.getSqliteDb().delete("maps", "view_id=?", whereArgs);

            ContentValues updateValues = new ContentValues();
            updateValues.put("lastSequence", 0);
            database.getSqliteDb().update("views", updateValues, "view_id=?",
                    whereArgs);

            success = true;
        } catch (SQLException e) {
            Log.e(CBLDatabase.TAG, "Error removing index", e);
        } finally {
            database.endTransaction(success);
        }
    }

    /**
     * Deletes the view, persistently.
     */
    public void delete() {
        database.deleteViewNamed(name);
        viewId = 0;
    }

    public void databaseClosing() {
        database = null;
        viewId = 0;
    }

    /*** Indexing ***/

    public String toJSONString(Object object) {
        if (object == null) {
            return null;
        }
        String result = null;
        try {
            result = CBLServer.getObjectMapper().writeValueAsString(object);
        } catch (Exception e) {
            Log.w(CBLDatabase.TAG, "Exception serializing object to json: " + object, e);
        }
        return result;
    }

    public Object fromJSON(byte[] json) {
        if (json == null) {
            return null;
        }
        Object result = null;
        try {
            result = CBLServer.getObjectMapper().readValue(json, Object.class);
        } catch (Exception e) {
            Log.w(CBLDatabase.TAG, "Exception parsing json", e);
        }
        return result;
    }

    public TDViewCollation getCollation() {
        return collation;
    }

    public void setCollation(TDViewCollation collation) {
        this.collation = collation;
    }

    /**
     * Updates the view's index (incrementally) if necessary.
     * @return 200 if updated, 304 if already up-to-date, else an error code
     */
    @SuppressWarnings("unchecked")
    public CBLStatus updateIndex() throws CBLiteException {
        Log.v(CBLDatabase.TAG, "Re-indexing view " + name + " ...");
        assert (mapBlock != null);

        if (getViewId() < 0) {
            throw new CBLiteException(new CBLStatus(CBLStatus.NOT_FOUND));
        }

        database.beginTransaction();
        CBLStatus result = new CBLStatus(CBLStatus.INTERNAL_SERVER_ERROR);
        Cursor cursor = null;

        try {

            long lastSequence = getLastSequenceIndexed();
            long dbMaxSequence = database.getLastSequenceNumber();
            if(lastSequence == dbMaxSequence) {
                throw new CBLiteException(new CBLStatus(CBLStatus.NOT_MODIFIED));
            }

            // First remove obsolete emitted results from the 'maps' table:
            long sequence = lastSequence;
            if (lastSequence < 0) {
                throw new CBLiteException(new CBLStatus(CBLStatus.INTERNAL_SERVER_ERROR));
            }

            if (lastSequence == 0) {
                // If the lastSequence has been reset to 0, make sure to remove
                // any leftover rows:
                String[] whereArgs = { Integer.toString(getViewId()) };
                database.getSqliteDb().delete("maps", "view_id=?", whereArgs);
            } else {
                // Delete all obsolete map results (ones from since-replaced
                // revisions):
                String[] args = { Integer.toString(getViewId()),
                        Long.toString(lastSequence),
                        Long.toString(lastSequence) };
                database.getSqliteDb().execSQL(
                        "DELETE FROM maps WHERE view_id=? AND sequence IN ("
                                + "SELECT parent FROM revs WHERE sequence>? "
                                + "AND parent>0 AND parent<=?)", args);
            }

            int deleted = 0;
            cursor = database.getSqliteDb().rawQuery("SELECT changes()", null);
            cursor.moveToFirst();
            deleted = cursor.getInt(0);
            cursor.close();

            // This is the emit() block, which gets called from within the
            // user-defined map() block
            // that's called down below.
            AbstractTouchMapEmitBlock emitBlock = new AbstractTouchMapEmitBlock() {

                @Override
                public void emit(Object key, Object value) {

                    try {
                        String keyJson = CBLServer.getObjectMapper().writeValueAsString(key);
                        String valueJson = CBLServer.getObjectMapper().writeValueAsString(value);
                        Log.v(CBLDatabase.TAG, "    emit(" + keyJson + ", "
                                + valueJson + ")");

                        ContentValues insertValues = new ContentValues();
                        insertValues.put("view_id", getViewId());
                        insertValues.put("sequence", sequence);
                        insertValues.put("key", keyJson);
                        insertValues.put("value", valueJson);
                        database.getSqliteDb().insert("maps", null, insertValues);
                    } catch (Exception e) {
                        Log.e(CBLDatabase.TAG, "Error emitting", e);
                        // find a better way to propagate this back
                    }
                }
            };

            // Now scan every revision added since the last time the view was
            // indexed:
            String[] selectArgs = { Long.toString(lastSequence) };

            cursor = database.getSqliteDb().rawQuery(
                    "SELECT revs.doc_id, sequence, docid, revid, json FROM revs, docs "
                            + "WHERE sequence>? AND current!=0 AND deleted=0 "
                            + "AND revs.doc_id = docs.doc_id "
                            + "ORDER BY revs.doc_id, revid DESC", selectArgs);

            cursor.moveToFirst();

            long lastDocID = 0;
            while (!cursor.isAfterLast()) {
                long docID = cursor.getLong(0);
                if (docID != lastDocID) {
                    // Only look at the first-iterated revision of any document,
                    // because this is the
                    // one with the highest revid, hence the "winning" revision
                    // of a conflict.
                    lastDocID = docID;

                    // Reconstitute the document as a dictionary:
                    sequence = cursor.getLong(1);
                    String docId = cursor.getString(2);
                    if(docId.startsWith("_design/")) {  // design docs don't get indexed!
                        cursor.moveToNext();
                        continue;
                    }
                    String revId = cursor.getString(3);
                    byte[] json = cursor.getBlob(4);
                    Map<String, Object> properties = database
                            .documentPropertiesFromJSON(json, docId, revId,
                                    sequence, EnumSet.noneOf(CBLDatabase.TDContentOptions.class));

                    if (properties != null) {
                        // Call the user-defined map() to emit new key/value
                        // pairs from this revision:
                        Log.v(CBLDatabase.TAG,
                                "  call map for sequence="
                                        + Long.toString(sequence));
                        emitBlock.setSequence(sequence);
                        mapBlock.map(properties, emitBlock);
                    }

                }

                cursor.moveToNext();
            }

            // Finally, record the last revision sequence number that was
            // indexed:
            ContentValues updateValues = new ContentValues();
            updateValues.put("lastSequence", dbMaxSequence);
            String[] whereArgs = { Integer.toString(getViewId()) };
            database.getSqliteDb().update("views", updateValues, "view_id=?",
                    whereArgs);

            // FIXME actually count number added :)
            Log.v(CBLDatabase.TAG, "...Finished re-indexing view " + name
                    + " up to sequence " + Long.toString(dbMaxSequence)
                    + " (deleted " + deleted + " added " + "?" + ")");
            result.setCode(CBLStatus.OK);

        } catch (SQLException e) {
            return result;
        } finally {
            if (cursor != null) {
                cursor.close();
            }
            if (!result.isSuccessful()) {
                Log.w(CBLDatabase.TAG, "Failed to rebuild view " + name + ": "
                        + result.getCode());
            }
            if(database != null) {
                database.endTransaction(result.isSuccessful());
            }
        }

        return result;
    }

    public Cursor resultSetWithOptions(CBLQueryOptions options) {
        if (options == null) {
            options = new CBLQueryOptions();
        }

        // OPT: It would be faster to use separate tables for raw-or ascii-collated views so that
        // they could be indexed with the right collation, instead of having to specify it here.
        String collationStr = "";
        if(collation == TDViewCollation.TDViewCollationASCII) {
            collationStr += " COLLATE JSON_ASCII";
        }
        else if(collation == TDViewCollation.TDViewCollationRaw) {
            collationStr += " COLLATE JSON_RAW";
        }

        String sql = "SELECT key, value, docid";
        if (options.isIncludeDocs()) {
            sql = sql + ", revid, json, revs.sequence";
        }
        sql = sql + " FROM maps, revs, docs WHERE maps.view_id=?";

        List<String> argsList = new ArrayList<String>();
        argsList.add(Integer.toString(getViewId()));

        if(options.getKeys() != null) {
            sql += " AND key in (";
            String item = "?";
            for (Object key : options.getKeys()) {
                sql += item;
                item = ", ?";
                argsList.add(toJSONString(key));
            }
            sql += ")";
        }

        Object minKey = options.getStartKey();
        Object maxKey = options.getEndKey();
        boolean inclusiveMin = true;
        boolean inclusiveMax = options.isInclusiveEnd();
        if (options.isDescending()) {
            minKey = maxKey;
            maxKey = options.getStartKey();
            inclusiveMin = inclusiveMax;
            inclusiveMax = true;
        }

        if (minKey != null) {
            assert (minKey instanceof String);
            if (inclusiveMin) {
                sql += " AND key >= ?";
            } else {
                sql += " AND key > ?";
            }
            sql += collationStr;
            argsList.add(toJSONString(minKey));
        }

        if (maxKey != null) {
            assert (maxKey instanceof String);
            if (inclusiveMax) {
                sql += " AND key <= ?";
            } else {
                sql += " AND key < ?";
            }
            sql += collationStr;
            argsList.add(toJSONString(maxKey));
        }

        sql = sql
                + " AND revs.sequence = maps.sequence AND docs.doc_id = revs.doc_id ORDER BY key";
        sql += collationStr;
        if (options.isDescending()) {
            sql = sql + " DESC";
        }
        sql = sql + " LIMIT ? OFFSET ?";
        argsList.add(Integer.toString(options.getLimit()));
        argsList.add(Integer.toString(options.getSkip()));

        Log.v(CBLDatabase.TAG, "Query " + name + ": " + sql);

        Cursor cursor = database.getSqliteDb().rawQuery(sql,
                argsList.toArray(new String[argsList.size()]));
        return cursor;
    }

    // Are key1 and key2 grouped together at this groupLevel?
    public static boolean groupTogether(Object key1, Object key2, int groupLevel) {
        if(groupLevel == 0 || !(key1 instanceof List) || !(key2 instanceof List)) {
            return key1.equals(key2);
        }
        @SuppressWarnings("unchecked")
        List<Object> key1List = (List<Object>)key1;
        @SuppressWarnings("unchecked")
        List<Object> key2List = (List<Object>)key2;
        int end = Math.min(groupLevel, Math.min(key1List.size(), key2List.size()));
        for(int i = 0; i < end; ++i) {
            if(!key1List.get(i).equals(key2List.get(i))) {
                return false;
            }
        }
        return true;
    }

    // Returns the prefix of the key to use in the result row, at this groupLevel
    @SuppressWarnings("unchecked")
    public static Object groupKey(Object key, int groupLevel) {
        if(groupLevel > 0 && (key instanceof List) && (((List<Object>)key).size() > groupLevel)) {
            return ((List<Object>)key).subList(0, groupLevel);
        }
        else {
            return key;
        }
    }

    /*** Querying ***/
    public List<Map<String, Object>> dump() {
        if (getViewId() < 0) {
            return null;
        }

        String[] selectArgs = { Integer.toString(getViewId()) };
        Cursor cursor = null;
        List<Map<String, Object>> result = null;

        try {
            cursor = database
                    .getSqliteDb()
                    .rawQuery(
                            "SELECT sequence, key, value FROM maps WHERE view_id=? ORDER BY key",
                            selectArgs);

            cursor.moveToFirst();
            result = new ArrayList<Map<String, Object>>();
            while (!cursor.isAfterLast()) {
                Map<String, Object> row = new HashMap<String, Object>();
                row.put("seq", cursor.getInt(0));
                row.put("key", cursor.getString(1));
                row.put("value", cursor.getString(2));
                result.add(row);
                cursor.moveToNext();
            }
        } catch (SQLException e) {
            Log.e(CBLDatabase.TAG, "Error dumping view", e);
            return null;
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }

        return result;
    }

    List<CBLQueryRow> reducedQuery(Cursor cursor, boolean group, int groupLevel) throws CBLiteException {

        List<Object> keysToReduce = null;
        List<Object> valuesToReduce = null;
        Object lastKey = null;
        if(getReduce() != null) {
            keysToReduce = new ArrayList<Object>(REDUCE_BATCH_SIZE);
            valuesToReduce = new ArrayList<Object>(REDUCE_BATCH_SIZE);
        }
        List<CBLQueryRow> rows = new ArrayList<CBLQueryRow>();

        cursor.moveToFirst();
        while (!cursor.isAfterLast()) {
            Object keyData = fromJSON(cursor.getBlob(0));
            Object value = fromJSON(cursor.getBlob(1));
            assert(keyData != null);

            if(group && !groupTogether(keyData, lastKey, groupLevel)) {
                if (lastKey != null) {
                    // This pair starts a new group, so reduce & record the last one:
                    Object reduced = (reduceBlock != null) ? reduceBlock.reduce(keysToReduce, valuesToReduce, false) : null;
                    Object key = groupKey(lastKey, groupLevel);
                    CBLQueryRow row = new CBLQueryRow(null, 0, key, reduced, null);
                    rows.add(row);
                    keysToReduce.clear();
                    valuesToReduce.clear();

                }
                lastKey = keyData;
            }
            keysToReduce.add(keyData);
            valuesToReduce.add(value);

            cursor.moveToNext();

        }

        if(keysToReduce.size() > 0) {
            // Finish the last group (or the entire list, if no grouping):
            Object key = group ? groupKey(lastKey, groupLevel) : null;
            Object reduced = (reduceBlock != null) ? reduceBlock.reduce(keysToReduce, valuesToReduce, false) : null;
            CBLQueryRow row = new CBLQueryRow(null, 0, key, reduced, null);
            rows.add(row);
        }

        return rows;

    }

    /**
     * Queries the view. Does NOT first update the index.
     *
     * @param options The options to use.
     * @return An array of CBLQueryRow objects.
     */
    public List<CBLQueryRow> queryWithOptions(CBLQueryOptions options) throws CBLiteException {

        if (options == null) {
            options = new CBLQueryOptions();
        }

        Cursor cursor = null;
        List<CBLQueryRow> rows = new ArrayList<CBLQueryRow>();

        try {
            cursor = resultSetWithOptions(options);
            int groupLevel = options.getGroupLevel();
            boolean group = options.isGroup() || (groupLevel > 0);
            boolean reduce = options.isReduce() || group;

            if (reduce && (reduceBlock == null) && !group) {
                String msg = "Cannot use reduce option in view " + name + " which has no reduce block defined";
                Log.w(CBLDatabase.TAG, msg);
                throw new CBLiteException(new CBLStatus(CBLStatus.BAD_REQUEST));
            }

            if (reduce || group) {
                // Reduced or grouped query:
                rows = reducedQuery(cursor, group, groupLevel);
            } else {
                // regular query
                cursor.moveToFirst();
                while (!cursor.isAfterLast()) {
                    Object keyData = fromJSON(cursor.getBlob(0));
                    Object value = fromJSON(cursor.getBlob(1));
                    String docId = cursor.getString(2);
                    Map<String, Object> docContents = null;
                    if (options.isIncludeDocs()) {
                        // http://wiki.apache.org/couchdb/Introduction_to_CouchDB_views#Linked_documents
                        if (value instanceof Map && ((Map) value).containsKey("_id")) {
                            String linkedDocId = (String) ((Map) value).get("_id");
                            CBLRevisionInternal linkedDoc = database.getDocumentWithIDAndRev(
                                    linkedDocId,
                                    null,
                                    EnumSet.noneOf(TDContentOptions.class)
                            );
                            docContents = linkedDoc.getProperties();
                        } else {
                            docContents = database.documentPropertiesFromJSON(
                                    cursor.getBlob(4),
                                    docId,
                                    cursor.getString(3),
                                    cursor.getLong(5),
                                    options.getContentOptions()
                            );
                        }
                    }
                    CBLQueryRow row = new CBLQueryRow(docId, 0, keyData, value, docContents);
                    rows.add(row);
                    cursor.moveToNext();

                }
            }

        } catch (SQLException e) {
            String errMsg = String.format("Error querying view: %s", this);
            Log.e(CBLDatabase.TAG, errMsg, e);
            throw new CBLiteException(errMsg, e, new CBLStatus(CBLStatus.DB_ERROR));
        } finally {
            if (cursor != null) {
                cursor.close();
            }
        }

        return rows;

    }


    /**
     * Utility function to use in reduce blocks. Totals an array of Numbers.
     */
    public static double totalValues(List<Object>values) {
        double total = 0;
        for (Object object : values) {
            if(object instanceof Number) {
                Number number = (Number)object;
                total += number.doubleValue();
            } else {
                Log.w(CBLDatabase.TAG, "Warning non-numeric value found in totalValues: " + object);
            }
        }
        return total;
    }

    public static CBLViewCompiler getCompiler() {
        return compiler;
    }

    public static void setCompiler(CBLViewCompiler compiler) {
        CBLView.compiler = compiler;
    }

}

abstract class AbstractTouchMapEmitBlock implements CBLMapEmitFunction {

    protected long sequence = 0;

    void setSequence(long sequence) {
        this.sequence = sequence;
    }

}
