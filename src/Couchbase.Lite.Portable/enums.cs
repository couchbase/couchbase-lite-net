
namespace Couchbase.Lite.Portable
{
    /// <summary>
    /// Used to specify when a <see cref="Couchbase.Lite.View"/> index is updated 
    /// when running a <see cref="Couchbase.Lite.Query"/>.
    /// 
    /// <list type="table">
    /// <listheader>
    /// <term>Name</term>
    /// <description>Description</description>
    /// </listheader>
    /// <item>
    /// <term>Before</term>
    /// <description>
    /// If needed, update the index before running the <see cref="Couchbase.Lite.Query"/> (default). 
    /// This guarantees up-to-date results at the expense of a potential delay in receiving results.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Never</term>
    /// <description>
    /// Never update the index when running a <see cref="Couchbase.Lite.Query"/>. 
    /// This guarantees receiving results the fastest at the expense of potentially out-of-date results.
    /// </description>
    /// </item>
    /// <item>
    /// <term>After</term>
    /// <description>
    /// If needed, update the index asynchronously after running the <see cref="Couchbase.Lite.Query"/>. 
    /// This guarantees receiving results the fastest, at the expense of potentially out-of-date results, 
    /// and that subsequent Queries will return more accurate results.
    /// </description>
    /// </item>    
    /// </list>
    /// </summary>
    public enum IndexUpdateMode
    {
        Before,
        Never,
        After
    }

    public enum AllDocsMode
    {
        AllDocs,
        IncludeDeleted,
        ShowConflicts,
        OnlyConflicts
    }

    #region Enums

    /// <summary>
    /// Describes the status of a <see cref="Couchbase.Lite.Replication"/>.
    /// <list type="table">
    /// <listheader>
    /// <term>Name</term>
    /// <description>Description</description>
    /// </listheader>
    /// <item>
    /// <term>Stopped</term>
    /// <description>
    /// The <see cref="Couchbase.Lite.Replication"/> is finished or hit a fatal error.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Offline</term>
    /// <description>
    /// The remote host is currently unreachable.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Idle</term>
    /// <description>
    /// The continuous <see cref="Couchbase.Lite.Replication"/> is caught up and
    /// waiting for more changes.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Active</term>
    /// <description>
    /// The <see cref="Couchbase.Lite.Replication"/> is actively transferring data.
    /// </description>
    /// </item>
    /// </list>
    /// </summary>
    public enum ReplicationStatus
    {
        /// <summary>The <see cref="Couchbase.Lite.Replication"/> is finished or hit a fatal error. </summary>
        Stopped,
        /// <summary>The remote host is currently unreachable. </summary>
        Offline,
        /// <summary>The continuous <see cref="Couchbase.Lite.Replication"/> is caught up and
        /// waiting for more changes. </summary>
        Idle,
        /// <summary> The <see cref="Couchbase.Lite.Replication"/> is actively transferring data. </summary>
        Active
    }

    #endregion

    // TODO: Either remove or update the API defs to indicate the enum value changes, and global scope.
    /// <summary>
    /// enum used with Couchbase.Lite.View <see cref="Couchbase.Lite.View"/>
    /// </summary>
    public enum ViewCollation
    {
        Unicode,
        Raw,
        ASCII
    }
}