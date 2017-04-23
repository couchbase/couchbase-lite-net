using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    public interface IDictionaryObject : IReadOnlyDictionary, IDictionaryFragment
    {
        IDictionaryObject Set(string key, object value);

        IDictionaryObject Set(IDictionary<string, object> dictionary);

        IDictionaryObject Remove(string key);

        new IArray GetArray(string key);

        new ISubdocument GetSubdocument(string key);

        new IFragment this[string key] { get; }
    }
}
