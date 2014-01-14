using System;
using System.Collections;
using System.Collections.Generic;
using ServiceStack.Text;
using System.IO;
using System.Linq;

namespace Couchbase.Lite {

    public class ObjectWriter {

        public ObjectWriter ()
        {
            throw new NotImplementedException ();
        }

        public ObjectWriter WriterWithDefaultPrettyPrinter() {
            throw new NotImplementedException ();
        }

        public IEnumerable<Byte> WriteValueAsBytes (Object properties)
        {
            throw new NotImplementedException ();
        }

        public string WriteValueAsString (object key)
        {
            throw new NotImplementedException ();
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

