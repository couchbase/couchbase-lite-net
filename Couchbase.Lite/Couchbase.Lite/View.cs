using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace Couchbase.Lite {

    public partial class View {

    #region Static Members
        //Properties
        public static IViewCompiler Compiler { get; set; }

    #endregion
    
    #region Instance Members
        //Properties
        public Database Database { get { throw new NotImplementedException(); } }

        public String Name { get { throw new NotImplementedException(); } }

        public MapDelegate Map { get { throw new NotImplementedException(); } }

        public ReduceDelegate Reduce { get { throw new NotImplementedException(); } }

        public Boolean IsStale { get { throw new NotImplementedException(); } }

        public long LastSequenceIndexed { get { throw new NotImplementedException(); } }

        //Methods
        public Boolean SetMap(MapDelegate map, String version) { throw new NotImplementedException(); }

        public Boolean SetMapReduce(MapDelegate map, ReduceDelegate reduce, String version) { throw new NotImplementedException(); }

        public void DeleteIndex() { throw new NotImplementedException(); }

        public void Delete() { throw new NotImplementedException(); }

        public Query CreateQuery() { throw new NotImplementedException(); }

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

    public delegate void MapDelegate(Dictionary<String, Object> document, EmitDelegate emit);

    
    public delegate void EmitDelegate(Object key, Object value);

    
    public delegate Object ReduceDelegate(IEnumerable<Object> keys, IEnumerable<Object> values, Boolean rereduce);

    
}

