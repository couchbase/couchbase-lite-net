// 
// IParameters.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
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

namespace Couchbase.Lite.Query
{
    /// <summary>
    /// An interface presenting parameters for use in an <see cref="IQuery"/>
    /// </summary>
    public interface IParameters
    {
        #region Public Methods

        /// <summary>
        /// Sets a named parameter
        /// </summary>
        /// <param name="name">The name of the parameter for substitution</param>
        /// <param name="value">The value of the parameter</param>
        /// <returns>The modified parameters object</returns>
        IParameters Set(string name, object value);

        #endregion
    }
}
