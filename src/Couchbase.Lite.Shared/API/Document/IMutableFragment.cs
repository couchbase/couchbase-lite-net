﻿// 
// IMutableFragment.cs
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

using System;

namespace Couchbase.Lite;

/// <summary>
/// An interface representing a writeable object capable of being indexed 
/// via <see cref="System.String"/>
/// </summary>
public interface IMutableDictionaryFragment
{
    /// <summary>
    /// Gets the value of an arbitrary <see cref="System.String"/> key
    /// </summary>
    /// <param name="key">The key to look up the value for</param>
    /// <returns>The value, or lack thereof, wrapped in a <see cref="IMutableFragment"/></returns>
    IMutableFragment this[string key] { get; }
}

/// <summary>
/// An interface representing a writeable object capable of being indexed
/// via <see cref="System.Int32"/>
/// </summary>
public interface IMutableArrayFragment
{
    /// <summary>
    /// Gets the value of an arbitrary index
    /// </summary>
    /// <param name="index">The index to look up the value for</param>
    /// <returns>The value, or lack thereof, wrapped in a <see cref="IMutableFragment"/></returns>
    IMutableFragment this[int index] { get; }
}

/// <summary>
/// An interface describing a mutable entry in a key-value path
/// on an object.  Note that if the key-value path does not exist,
/// then setting the value will throw an exception.
/// </summary>
public interface IMutableFragment : IMutableArrayFragment, IMutableDictionaryFragment
{
    /// <summary>
    /// Gets or sets the value of the fragment as an untyped object
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a value is attempted to be
    /// set on a key path that does not exist</exception>
    object? Value { get; set; }
        
    /// <summary>
    /// Gets the contained value as a <see cref="MutableArrayObject"/>
    /// </summary>
    MutableArrayObject? Array { get; set; }
        
    /// <summary>
    /// Gets the contained value as a <see cref="Blob"/>
    /// </summary>
    Blob? Blob { get; set; }

    /// <summary>
    /// Gets the contained value as a <see cref="Boolean"/>
    /// </summary>
    bool Boolean { get; set; }
        
    /// <summary>
    /// Gets the contained value as a <see cref="DateTimeOffset"/>
    /// </summary>
    DateTimeOffset Date { get; set; }
        
    /// <summary>
    /// Gets the contained value as a <see cref="MutableDictionaryObject"/>
    /// </summary>
    MutableDictionaryObject? Dictionary { get; set; }

    /// <summary>
    /// Gets the contained value as a <see cref="Double"/>
    /// </summary>
    double Double { get; set; }

    /// <summary>
    /// Gets the contained value as a <see cref="Single"/>
    /// </summary>
    float Float { get; set; }

    /// <summary>
    /// Gets the contained value as an <see cref="Int32"/>
    /// </summary>
    int Int { get; set; }

    /// <summary>
    /// Gets the contained value as an <see cref="Int64"/>
    /// </summary>
    long Long { get; set; }
        
    /// <summary>
    /// Gets the contained value as a <see cref="String"/>
    /// </summary>
    string? String { get; set; }
}