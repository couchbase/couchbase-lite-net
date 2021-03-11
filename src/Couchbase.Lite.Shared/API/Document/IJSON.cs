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
        [NotNull]
        string ToJSON();

        #endregion
    }


}
