//
// SecureLogString.cs
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
using JetBrains.Annotations;
using Newtonsoft.Json;
using System.Linq;
using System.Text;

namespace Couchbase.Lite.Logging
{
    internal enum LogMessageSensitivity
    {
        PotentiallyInsecure = 1,
        Insecure
    }

    internal abstract class SecureLogItem
    {
        #region Constants

        protected const int CharLimit = 100;

        #endregion

        #region Variables

        private readonly LogMessageSensitivity _sensitivity;

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

        [NotNull]
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

                _string = str.Length > 100 ? $"{new string(str.Take(CharLimit).ToArray())}..." : str;

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

        [NotNull]
        public override string ToString() => String;

        #endregion
    }

    internal sealed class SecureLogJsonString : SecureLogItem
    {
        #region Variables

        private readonly object _object;
        private string _str;

        #endregion

        [NotNull]
        #region Properties

        private string String 
        {
            get {
                if (_str != null) {
                    return _str;
                }

                var str = JsonConvert.SerializeObject(_object) ?? "(null)";
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

        [NotNull]
        public override string ToString() => String;

        #endregion
    }
}

