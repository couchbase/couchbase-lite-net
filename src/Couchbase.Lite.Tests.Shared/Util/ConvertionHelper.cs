using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Couchbase.Lite.Tests.Shared.Util
{
    public static class ConvertionHelper
    {
        public static IDictionary<string, object> StringToDictionary(string str)
        {
            return JsonConvert.DeserializeObject<IDictionary<string, object>>(str);
        }
        public static byte[] ReadFully(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }
}
