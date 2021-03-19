using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Lite
{
    public interface IJSON
    {
        #region Properties 

        /// <summary>
        /// Converts this object to JSON format string.
        /// </summary>
        /// <returns>The contents of this object in JSON format string</returns>
        /// <exception cref="NotSupportedException">Thrown if ToJSON is called from <see cref="MutableDocument"/>,  
        /// <see cref="MutableDictionaryObject"/>, or <see cref="MutableArrayObject"/></exception>
        [NotNull]
        string ToJSON();

        #endregion
    }


}
