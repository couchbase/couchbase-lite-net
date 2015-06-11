//
// View.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

using System;
using System.Collections.Generic;

using Couchbase.Lite.Util;
using Couchbase.Lite.Store;
using Sharpen;
using Couchbase.Lite.Internal;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.Collections;
using Couchbase.Lite.Views;
using System.Threading;

namespace Couchbase.Lite {

    // TODO: Either remove or update the API defs to indicate the enum value changes, and global scope.
    /// <summary>
    /// Indicates the collation to use for sorted items in the view
    /// </summary>
    [Serializable]
    public enum ViewCollation
    {
        /// <summary>
        /// Sort via the unicode standard
        /// </summary>
        Unicode,
        /// <summary>
        /// Raw binary sort
        /// </summary>
        Raw,
        /// <summary>
        /// Sort via ASCII comparison
        /// </summary>
        ASCII
    }

    /// <summary>
    /// A Couchbase Lite <see cref="Couchbase.Lite.View"/>. 
    /// A <see cref="Couchbase.Lite.View"/> defines a persistent index managed by map/reduce.
    /// </summary>
    public sealed class View : IViewStoreDelegate {

    #region Constructors

        internal static View MakeView(Database database, string name, bool create)
        {
            
            var storage = database.Storage.GetViewStorage(name, create);
            if (storage == null) {
                return null;
            }

            var view = new View();
            view.Storage = storage;
            view.Database = database;
            storage.Delegate = view;
            view.Name = name;

            // means 'unknown'
            view.Collation = ViewCollation.Unicode;
            return view;
        }

    #endregion

    #region Static Members
        /// <summary>
        /// Gets or sets an object that can compile source code into map and reduce delegates.
        /// </summary>
        /// <value>The compiler object.</value>
        public static IViewCompiler Compiler { get; set; }

    #endregion
    
    #region Constants

        internal const String Tag = "View";

        const Int32 ReduceBatchSize = 100;

    #endregion

    #region Non-public Members

        private object _updateLock = new object();
        private string _version;

        internal IViewStore Storage { get; private set; }

        internal ViewCollation Collation { get; set; }


        internal void Close()
        {
            Storage.Close();
            Storage = null;
            Database = null;
        }

        internal void UpdateIndex()
        {
            //TODO: View grouping
            Storage.UpdateIndexes(new List<IViewStore> { Storage });
        }

        private bool GroupOrReduce(QueryOptions options) {
            if (options.Group|| options.GroupLevel> 0) {
                return true;
            }

            if (options.ReduceSpecified) {
                return options.Reduce;
            }

            return Reduce != null;
        }

        /// <summary>Queries the view.</summary>
        /// <remarks>Queries the view. Does NOT first update the index.</remarks>
        /// <param name="options">The options to use.</param>
        /// <returns>An array of QueryRow objects.</returns>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        internal IEnumerable<QueryRow> QueryWithOptions(QueryOptions options)
        {
            if (options == null) {
                options = new QueryOptions();
            }

            IEnumerable<QueryRow> iterator = null;
            if (false) {
                //TODO: Full text
            } else if (GroupOrReduce(options)) {
                iterator = Storage.ReducedQuery(options);
            } else {
                iterator = Storage.RegularQuery(options);
            }

            if (iterator != null) {
                Log.D(Tag, "Query {0}: Returning iterator", Name);
            } else {
                Log.D(Tag, "Query {0}: Failed", Name);
            }

            return iterator;
        }

        /// <summary>Indexing</summary>
        internal string ToJSONString(Object obj)
        {
            if (obj == null)
                return null;

            String result = null;
            try
            {
                result = Manager.GetObjectMapper().WriteValueAsString(obj);
            }
            catch (Exception e)
            {
                Log.W(Database.Tag, "Exception serializing object to json: " + obj, e);
            }
            return result;
        }

        internal Object FromJSON(IEnumerable<Byte> json)
        {
            if (json == null)
            {
                return null;
            }
            object result = null;
            try
            {
                result = Manager.GetObjectMapper().ReadValue<Object>(json);
            }
            catch (Exception e)
            {
                Log.W(Database.Tag, "Exception parsing json", e);
            }
            return result;
        }

        internal static double TotalValues(IList<object> values)
        {
            double total = 0;
            foreach (object o in values)
            {
                try {
                    double number = Convert.ToDouble(o);
                    total += number;
                } 
                catch (Exception e)
                {
                    Log.E(Database.Tag, "Warning non-numeric value found in totalValues: " + o, e);
                }
            }
            return total;
        }

