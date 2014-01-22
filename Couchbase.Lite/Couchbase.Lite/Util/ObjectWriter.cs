using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

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
            var json = WriteValueAsString<T>(item);
            return Encoding.UTF8.GetBytes(json);
        }

        public string WriteValueAsString<T> (T item)
        {
            return JsonConvert.SerializeObject(item, prettyPrintJson ? Formatting.Indented : Formatting.None);
        }

        public T ReadValue<T> (String json)
        {
            var item = JsonConvert.DeserializeObject<T>(json);
            return item;
        }

        public T ReadValue<T> (IEnumerable<Byte> json)
        {
            using (var jsonStream = new MemoryStream(json.ToArray())) 
            using (var jsonReader = new JsonTextReader(new StreamReader(jsonStream))) 
            {
                var serializer = new JsonSerializer();
                var item = serializer.Deserialize<T>(jsonReader);
                return item;
            }
        }

        public T ReadValue<T> (Stream jsonStream)
        {
            using (var jsonReader = new JsonTextReader(new StreamReader(jsonStream))) 
            {
                var serializer = new JsonSerializer();
                var item = serializer.Deserialize<T>(jsonReader);
                return item;
            }
        }
    }
}

