// 
//  CBDebug.cs
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
using System.Diagnostics;

using Couchbase.Lite.Internal.Logging;

using System.Diagnostics.CodeAnalysis;

namespace Couchbase.Lite.Util
{
    [ExcludeFromCodeCoverage]
    internal static class CBDebug
    {
            public static void AssertAndLog(DomainLogger logger,
                [DoesNotReturnIf(false)]
                bool assertion, string tag, string message)
        {
            Debug.Assert(assertion, message);
            logger.W(tag, message);
        }

        [DoesNotReturn]
        public static void LogAndThrow(DomainLogger logger, Exception e, string tag, string? message, bool fatal)
        {
            if (fatal) {
                logger.E(tag, message ?? e.Message, e);
            } else {
                logger.W(tag, message ?? e.Message, e);
            }

            throw e;
        }

        public static T MustNotBeNull<T>(DomainLogger logger, string tag, string argumentName, T? argumentValue) where T : class
        {
            Debug.Assert(argumentValue != null);
            if (argumentValue == null) {
                ThrowArgumentNullException(logger, tag, argumentName);
            }

            return argumentValue;
        }

        public static IEnumerable<T> ItemsMustNotBeNull<T>(DomainLogger logger, string tag, string argumentName, IEnumerable<T?> argumentValues) where T : class
        {
            Debug.Assert(argumentValues != null);
            if (argumentValues == null) {
                ThrowArgumentNullException(logger, tag, argumentName);
            } else {
                int index = 0;
                foreach(var item in argumentValues) {
                    if (item == null) {
                        ThrowArgumentNullException(logger, tag, $"{argumentName}[{index}]");
                    }
                    index++;
                }
            }

            return argumentValues!;
        }

        public static unsafe void* MustNotBeNullPointer(DomainLogger logger, string tag, string argumentName, void* argumentValue)
        {
            Debug.Assert(argumentValue != null);
            if (argumentValue == null) {
                ThrowArgumentNullException(logger, tag, argumentName);
            }

            return argumentValue;
        }

        [DoesNotReturn]
        private static void ThrowArgumentNullException(DomainLogger logger, string tag, string message)
        {
            var ex = new ArgumentNullException(message);
            logger.E(tag, ex.ToString() ?? "");
            throw ex;
        }
    }
}