        internal Status CompileFromDesignDoc()
        {
            if (Map != null) {
                return new Status(StatusCode.Ok);
            }

            string language = null;
            var viewProps = Database.GetDesignDocFunction(Name, "views", out language).AsDictionary<string, object>();
            if (viewProps == null) {
                return new Status(StatusCode.NotFound);
            }

            Log.D(Tag, "{0}: Attempting to compile {1} from design doc", Name, language);
            if (Compiler == null) {
                return new Status(StatusCode.NotImplemented);
            }

            return Compile(viewProps, language);
        }

        internal Status Compile(IDictionary<string, object> viewProps, string language)
        {
            language = language ?? "javascript";
            string mapSource = viewProps.Get("map") as string;
            if (mapSource == null) {
                return new Status(StatusCode.NotFound);
            }

            MapDelegate mapDelegate = Compiler.CompileMap(mapSource, language);
            if (mapDelegate == null) {
                Log.W(Tag, "View {0} could not compile {1} map fn: {2}", Name, language, mapSource);
                return new Status(StatusCode.CallbackError);
            }

            string reduceSource = viewProps.Get("reduce") as string;
            ReduceDelegate reduceDelegate = null;
            if (reduceSource != null) {
                reduceDelegate = Compiler.CompileReduce(reduceSource, language);
                if (reduceDelegate == null) {
                    Log.W(Tag, "View {0} could not compile {1} reduce fn: {2}", Name, language, mapSource);
                    return new Status(StatusCode.CallbackError);
                }
            }
                
            string version = Misc.HexSHA1Digest(Manager.GetObjectMapper().WriteValueAsBytes(viewProps));
            SetMapReduce(mapDelegate, reduceDelegate, version);
            //TODO: DocumentType

            var options = viewProps.Get("options").AsDictionary<string, object>();
            Collation = ViewCollation.Unicode;
            if (options != null && options.ContainsKey("collation")) {
                string collation = options["collation"] as string;
                if (collation.ToLower().Equals("raw")) {
                    Collation = ViewCollation.Raw;
                }
            }

            return new Status(StatusCode.Ok);
        }

    #endregion

    #region Instance Members
        /// <summary>
        /// Get the <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        /// <value>
        /// The <see cref="Couchbase.Lite.Database"/> that owns the <see cref="Couchbase.Lite.View"/>.
        /// </value>
        public Database Database { get; private set; }

        /// <summary>
        /// Gets the <see cref="Couchbase.Lite.View"/>'s name.
        /// </summary>
        /// <value>the <see cref="Couchbase.Lite.View"/>'s name.</value>
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
        public ReduceDelegate Reduce { get; set; }

        /// <summary>
        /// Gets if the <see cref="Couchbase.Lite.View"/>'s indices are currently out of date.
        /// </summary>
        /// <value><c>true</c> if this instance is stale; otherwise, <c>false</c>.</value>
        public Boolean IsStale { get { return (LastSequenceIndexed < Database.LastSequenceNumber); } }

        /// <summary>
        /// Gets the last sequence number indexed so far.
        /// </summary>
        /// <value>The last sequence number indexed.</value>
        public Int64 LastSequenceIndexed { 
            get {
                return Storage.LastSequenceIndexed;
            }
        }

        /// <summary>
        /// Gets the total number of rows present in the view
        /// </summary>
        public int TotalRows {
            get {
                return Storage.TotalRows;
            }
        }

        /// <summary>
        /// Defines the <see cref="Couchbase.Lite.View"/>'s <see cref="Couchbase.Lite.MapDelegate"/> and sets 
        /// its <see cref="Couchbase.Lite.ReduceDelegate"/> to null.
        /// </summary>
        /// <returns>
        /// True if the <see cref="Couchbase.Lite.MapDelegate"/> was set, otherwise false. If the values provided are 
        /// identical to the values that are already set, then the values will not be updated and false will be returned.  
        /// In addition, if true is returned, the index was deleted and will be rebuilt on the next 
        /// <see cref="Couchbase.Lite.Query"/> execution.
        /// </returns>
        /// <param name="mapDelegate">The <see cref="Couchbase.Lite.MapDelegate"/> to set</param>
        /// <param name="version">
        /// The key of the property value to return. The value of this parameter must change when 
        /// the <see cref="Couchbase.Lite.MapDelegate"/> is changed in a way that will cause it to 
        /// produce different results.
        /// </param>
        public Boolean SetMap(MapDelegate mapDelegate, String version) {
            return SetMapReduce(mapDelegate, null, version);
        }

