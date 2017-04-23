//
//  DatabaseFactory.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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


using Couchbase.Lite.Internal.DB;

namespace Couchbase.Lite
{
    /// <summary>
    /// A factory for creating <see cref="IDatabase"/> instances 
    /// </summary>
    public static class DatabaseFactory
    {
        #region Public Methods

        /// <summary>
        /// Creates an <see cref="IDatabase"/> instance with the given name.  Internally
        /// it may be operating on the same underlying data as another instance.
        /// </summary>
        /// <param name="name">The name of the database</param>
        /// <returns>The instantiated <see cref="IDatabase"/> object</returns>
        /// <exception cref="System.ArgumentNullException">Thrown if <c>name</c> is <c>null</c></exception>
        /// <exception cref="System.ArgumentException"><c>name</c> contains invalid characters</exception> 
        /// <exception cref="LiteCore.LiteCoreException">An error occurred during LiteCore interop</exception>
        public static IDatabase Create(string name)
        {
            return new Database(name);
        }

        
        public static IDatabase Create(string name, DatabaseConfiguration config)
        {
            var retVal = new Database(name, config);
            return retVal;
        }

        /// <summary>
        /// Creates an <see cref="IDatabase"/> object using the source object as
        /// a model for creation.
        /// </summary>
        /// <param name="other">The <see cref="IDatabase"/> to clone name and options from</param>
        /// <returns>The instantiated <see cref="IDatabase"/> object</returns>
        /// <exception cref="LiteCore.LiteCoreException">An error occurred during LiteCore interop</exception>
        public static IDatabase Create(IDatabase other)
        {
            return Create(other.Name, other.Config);
        }

        /// <summary>
        /// Checks if underlying data exists for a given database in the given directory.
        /// A <c>null</c> directory will check the library default directory for the platform
        /// </summary>
        /// <param name="name">The name of the database to check for</param>
        /// <param name="directory">The directory to check in</param>
        /// <returns><c>true</c> if data exists, <c>false</c> otherwise</returns>
        /// <exception cref="System.ArgumentNullException"><c>name</c> is <c>null</c></exception>
        /// <exception cref="System.ArgumentException"><c>name</c> contains invalid characters</exception> 
        public static bool DatabaseExists(string name, string directory)
        {
            return Database.Exists(name, directory);
        }

        /// <summary>
        /// Deletes underlying data for a given database given the database name and directory
        /// (from <see cref="Directory"/>).  Useful for deleting a database
        /// that cannot be otherwise opened (e.g. encryption key forgotten) but 
        /// <see cref="IDatabase.Delete"/> is preferred. A <c>null</c> directory will check
        /// the library default directory for the platform.
        /// </summary>
        /// <param name="name">The name of the database to delete</param>
        /// <param name="directory">The directory where the database is located</param>
        /// <exception cref="System.ArgumentNullException"><c>name</c> is <c>null</c></exception>
        /// <exception cref="System.ArgumentException"><c>name</c> contains invalid characters</exception> 
        /// <exception cref="LiteCore.LiteCoreException">An error occurred during LiteCore Interop</exception>
        public static void DeleteDatabase(string name, string directory)
        {
            Database.Delete(name, directory);
        }

        #endregion
    }
}
