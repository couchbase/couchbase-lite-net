//
// ConfigurationSetting.cs
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
using System.Collections.Generic;
using Couchbase.Lite.Util;
using System.Text;

namespace Couchbase.Lite.Configuration
{
    internal abstract class ConfigurationSetting
    {
        public string Key { get; private set; }

        protected ConfigurationSetting(string key)
        {
            Key = key;
        }

        public abstract object Save();

        public abstract void Apply(object value);
    }

    internal sealed class LogConfigurationSetting : ConfigurationSetting
    {
        public LogConfigurationSetting() : base("LogSetting")
        {

        }

        private string FormatDomain(string domain)
        {
            var toUpper = true;
            var sb = new StringBuilder();

            for(int i = 0; i < domain.Length; i++) {
                if (domain[i] == ' ') {
                    toUpper = true;
                } else {
                    sb.Append(toUpper ? Char.ToUpperInvariant(domain[i]) : Char.ToLowerInvariant(domain[i]));
                    toUpper = false;
                }
            }

            return sb.ToString();
        }

        public override void Apply(object value)
        {
            var root = value.AsDictionary<string, object>();
            Log.Disabled = root.GetCast<bool>("Disabled");
            var levels = root.Get("LogLevels").AsDictionary<string, Log.LogLevel>();
            if (levels == null) {
                return;
            }

            foreach (var pair in levels) {
                var logger = (IDomainLogging)Log.Domains.GetType().GetProperty(pair.Key).GetValue(Log.Domains);
                if (logger == null) {
                    continue;
                }

                logger.Level = pair.Value;
            }
        }

        public override object Save()
        {
            var root = new Dictionary<string, object>();
            root["Disabled"] = Log.Disabled;
            var levels = new Dictionary<string, Log.LogLevel>();
            foreach (var logger in Log.Domains.All) {
                var domain = ((DomainLogger)logger).Domain;
                levels[FormatDomain(domain)] = logger.Level;
            }

            root["LogLevels"] = levels;
            return root;
        }
    }
}