        /// <summary>
        /// Defines the View's <see cref="Couchbase.Lite.MapDelegate"/> 
        /// and <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
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
        /// <returns>
        /// <c>true</c> if the <see cref="Couchbase.Lite.MapDelegate"/> 
        /// and <see cref="Couchbase.Lite.ReduceDelegate"/> were set, otherwise <c>false</c>.
        /// If the values provided are identical to the values that are already set, 
        /// then the values will not be updated and <c>false</c> will be returned. 
        /// In addition, if <c>true</c> is returned, the index was deleted and 
        /// will be rebuilt on the next <see cref="Couchbase.Lite.Query"/> execution.
        /// </returns>
        /// <param name="map">The <see cref="Couchbase.Lite.MapDelegate"/> to set.</param>
        /// <param name="reduce">The <see cref="Couchbase.Lite.ReduceDelegate"/> to set.</param>
        /// <param name="version">
        /// The key of the property value to return. The value of this parameter must change 
        /// when the <see cref="Couchbase.Lite.MapDelegate"/> and/or <see cref="Couchbase.Lite.ReduceDelegate"/> 
        /// are changed in a way that will cause them to produce different results.
        /// </param>
        public bool SetMapReduce(MapDelegate map, ReduceDelegate reduce, string version) { 
            System.Diagnostics.Debug.Assert(map != null);
            System.Diagnostics.Debug.Assert(version != null); // String.Empty is valid.

            var success = true;
            if (_version != version) {
                Map = map;
                Reduce = reduce;
                success = Storage.SetVersion(version);
                _version = version;
            }

            return success;
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>'s persistent index. 
        /// The index is regenerated on the next <see cref="Couchbase.Lite.Query"/> execution.
        /// </summary>
        public void DeleteIndex()
        {
            Storage.DeleteIndex();
        }

        /// <summary>
        /// Deletes the <see cref="Couchbase.Lite.View"/>.
        /// </summary>
        public void Delete()
        { 
            Storage.DeleteView();
            Database.ForgetView(Name);
            Close();
        }

        /// <summary>
        /// Creates a new <see cref="Couchbase.Lite.Query"/> for this view.
        /// </summary>
        /// <returns>A new <see cref="Couchbase.Lite.Query"/> for this view.</returns>
        public Query CreateQuery() {
            return new Query(Database, this);
        }

    #endregion

        #region IViewStoreDelegate

        public MapDelegate MapBlock
        {
            get
            {
                var map = Map;
                if (map == null) {
                    if (CompileFromDesignDoc().IsSuccessful) {
                        map = Map;
                    }
                }

                return map;
            }
        }

        public ReduceDelegate ReduceBlock
        {
            get
            {
                return Reduce;
            }
        }

        public string MapVersion
        {
            get
            {
                return _version;
            }
        }

        public string DocumentType
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        #endregion
    
    }

    /// <summary>
    /// An object that can be used to compile source code into map and reduce delegates.
    /// </summary>
    public interface IViewCompiler {

    #region Instance Members
        //Methods
        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.MapDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.MapDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.MapDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        MapDelegate CompileMap(String source, String language);

        /// <summary>
        /// Compiles source code into a <see cref="Couchbase.Lite.ReduceDelegate"/>.
        /// </summary>
        /// <returns>A compiled <see cref="Couchbase.Lite.ReduceDelegate"/>.</returns>
        /// <param name="source">The source code to compile into a <see cref="Couchbase.Lite.ReduceDelegate"/>.</param>
        /// <param name="language">The language of the source.</param>
        ReduceDelegate CompileReduce(String source, String language);

    #endregion
    
    }

    #region Global Delegates

    /// <summary>
    /// A delegate that is invoked when a <see cref="Couchbase.Lite.Document"/> 
    /// is being added to a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="document">The <see cref="Couchbase.Lite.Document"/> being mapped.</param>
    /// <param name="emit">The delegate to use to add key/values to the <see cref="Couchbase.Lite.View"/>.</param>
    public delegate void MapDelegate(IDictionary<String, Object> document, EmitDelegate emit);
        
    /// <summary>
    /// A delegate that can be invoked to add key/values to a <see cref="Couchbase.Lite.View"/> 
    /// during a <see cref="Couchbase.Lite.MapDelegate"/> call.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    public delegate void EmitDelegate(Object key, Object value);
        
    /// <summary>
    /// A delegate that can be invoked to summarize the results of a <see cref="Couchbase.Lite.View"/>.
    /// </summary>
    /// <param name="keys">A list of keys to be reduced, or null if this is a rereduce.</param>
    /// <param name="values">A parallel array of values to be reduced, corresponding 1-to-1 with the keys.</param>
    /// <param name="rereduce"><c>true</c> if the input values are the results of previous reductions, otherwise <c>false</c>.</param>
    public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    #endregion
}

