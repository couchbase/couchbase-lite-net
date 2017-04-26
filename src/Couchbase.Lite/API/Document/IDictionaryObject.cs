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

        new ArrayObject GetArray(string key);

        new Subdocument GetSubdocument(string key);

        new Fragment this[string key] { get; }
    }
}
