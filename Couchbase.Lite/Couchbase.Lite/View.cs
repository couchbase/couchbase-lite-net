using System;
using System.Collections.Generic;

using Couchbase.Lite.Util;
using Couchbase.Lite.Storage;

namespace Couchbase.Lite {

    // TODO: Either remove or update the API defs to indicate the enum value changes, and global scope.
    public enum ViewCollation
    {
        Unicode,
        Raw,
        ASCII
    }

    public partial class View {

    #region Constructors

        internal View(Database database, String name)
        {
            Database = database;
            Name = name;
            _id = -1;
            // means 'unknown'
            Collation = ViewCollation.Unicode;
        }

    #endregion

    #region Static Members
        /// <summary>
        /// Gets or sets an object that can compile source code into map and reduce delegates.
        /// </summary>
        /// <value>The compiler.</value>
        public static IViewCompiler Compiler { get; set; }

    #endregion
    
    #region Non-public Members

        private Int32 _id;

        private ViewCollation Collation { get; set; }

        internal Int32 Id {
            get {
                if (_id < 0)
                {
                    string sql = "SELECT view_id FROM views WHERE name=?";
                    var args = new [] { Name };
                    Cursor cursor = null;
                    try
                    {
                        cursor = Database.StorageEngine.RawQuery(sql, args);
                        if (cursor.MoveToNext())
                        {
                            _id = cursor.GetInt(0);
                        }
                        else
                        {
                            _id = 0;
                        }
                    }
                    catch (SQLException e)
                    {
                        Log.E(Database.Tag, "Error getting view id", e);
                        _id = 0;
                    }
                    finally
                    {
                        if (cursor != null)
                        {
                            cursor.Close();
                        }
                    }
                }
                return _id;
            }
        }

        internal void DatabaseClosing()
        {
            Database = null;
            _id = 0;
        }

    #endregion

