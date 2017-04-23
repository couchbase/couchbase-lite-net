using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    public interface IReadOnlyArray : IReadOnlyArrayFragment
    {
        int Count { get; }

        object GetObject(int index);

        string GetString(int index);

        int GetInt(int index);

        long GetLong(int index);

        double GetDouble(int index);

        bool GetBoolean(int index);

        IBlob GetBlob(int index);

        DateTimeOffset GetDate(int index);

        IReadOnlyArray GetArray(int index);

        IReadOnlySubdocument GetSubdocument(int index);

        IList<object> ToArray();
    }
}
