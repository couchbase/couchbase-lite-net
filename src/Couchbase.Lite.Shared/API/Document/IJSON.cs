// 
// IFragment.cs
// 
// Copyright (c) 2022 Couchbase, Inc All rights reserved.
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

namespace Couchbase.Lite;

/// <summary>
/// An interface describing an object that can be serialized to JSON
/// </summary>
public interface IJSON
{
    /// <summary>
    /// Converts this object to JSON format string.
    /// </summary>
    /// <returns>The contents of this object in JSON format string</returns>
    /// <exception cref="NotSupportedException">Thrown if ToJSON is called from <see cref="MutableDocument"/>,  
    /// <see cref="MutableDictionaryObject"/>, or <see cref="MutableArrayObject"/></exception>
    string ToJSON();
}