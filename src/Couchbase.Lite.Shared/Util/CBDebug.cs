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
using System.Diagnostics;

using Couchbase.Lite.Logging;

using JetBrains.Annotations;

namespace Couchbase.Lite.Util
{
    internal static class CBDebug
    {
        #if DEBUG
        [ContractAnnotation("assertion:false => halt")]
        #endif
        public static void AssertAndLog(DomainLogger logger, bool assertion, string tag, string message)
        {
            Debug.Assert(assertion, message);
            logger.W(tag, message);
        }

        [ContractAnnotation("=> halt")]
        public static void LogAndThrow(DomainLogger logger, Exception e, string tag, string message, bool fatal)
        {
            if (fatal) {
                logger.E(tag, message, e);
            } else {
                logger.W(tag, message, e);
            }

            throw e;
        }

        [NotNull]
        public static T MustNotBeNull<T>(DomainLogger logger, string tag, string argumentName, T argumentValue) where T : class
        {
            Debug.Assert(argumentValue != null);
            if (argumentValue == null) {
                var ex = new ArgumentNullException(argumentName);
                logger.E(tag, ex.ToString());
                throw ex;
            }

            return argumentValue;
        }

        [NotNull]
        public static unsafe void* MustNotBeNullPointer(DomainLogger logger, string tag, string argumentName, void* argumentValue)
        {
            Debug.Assert(argumentValue != null);
            if (argumentValue == null) {
                var ex = new ArgumentNullException(argumentName);
                logger.E(tag, ex.ToString());
                throw ex;
            }

            return argumentValue;
        }
    }
}