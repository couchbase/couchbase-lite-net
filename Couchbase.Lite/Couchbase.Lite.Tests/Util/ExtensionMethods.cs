using System;
using System.Collections;
using Sharpen;
using System.Text;

namespace Couchbase.Lite.Tests
{
    public static class ExtensionMethods
    {
        public static void Load(this Hashtable props, InputStream stream)
        {
            using (var reader = new InputStreamReader(stream, Encoding.UTF8))
            {
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (!String.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    {
                        var parts = line.Split('=');
                        if (parts.Length != 2)
                            throw new InvalidOperationException("Properties must be key value pairs separated by an '='.");

                        if (!props.ContainsKey(parts[0]))
                            props.Add(parts[0], parts[1]);
                        else
                            props[parts[0]] = parts[1];
                    }
                }
            }
        }
    }
}

