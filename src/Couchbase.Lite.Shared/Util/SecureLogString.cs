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

namespace Couchbase.Lite.Util
{
    internal enum LogScrubSensitivity
    {
        NoInsecure = 0,
        PotentiallyInsecureOK,
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
        private readonly string _string;

        public SecureLogString(string str, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
        {
            _string = str;
        }

        public override string ToString()
        {
            return ShouldLog ? _string : Redacted;
        }
    }

    internal sealed class SecureLogJsonString : SecureLogItem
    {
        private readonly object _object;
        private string _str;

        private string String 
        {
            get {
                if(_str == null) {
                    _str = Manager.GetObjectMapper().WriteValueAsString(_object);
                }

                return _str;
            }
        }

        public SecureLogJsonString(object input, LogMessageSensitivity sensitivityLevel) : base(sensitivityLevel)
        {
            _object = input;
        }

        public override string ToString()
        {
            return ShouldLog ? String : Redacted;
        }
    }

    internal sealed class SecureLogUri : SecureLogItem
    {
        private readonly Uri _uri;
        private string _str;

        private string UriString
        {
            get {
                if (_str == null) {
                    _str = _uri.ToString().ReplaceAll("://.*:.*@", "://<redacted>:<redacted>@");
                }

                return _str;
            }
        }

        // Only used for stripping credentials, so always insecure
        public SecureLogUri(Uri uri) : base(LogMessageSensitivity.Insecure)
        {
            _uri = uri;
        }

        public override string ToString()
        {
            return ShouldLog ? _uri.ToString() : UriString;
        }
    }
}

