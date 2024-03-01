// 
//  Parameters.cs
// 
//  Copyright (c) 2017 Couchbase, Inc All rights reserved.
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
using System.Diagnostics.CodeAnalysis;
using Couchbase.Lite.Internal.Doc;
using Couchbase.Lite.Internal.Logging;
using Couchbase.Lite.Internal.Query;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using LiteCore.Interop;

using Newtonsoft.Json;

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// A class which contains parameters for an <see cref="IQuery"/>
    /// </summary>
    public sealed class Parameters
    {
        #region Constants

        private const string Tag = nameof(Parameters);

        #endregion

        #region Variables

        private readonly Freezer _freezer = new Freezer();

        private readonly Dictionary<string, object?> _params;

        // LiveQuerier needs QueryBase to SetParameters.
        private QueryBase? _query;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Parameters()
        {
            _params = new Dictionary<string, object?>();
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="parameters">The object to copy values from</param>
        public Parameters(Parameters parameters)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(parameters), parameters);
            _params = new Dictionary<string, object?>(parameters._params);
        }

        internal Parameters(Dictionary<string, object?> values)
        {
            _params = values;
        }

        internal Parameters(QueryBase query)
        {
            _params = new Dictionary<string, object?>();
            _query = query;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the untyped value of the given key in the parameters
        /// </summary>
        /// <param name="key">The key to lookup</param>
        /// <returns>The value of the key, or <c>null</c> if it does not exist</returns>
        public object? GetValue(string key) => 
            _params.TryGetValue(CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(key), key), out var existing) ? existing : null;

        /// <summary>
        /// Sets a <see cref="Blob"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetBlob(string name, Blob? value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets a <see cref="bool"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetBoolean(string name, bool value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets a <see cref="DateTimeOffset"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetDate(string name, DateTimeOffset value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets a <see cref="Double"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetDouble(string name, double value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets a <see cref="Single"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetFloat(string name, float value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets an <see cref="Int32"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetInt(string name, int value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets an <see cref="Int64"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetLong(string name, long value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets a <see cref="String"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetString(string name, string? value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets an <see cref="ArrayObject"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetArray(string name, ArrayObject? value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets an <see cref="DictionaryObject"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetDictionary(string name, DictionaryObject? value)
        {
            SetValue(name, value);
            return this;
        }

        /// <summary>
        /// Sets an untyped value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        public Parameters SetValue(string name, object? value)
        {
            CBDebug.MustNotBeNull(WriteLog.To.Query, Tag, nameof(name), name);

            _freezer.PerformAction(() => _params[name] = value);
            _query?.SetParameters(this.ToString());

            return this;
        }

        #endregion

        #region Internal Methods

        internal Parameters Freeze()
        {
            var retVal = new Parameters(this);
            retVal._freezer.Freeze("Cannot modify a Parameters class while it is in use");
            return retVal;
        }

        internal FLSliceResult FLEncode()
        {
            return _params.FLEncode();
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override string ToString()
        {
            return JsonConvert.SerializeObject(_params) ?? "(null)";
        }

        #endregion
    }
}
