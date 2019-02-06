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

using JetBrains.Annotations;

namespace Couchbase.Lite.Util
{
    internal static class CBDebug
    {
        #if DEBUG
        [ContractAnnotation("assertion:false => halt")]
        #endif
        public static void AssertAndLog([NotNull]DomainLogger logger, bool assertion, [NotNull]string tag, [NotNull]string message)
        {
            Debug.Assert(assertion, message);
            logger.W(tag, message);
        }

        public static void LogAndThrow([NotNull]DomainLogger logger, [NotNull]Exception e, [NotNull]string tag, [NotNull]string message, bool fatal)
        {
            if (fatal) {
                logger.E(tag, message, e);
            } else {
                logger.W(tag, message, e);
            }

            throw e;
        }

        [NotNull]
        public static T MustNotBeNull<T>([NotNull]DomainLogger logger, [NotNull]string tag, [NotNull]string argumentName, T argumentValue) where T : class
        {
            Debug.Assert(argumentValue != null);
            if (argumentValue == null) {
                throwArgumentNullException(logger, tag, argumentName);
            }

            return argumentValue;
        }

        [NotNull]
        public static IEnumerable<T> ItemsMustNotBeNull<T>([NotNull]DomainLogger logger, [NotNull]string tag, [NotNull]string argumentName, IEnumerable<T> argumentValues) where T : class
        {
            Debug.Assert(argumentValues != null);
            if (argumentValues == null) {
                throwArgumentNullException(logger, tag, argumentName);
            } else {
                int index = 0;
                foreach(var item in argumentValues) {
                    if (item == null) {
                        throwArgumentNullException(logger, tag, $"{argumentName}[{index}]");
                    }
                    index++;
                }
            }

            return argumentValues;
        }

        [NotNull]
        public static unsafe void* MustNotBeNullPointer([NotNull]DomainLogger logger, [NotNull]string tag, [NotNull]string argumentName, void* argumentValue)
        {
            Debug.Assert(argumentValue != null);
            if (argumentValue == null) {
                throwArgumentNullException(logger, tag, argumentName);
            }

            return argumentValue;
        }
    
        private static void throwArgumentNullException(DomainLogger logger, string tag, string message)
        {
            var ex = new ArgumentNullException(message);
            logger.E(tag, ex.ToString() ?? "");
            throw ex;
        }
    }
}