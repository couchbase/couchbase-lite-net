// 
//  Freezer.cs
// 
//  Author:
//   Jim Borden  <jim.borden@couchbase.com>
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

using System;
using System.Runtime.CompilerServices;

using JetBrains.Annotations;

namespace Couchbase.Lite.Support
{
    internal sealed class Freezer
    {
        #region Variables

        private bool _frozen;
        private string _message;

        #endregion

        #region Public Methods

        public void Freeze([NotNull]string message)
        {
            _frozen = true;
            _message = message;
        }

        public void PerformAction([NotNull]Action a, [CallerMemberName]string caller = null)
        {
            if (_frozen) {
                throw new InvalidOperationException($"Attempt to modify a frozen object '{caller}' ({_message})");
            }

            a();
        }

        public void SetValue<T>(ref T location, T newValue, [CallerMemberName]string caller = null)
        {
            if (_frozen) {
                throw new InvalidOperationException($"Attempt to modify a frozen object '{caller}' ({_message})");
            }

            location = newValue;
        }

        #endregion
    }
}