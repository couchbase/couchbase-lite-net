//
// CouchbaseConfigSection.cs
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

#if !__IOS__ && !__ANDROID__
using System;
using System.Configuration;
using System.Collections.Generic;
using System.Xml;
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Configuration
{
    public sealed class CouchbaseConfigurationHandler : IConfigurationSectionHandler
    {
        private static readonly string Tag = typeof(CouchbaseConfigurationHandler).Name;

        #region IConfigurationSectionHandler implementation

        public object Create(object parent, object configContext, XmlNode section)
        {
            var retVal = new CouchbaseConfigSection();
            foreach (XmlNode childNode in section.ChildNodes) {
                if (childNode.Name.ToLowerInvariant() == "logging") {
                    retVal.Logging = new LogConfigSection(childNode);
                } else {
                    Log.To.NoDomain.W(Tag, "Unknown element {0} found in configuration file", childNode.Name);
                }
            }

            return retVal;
        }

        #endregion
    }

    internal sealed class CouchbaseConfigSection
    {
        internal LogConfigSection Logging { get; set; }

        internal static string GetNamedAttribute(XmlNode node, string name)
        {
            var val = node.Attributes.GetNamedItem(name);
            return val == null ? null : val.Value;
        }
    }

    internal sealed class LogDomainVerbosityCollection
    {
        private static readonly string Tag = typeof(LogDomainVerbosityCollection).Name;
        private readonly Dictionary<string, Log.LogLevel> _values = new Dictionary<string, Log.LogLevel>();

        public LogDomainVerbosityCollection()
        {
            // empty collection
        }

        public LogDomainVerbosityCollection(XmlNodeList data)
        {
            foreach(XmlNode node in data) {
                var domain = node.Name;
                var verbosityStr = CouchbaseConfigSection.GetNamedAttribute(node, "verbosity");
                if(verbosityStr == null) {
                    Log.To.NoDomain.W(Tag, "Invalid element {0} (missing 'verbosity') in" +
                        " configuration file, skipping...", domain);
                    continue;
                }

                Log.LogLevel level;
                if(!Enum.TryParse<Log.LogLevel>(verbosityStr, true, out level)) {
                    Log.To.NoDomain.W(Tag, "Invalid verbosity {0} ({1}) in configuration file.  " +
                        "Valid values are normal, verbose, debug", verbosityStr, domain);
                    continue;
                }

                if(_values.ContainsKey(domain)) {
                    Log.To.NoDomain.W(Tag, "Duplicate entry for {0} detected, ignoring {1} setting...",
                        domain, verbosityStr);
                    continue;
                }

                _values.Add(domain, level);

            }
        }

        public IDictionary<string, Log.LogLevel> Values
        {
            get {
                return _values;
            }
        }
    }

    internal sealed class LogConfigSection
    {
        private static readonly string Tag = typeof(LogConfigSection).Name;

        private readonly bool _enabled;
        private readonly LogDomainVerbosityCollection _verbositySettings;
		private readonly Log.LogLevel _logLevel = Log.LogLevel.Base;
		private readonly LogScrubSensitivity _scrubSensitivity;

        public bool Enabled
        {
            get { return _enabled; }
        }

		public Log.LogLevel GlobalLevel
		{
			get { return _logLevel; }
		}

		public LogScrubSensitivity ScrubSensitivity
		{
			get { return _scrubSensitivity; }
		}

        public LogDomainVerbosityCollection VerbositySettings
        {
            get { return _verbositySettings; }
        }

        public LogConfigSection(XmlNode data)
        {
            var enabledStr = CouchbaseConfigSection.GetNamedAttribute(data, "enabled");
			if (enabledStr == null || !Boolean.TryParse (enabledStr, out _enabled)) {
				_enabled = true;
			}

			var verbosityStr = CouchbaseConfigSection.GetNamedAttribute (data, "verbosity");
			if (enabledStr == null || !Enum.TryParse<Log.LogLevel> (verbosityStr, true, out _logLevel)) {
				_logLevel = Log.LogLevel.Base;
			}

			var scrubSensitivityStr = CouchbaseConfigSection.GetNamedAttribute (data, "scrubSensitivity");
			if (scrubSensitivityStr == null || !Enum.TryParse<LogScrubSensitivity> (scrubSensitivityStr, true, out _scrubSensitivity)) {
				_scrubSensitivity = LogScrubSensitivity.NoInsecure;
			}

            _verbositySettings = new LogDomainVerbosityCollection();
            foreach (XmlNode childNode in data.ChildNodes) {
                if (childNode.Name == "domains") {
                    _verbositySettings = new LogDomainVerbosityCollection(childNode.ChildNodes);
                } else {
                    Log.To.NoDomain.W(Tag, "Unknown element {0} found in configuration file", childNode.Name);
                }
            }
        }
    }

    
}
#endif
