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
using Couchbase.Lite.Util;
using Newtonsoft.Json;

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
        PotentiallyInsecureOk,

        /// <summary>
        /// All information should be logged
        /// </summary>
        AllOk
    }

    internal enum LogMessageSensitivity
    {
        PotentiallyInsecure = 1,
        Insecure
    }

    internal abstract class SecureLogItem
    {
        #region Constants

        protected const int CharLimit = 100;
        protected const string Redacted = "<redacted>";

        #endregion

        #region Variables

        private readonly LogMessageSensitivity _sensitivity;

        #endregion

        #region Properties

        protected bool ShouldLog => (int)_sensitivity <= (int)Log.ScrubSensitivity;

        #endregion

        #region Constructors

        protected SecureLogItem(LogMessageSensitivity sensitivity)
        {
            _sensitivity = sensitivity;
        }

        #endregion
    }

    internal sealed class SecureLogString : SecureLogItem
    {
        #region Variables

        private readonly byte[] _bytes;
        private readonly object _obj;
        private string _string;

        #endregion

        #region Properties

        private string String
        {
            get {
                if (_string != null) {
                    return _string;
                }

                string str;
                if (_bytes != null) {
                    str = Encoding.UTF8.GetString(_bytes);
                } else if (_obj != null) {
                    str = _obj.ToString();
                } else {
                    str = "(null)";
                }

                _string = str.Length > 100 ? $"{new string(str.Take(100).ToArray())}..." : str;

                return _string;
            }
        }

        #endregion

        #region Constructors

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

        #endregion

        #region Overrides

        public override string ToString()
        {
            return ShouldLog ? String : Redacted;
        }

        #endregion
    }

    internal sealed class SecureLogJsonString : SecureLogItem
    {
        #region Variables

        private readonly object _object;
        private string _str;

        #endregion

        #region Properties

        private string String 
        {
            get {
                if (_str != null) {
                    return _str;
                }

                var str = JsonConvert.SerializeObject(_object);
                _str = str.Length > 100 ? $"{new string(str.Take(100).ToArray())}..." : str;

                return _str;
            }
        }

        #endregion

        #region Constructors

        public SecureLogJsonString(object input, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
        {
            _object = input;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return ShouldLog ? String : Redacted;
        }

        #endregion
    }

    internal sealed class SecureLogUri : SecureLogItem
    {
        #region Variables

        private readonly Uri _uri;
        private string _str;

        #endregion

        #region Properties

        private string UriString => _str ?? (_str = _uri.ToString().ReplaceAll("://.*:.*@", "://<redacted>:<redacted>@"));

        #endregion

        #region Constructors

        // Only used for stripping credentials, so always insecure
        public SecureLogUri(Uri uri) : base(LogMessageSensitivity.Insecure)
        {
            _uri = uri;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            return ShouldLog ? UriString : String.Empty;
        }

        #endregion
    }
}