    #region Instance Members
        /// <summary>Get the <see cref="Couchbase.Lite.Database"/> that owns this <see cref="Couchbase.Lite.View"/>.</summary>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s name.
        /// </summary>
        /// <value>The name.</value>
        public String Name { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.MapDelegate"/>.
        /// </summary>
        /// <value>The map function.</value>
        public MapDelegate Map { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
        /// <value>The reduce function.</value>
        public ReduceDelegate Reduce { get; private set; }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.View"/>'s indices are currently out of date.
        /// </summary>
        /// <value><c>true</c> if this instance is stale; otherwise, <c>false</c>.</value>
        public Boolean IsStale { get { return (LastSequenceIndexed < Database.GetLastSequenceNumber()); } }

        /// <summary>
        /// Gets the last sequence number indexed so far.
        /// </summary>
        /// <value>The last sequence indexed.</value>
        public Int64 LastSequenceIndexed { 
            get {
                var sql = "SELECT lastSequence FROM views WHERE name=?"; // TODO: Convert to ADO string params.
                var args = new[] { Name };
                Cursor cursor = null;
                var result = -1L;
                try
                {
                    Log.D(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " start running query: " + sql);
                    cursor = Database.StorageEngine.RawQuery(sql, args);
                    Log.D(Database.TagSql, Sharpen.Thread.CurrentThread().GetName() + " finish running query: " + sql);

                    if (cursor.MoveToNext())
                    {
                        result = cursor.GetLong(0);
                    }
                }
                catch (Exception)
                {
                    Log.E(Database.Tag, "Error getting last sequence indexed");
                }
                finally
                {
                    if (cursor != null)
                    {
                        cursor.Close();
                    }
                }
                return result;
            }
        }

        /// <summary>Defines the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.MapDelegate"/> and sets 
        /// its <see cref="Couchbase.Lite.ReduceDelegate"/> to null.</summary>
        /// <returns>
        /// True if the <see cref="Couchbase.Lite.MapDelegate"/> was set, otherwise false.  If the values provided are 
        /// identical to the values that are already set, then the values will not be updated and false will be returned.  
        /// In addition, if true is returned, the index was deleted and will be rebuilt on the next 
        /// <see cref="Couchbase.Lite.Query"/> execution.
        /// </returns>
        public Boolean SetMap(MapDelegate mapDelegate, String version) {
            return SetMapReduce(mapDelegate, null, version);
        }

        /// <summary>Defines a view's functions.</summary>
        /// <remarks>
        /// Defines a view's functions.
        /// The view's definition is given as a class that conforms to the Mapper or
        /// Reducer interface (or null to delete the view). The body of the block
        /// should call the 'emit' object (passed in as a paramter) for every key/value pair
        /// it wants to write to the view.
        /// Since the function itself is obviously not stored in the database (only a unique
        /// string idenfitying it), you must re-define the view on every launch of the app!
        /// If the database needs to rebuild the view but the function hasn't been defined yet,
        /// it will fail and the view will be empty, causing weird problems later on.
        /// It is very important that this block be a law-abiding map function! As in other
        /// languages, it must be a "pure" function, with no side effects, that always emits
        /// the same values given the same input document. That means that it should not access
        /// or change any external state; be careful, since callbacks make that so easy that you
        /// might do it inadvertently!  The callback may be called on any thread, or on
        /// multiple threads simultaneously. This won't be a problem if the code is "pure" as
        /// described above, since it will as a consequence also be thread-safe.
        /// </remarks>
        public Boolean SetMapReduce(MapDelegate map, ReduceDelegate reduce, String version) { 
            System.Diagnostics.Debug.Assert((map != null));
            System.Diagnostics.Debug.Assert(!String.IsNullOrWhiteSpace(version));

            Map = map;
            Reduce = reduce;

            if (!Database.Open())
            {
                return false;
            }
            // Update the version column in the database. This is a little weird looking
            // because we want to
            // avoid modifying the database if the version didn't change, and because the
            // row might not exist yet.
            var storageEngine = this.Database.StorageEngine;

            // Older Android doesnt have reliable insert or ignore, will to 2 step
            // FIXME review need for change to execSQL, manual call to changes()
            var sql = "SELECT name, version FROM views WHERE name=?"; // TODO: Convert to ADO params.
            var args = new [] { Name };
            Cursor cursor = null;

            try
            {
                cursor = storageEngine.RawQuery(sql, args);

                if (!cursor.MoveToNext())
                {
                    // no such record, so insert
                    var insertValues = new ContentValues();
                    insertValues.Put("name", Name);
                    insertValues.Put("version", version);
                    storageEngine.Insert("views", null, insertValues);
                    return true;
                }

                var updateValues = new ContentValues();
                updateValues.Put("version", version);
                updateValues.Put("lastSequence", 0);

                var whereArgs = new [] { Name, version };
                var rowsAffected = storageEngine.Update("views", updateValues, "name=? AND version!=?", whereArgs);

                return (rowsAffected > 0);
            }
            catch (SQLException e)
            {
                Log.E(Database.Tag, "Error setting map block", e);
                return false;
            }
            finally
            {
                if (cursor != null)
                {
                    cursor.Close();
                }
            }
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>'s persistent index.  The index is regenerated on the next <see cref="Couchbase.Lite.Query"/> execution.
        /// </summary>
        public void DeleteIndex()
        {
            if (Id < 0)
                return;

            var success = false;

            try
            {
                Database.BeginTransaction();

                var whereArgs = new string[] { Sharpen.Extensions.ToString(Id) };
                Database.StorageEngine.Delete("maps", "view_id=?", whereArgs);

                var updateValues = new ContentValues();
                updateValues.Put("lastSequence", 0);

                Database.StorageEngine.Update("views", updateValues, "view_id=?", whereArgs); // TODO: Convert to ADO params.

                success = true;
            }
            catch (SQLException e)
            {
                Log.E(Database.Tag, "Error removing index", e);
            }
            finally
            {
                Database.EndTransaction(success);
            }
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        public void Delete()
        { 
            Database.DeleteViewNamed(Name);
            _id = 0;
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Query"/> for this view.
        /// </summary>
        /// <returns>The query.</returns>
        public Query CreateQuery() {
            return new Query(Database, this);
        }

    #endregion
    
    #region Delegates
        public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    #endregion
    
    }

    public partial interface IViewCompiler {

    #region Instance Members
        //Methods
        MapDelegate CompileMap(String source, String language);

        ReduceDelegate CompileReduce(String source, String language);

    #endregion
    
    }

    #region Global Delegates

    public delegate void MapDelegate(Dictionary<String, Object> document, EmitDelegate emit);
        
    public delegate void EmitDelegate(Object key, Object value);
        
    public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    #endregion
}

