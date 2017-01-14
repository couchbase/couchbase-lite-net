//
// SecureLogString.cs
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
using System.Linq;
using System.Text;

namespace Couchbase.Lite.Logging
{
    /// <summary>
    /// The sensitivity at which the logger should redact
    /// sensitive information
    /// </summary>
    public enum LogScrubSensitivity
    {
        /// <summary>
        /// No potentially insecure information shall be logged
        /// </summary>
        NoInsecure = 0,

        /// <summary>
        /// Information that might be insecure (i.e. user generated)
        /// may be logger, but access tokens, passwords, etc should still
        /// be redacted
        /// </summary>
        PotentiallyInsecureOK,

        /// <summary>
        /// All information should be logged
        /// </summary>
        AllOK
    }

    internal enum LogMessageSensitivity
    {
        PotentiallyInsecure = 1,
        Insecure
    }

    internal abstract class SecureLogItem
    {
        protected const string Redacted = "<redacted>";
        protected const int CharLimit = 100;

        private readonly LogMessageSensitivity _sensitivity;

        protected bool ShouldLog
        {
            get {
                return (int)_sensitivity <= (int)Log.ScrubSensitivity;
            }
        }

        protected SecureLogItem(LogMessageSensitivity sensitivity)
        {
            _sensitivity = sensitivity;
        }

    }

    internal sealed class SecureLogString : SecureLogItem
    {
        private string _string;
        private readonly byte[] _bytes;
        private readonly object _obj;

        private string String
        {
            get {
                if (_string == null) {
                    var str = default(string);
                    if (_bytes != null) {
                        str = Encoding.UTF8.GetString(_bytes);
                    } else if (_obj != null) {
                        str = _obj.ToString();
                    } else {
                        str = "(null)";
                    }

                    if(str.Length > 100) {
                        _string = $"{new string(str.Take(100).ToArray())}...";
                    } else {
                        _string = str;
                    }
                }

                return _string;
            }
        }

        public SecureLogString(string str, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
        {
            _string = str;
        }

        public SecureLogString(byte[] utf8Bytes, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
        {
            _bytes = utf8Bytes;
        }

        public SecureLogString(object obj, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
        {
            _obj = obj;
        }

        public override string ToString()
        {
            return ShouldLog ? String : Redacted;
        }
    }

    internal sealed class SecureLogJsonString : SecureLogItem
    {
        private readonly object _object;
        private string _str;

       /* private string String 
        {
            get {
                if(_str == null) {
                    var str = Manager.GetObjectMapper().WriteValueAsString(_object);
                    if(str.Length > 100) {
                        _str = $"{new string(str.Take(100).ToArray())}...";
                    } else {
                        _str = str;
                    }
                }

                return _str;
            }
        }*/

        public SecureLogJsonString(object input, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
        {
            _object = input;
        }

        public override string ToString()
        {
            return ShouldLog ? String.Empty : Redacted;
        }
    }

    internal sealed class SecureLogUri : SecureLogItem
    {
        private readonly Uri _uri;
        private string _str;

        /*private string UriString
        {
            get {
                if (_str == null) {
                    _str = _uri.ToString().ReplaceAll("://.*:.*@", "://<redacted>:<redacted>@");
                }

                return _str;
            }
        }*/

        // Only used for stripping credentials, so always insecure
        public SecureLogUri(Uri uri) : base(LogMessageSensitivity.Insecure)
        {
            _uri = uri;
        }

        public override string ToString()
        {
            return ShouldLog ? _uri.ToString() : String.Empty;
        }
    }
}

