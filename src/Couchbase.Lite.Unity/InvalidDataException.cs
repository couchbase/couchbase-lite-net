//
// System.InvalidDataException.cs
//
// Authors:
//   Christopher James Lahey <clahey@ximian.com>
//   Andreas Nahr (ClassDevelopment@A-SoftTech.com)
//
// Copyright (C) 2004, 2006 Novell, Inc (http://www.novell.com)
//

using System.Globalization;
using System.Runtime.Serialization;

namespace System.IO.Couchbase
{
    [Serializable]
    public sealed class InvalidDataException : SystemException
    {
        const int Result = unchecked ((int)0x80131503);

        // Constructors
        public InvalidDataException ()
            : base ("Invalid data format.")
        {
            HResult = Result;
        }

        public InvalidDataException (string message)
            : base (message)
        {
            HResult = Result;
        }

        public InvalidDataException (string message, Exception innerException)
            : base (message, innerException)
        {
            HResult = Result;
        }

        private InvalidDataException (SerializationInfo info, StreamingContext context)
            : base (info, context)
        {
        }
    }
}
