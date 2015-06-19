//
//  IObjectMapper.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Collections.Generic;
using System.IO;

namespace Couchbase.Lite
{

    /// <summary>
    /// An interface describing a class that can serialize .NET objects 
    /// to and from their JSON representation
    /// </summary>
    public interface IJsonSerializer
    {
        /// <summary>
        /// Convert an object to a JSON string
        /// </summary>
        /// <returns>The JSON string</returns>
        /// <param name="obj">The object to convert</param>
        /// <param name="pretty">Whether or not to use pretty printing</param>
        string SerializeObject(object obj, bool pretty);

        /// <summary>
        /// Converts a JSON string to a typed object
        /// </summary>
        /// <returns>The object</returns>
        /// <param name="json">The json string to parse</param>
        /// <typeparam name="T">The type of object to return</typeparam>
        T DeserializeObject<T>(string json);

        /// <summary>
        /// Reads a stream and converts the contained data to a typed object
        /// </summary>
        /// <param name="json">The stream to read from</param>
        /// <returns>The parsed object</returns>
        /// <typeparam name="T">The type of object to return</typeparam>
        T Deserialize<T>(Stream json);

        /// <summary>
        /// Converts the object from its intermediary JSON dictionary class to a .NET dictionary,
        /// if applicable.
        /// </summary>
        /// <returns>The .NET dictionary, or null if the object cannot be converted</returns>
        /// <param name="obj">The object to try to convert</param>
        /// <typeparam name="K">The key type of the dictionary</typeparam>
        /// <typeparam name="V">The value type of the dictionary</typeparam>
        IDictionary<K, V> ConvertToDictionary<K, V>(object obj);

        /// <summary>
        /// Converts the object from its intermediary JSON array class to a .NET list,
        /// if applicable.
        /// </summary>
        /// <returns>The .NET list, or null if the object cannot be converted</returns>
        /// <param name="obj">The object to try to convert</param>
        /// <typeparam name="T">The type of object in the list</typeparam>
        IList<T> ConvertToList<T>(object obj);
    }
}

