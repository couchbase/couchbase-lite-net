using System;
using Couchbase.Lite;


public class ExampleConflictResolver : IConflictResolver
{
    public ExampleConflictResolver()
    {
            
    }

    public ReadOnlyDocument Resolve(Conflict conflict)
    {
        var baseProperties = conflict.Base;
        var mine = conflict.Mine;
        var theirs = conflict.Theirs;

        return theirs;
    }
}

