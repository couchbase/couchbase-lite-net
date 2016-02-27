//
// EmptyClass.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
using System;
using System.Threading.Tasks;
using Couchbase.Lite.Util;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Lite.Configuration
{
    internal static class ConfigurationCreator
    {
        public static IList<ConfigurationSetting> GetSettings()
        {
            return new List<ConfigurationSetting> {
                new LogConfigurationSetting()
            };
        }
    }

    internal static class Configure
    {
        private static readonly string Tag = typeof(Configure).Name;

        public static Task LoadFrom(IConfigLoadSource source)
        {
            return Task.Factory.StartNew(() =>
            {
                var readStream = source.ReadStream;
                if (readStream == null || !readStream.CanRead) {
                    var e = new InvalidOperationException("Cannot read from configuration source");
                    Log.To.All.E(Tag, "Error loading configuration values", e);
                    throw e;
                }

                var savedProps = Manager.GetObjectMapper().ReadValue<IDictionary<string, object>>(readStream);
                foreach(var setting in ConfigurationCreator.GetSettings()) {
                    var saved = savedProps.Get(setting.Key);
                    if(saved != null) {
                        setting.Apply(saved);
                    }
                }
            });
        }

        public static Task SaveTo(IConfigSaveDestination destination)
        {
            var writeStream = destination.WriteStream;
            if (writeStream == null || !writeStream.CanWrite) {
                var e = new InvalidOperationException("Cannot write to configuration destination");
                Log.To.All.E(Tag, "Error saving configuration values", e);
                throw e;
            }

            var toSaveProps = new Dictionary<string, object>();
            foreach(var setting in ConfigurationCreator.GetSettings()) {
                toSaveProps[setting.Key] = setting.Save();
            }

            var bytes = Manager.GetObjectMapper().WriteValueAsBytes(toSaveProps);
            var ms = new MemoryStream(bytes.ToArray());
            return ms.CopyToAsync(destination.WriteStream).ContinueWith(t => ms.Dispose());
        }
    }
}

