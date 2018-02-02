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

using Couchbase.Lite.Logging;
using Couchbase.Lite.Support;
using Couchbase.Lite.Util;

using JetBrains.Annotations;

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

        [NotNull] private readonly Freezer _freezer = new Freezer();

        [NotNull] private readonly Dictionary<string, object> _params;

        #endregion

        #region Constructors

        /// <summary>
        /// Default Constructor
        /// </summary>
        public Parameters()
        {
            _params = new Dictionary<string, object>();
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="parameters">The object to copy values from</param>
        public Parameters([NotNull]Parameters parameters)
        {
            CBDebug.MustNotBeNull(Log.To.Query, Tag, nameof(parameters), parameters);
            _params = new Dictionary<string, object>(parameters._params);
        }

        internal Parameters([NotNull]Dictionary<string, object> values)
        {
            _params = values;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the untyped value of the given key in the parameters
        /// </summary>
        /// <param name="key">The key to lookup</param>
        /// <returns>The value of the key, or <c>null</c> if it does not exist</returns>
        public object GetValue(string key) => _params.TryGetValue(key, out var existing) ? existing : null;

        /// <summary>
        /// Sets a <see cref="bool"/> value in the parameters
        /// </summary>
        /// <param name="name">The name of the key to set</param>
        /// <param name="value">The value to set</param>
        /// <returns>The parameters object for further processing</returns>
        [NotNull]
        [ContractAnnotation("name:null => halt")]
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
        [NotNull]
        [ContractAnnotation("name:null => halt")]
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
        [NotNull]
        [ContractAnnotation("name:null => halt")]
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
        [NotNull]
        [ContractAnnotation("name:null => halt")]
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
        [NotNull]
        [ContractAnnotation("name:null => halt")]
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
        [NotNull]
        [ContractAnnotation("name:null => halt")]
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
        [NotNull]
        [ContractAnnotation("name:null => halt")]
        public Parameters SetString(string name, string value)
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
        [NotNull]
        [ContractAnnotation("name:null => halt")]
        public Parameters SetValue(string name, object value)
        {
            CBDebug.MustNotBeNull(Log.To.Query, Tag, nameof(name), name);

            _freezer.PerformAction(() => _params[name] = value);
            return this;
        }

        #endregion

        #region Internal Methods

        [NotNull]
        internal Parameters Freeze()
        {
            var retVal = new Parameters(this);
            retVal._freezer.Freeze("Cannot modify a Parameters class while it is in use");
            return retVal;
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        [NotNull]
        public override string ToString()
        {
            return JsonConvert.SerializeObject(_params) ?? "(null)";
        }

        #endregion
    }
}
