using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ServiceStack.Text;
using System.Text;

namespace Couchbase.Lite 
{

    public class ObjectWriter 
    {
        readonly Boolean prettyPrintJson;

        public ObjectWriter() : this(false) { }

        public ObjectWriter(Boolean prettyPrintJson)
        {
            this.prettyPrintJson = prettyPrintJson;
        }

        public ObjectWriter WriterWithDefaultPrettyPrinter()
        {
            return new ObjectWriter(true); // Currently doesn't do anything, but could use something like http://www.limilabs.com/blog/json-net-formatter in the future.
        }

        public IEnumerable<Byte> WriteValueAsBytes<T> (T item)
        {
            return Encoding.UTF8.GetBytes(WriteValueAsString<T>(item));
        }

        public string WriteValueAsString<T> (T item)
        {
            return JsonSerializer.SerializeToString(item);
        }

        public T ReadValue<T> (String json)
        {
           return JsonSerializer.DeserializeFromString<T>(json);
        }

        public T ReadValue<T> (IEnumerable<Byte> json)
        {
            using (var jsonStream = new MemoryStream(json.ToArray())) {
                return JsonSerializer.DeserializeFromStream<T>(jsonStream);
            }
        }

        public T ReadValue<T> (Stream jsonStream)
        {
            return JsonSerializer.DeserializeFromStream<T>(jsonStream);
        }
    }
}

