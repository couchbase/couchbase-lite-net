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
using System;
using System.Collections.Generic;
using System.IO;

namespace Couchbase.Lite
{
    /// <summary>
    /// An enum representing the current Token being parsed
    /// in a JSON stream
    /// </summary>
    public enum JsonToken
    {
        /// <summary>
        /// No token
        /// </summary>
        None,
        /// <summary>
        /// Start of an object ("{")
        /// </summary>
        StartObject,
        /// <summary>
        /// Start of an array ("[")
        /// </summary>
        StartArray,
        /// <summary>
        /// Start of a JSON constructor
        /// </summary>
        StartConstructor,
        /// <summary>
        /// An object property name
        /// </summary>
        PropertyName,
        /// <summary>
        /// A comment
        /// </summary>
        Comment,
        /// <summary>
        /// Raw JSON
        /// </summary>
        Raw,
        /// <summary>
        /// An integer
        /// </summary>
        Integer,
        /// <summary>
        /// A float
        /// </summary>
        Float,
        /// <summary>
        /// A string
        /// </summary>
        String,
        /// <summary>
        /// A boolean
        /// </summary>
        Boolean,
        /// <summary>
        /// A null token
        /// </summary>
        Null,
        /// <summary>
        /// An undefined token
        /// </summary>
        Undefined,
        /// <summary>
        /// End of an object ("}")
        /// </summary>
        EndObject,
        /// <summary>
        /// End of an array ("]")
        /// </summary>
        EndArray,
        /// <summary>
        /// A constructor end token.
        /// </summary>
        EndConstructor,
        /// <summary>
        /// A date
        /// </summary>
        Date,
        /// <summary>
        /// Byte data
        /// </summary>
        Bytes
    }

    /// <summary>
    /// An interface describing a class that can serialize .NET objects 
    /// to and from their JSON representation
    /// </summary>
    public interface IJsonSerializer : IDisposable
    {

        /// <summary>
        /// Gets the current token when parsing in streaming mode
        /// </summary>
        JsonToken CurrentToken { get; }

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
        /// Starts parsing a stream of JSON incrementally, rather than serializing
        /// the entire object into memory
        /// </summary>
        /// <param name="json">The stream containing JSON data</param>
        void StartIncrementalParse(Stream json);

        /// <summary>
        /// Reads the next token from a JSON stream.  Note that an incremental parse
        /// must be started first.
        /// </summary>
        /// <returns>True if another token was read, false if an incremental parse is not started
        /// or no more tokens are left</returns>
        bool Read();

        /// <summary>
        /// A convenience function for deserializing the next object in a stream into
        /// a .NET object
        /// </summary>
        /// <returns>The deserialized object, or null if unable to deserialize</returns>
        IDictionary<string, object> DeserializeNextObject();

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

        /// <summary>
        /// Makes a deep copy of the serializer in order to start an incremental parse
        /// that is disposable.
        /// </summary>
        /// <returns>The cloned object</returns>
        IJsonSerializer DeepClone();

        /// <summary>
        /// Deserializes the CBForest key into a .NET object of a given type
        /// </summary>
        /// <returns>The deserialized key</returns>
        /// <param name="keyReader">The CBForest key reader instance to read from</param>
        /// <typeparam name="T">The type of object to deserialize into</typeparam>
        T DeserializeKey<T>(CBForest.C4KeyReader keyReader);

        /// <summary>
        /// Serializes the given object into a CBForest key
        /// </summary>
        /// <returns>The serialized key</returns>
        /// <param name="keyValue">The object to serialize</param>
        unsafe CBForest.C4Key* SerializeToKey(object keyValue);

    }
}

