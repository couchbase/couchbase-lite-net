using System;
namespace Couchbase.Lite.Portable
{
    /// <summary>
    /// defines the public contract for a <see cref="Couchbase.Lite.View"/> in a portable manner
    /// </summary>
    public interface IView
    {
        Couchbase.Lite.Portable.IQuery CreateQuery();
        void Delete();
        void DeleteIndex();
        bool IsStale { get; }
        long LastSequenceIndexed { get; }
        Couchbase.Lite.Portable.MapDelegate Map { get; }
        string Name { get; }
        Couchbase.Lite.Portable.ReduceDelegate Reduce { get; set; }
        bool SetMap(Couchbase.Lite.Portable.MapDelegate mapDelegate, string version);
        bool SetMapReduce(Couchbase.Lite.Portable.MapDelegate map, Couchbase.Lite.Portable.ReduceDelegate reduce, string version);
    }
}
