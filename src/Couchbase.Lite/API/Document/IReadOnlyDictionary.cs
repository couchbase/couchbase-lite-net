using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    public interface IReadOnlyDictionary : IReadOnlyDictionaryFragment
    {
        int Count { get; }

        object GetObject(string key);

        string GetString(string key);

        int GetInt(string key);

        long GetLong(string key);

        double GetDouble(string key);

        bool GetBoolean(string key);

        IBlob GetBlob(string key);

        DateTimeOffset GetDate(string key);

        IReadOnlyArray GetArray(string key);

        IReadOnlySubdocument GetSubdocument(string key);

        IDictionary<string, object> ToDictionary();

        bool Contains(string key);

        ICollection<string> AllKeys();
    }
}
