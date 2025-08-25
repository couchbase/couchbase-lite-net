// 
// AtomicBool.cs
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Threading;

namespace Couchbase.Lite.Util;

internal struct AtomicBool(bool value)
{
    private int _value = value ? 1 : 0;

    public bool Set(bool value) => 
        Interlocked.Exchange(ref _value, value ? 1 : 0) != 0;

    public static implicit operator bool(AtomicBool value) => 
        value._value != 0;

    public static implicit operator AtomicBool(bool value) => 
        new(value);
}