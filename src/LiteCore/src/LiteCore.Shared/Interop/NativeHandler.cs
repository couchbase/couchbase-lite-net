//
// NativeHandler.cs
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Couchbase.Lite;

namespace LiteCore.Interop
{

    #region Delegates

    /// <summary>
    /// A delegate for calling native functions that return
    /// a bool and have an out error parameter
    /// </summary>
    internal unsafe delegate bool C4TryLogicDelegate1(C4Error* err);

    /// <summary>
    /// A delegate for calling native functions that return
    /// a pointer and have an out error parameter
    /// </summary>
    internal unsafe delegate void* C4TryLogicDelegate2(C4Error* err);

    /// <summary>
    /// A delegate for calling native functions that return
    /// an int and have an out error parameter
    /// </summary>
    internal unsafe delegate int C4TryLogicDelegate3(C4Error* err);

    internal unsafe delegate byte[] C4TryLogicDelegate4(C4Error* err);

    #endregion

    /// <summary>
    /// A rudimentary retry handler with options for allowing specific errors
    /// and custom exception handling
    /// </summary>
    internal sealed class NativeHandler
    {


        #region Variables

        private Action<CouchbaseException> _exceptionHandler;

        private readonly List<C4Error> _allowedErrors = new List<C4Error>();

        #endregion

        #region Properties

        /// <summary>
        /// Gets the exception thrown during the operation, if any
        /// </summary>
        public CouchbaseException Exception { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a new object for chaining in a fluent fashion
        /// </summary>
        /// <returns>A constructed object</returns>
        public static NativeHandler Create()
        {
            return new NativeHandler();
        }

        /// <summary>
        /// Allows the operation to succeed even if an error with the
        /// given code and domain occurs
        /// </summary>
        /// <returns>The current object for further fluent operations</returns>
        /// <param name="code">The code of the error to allow.</param>
        /// <param name="domain">The domain of the error to allow.</param>
        public NativeHandler AllowError(int code, C4ErrorDomain domain)
        {
            return AllowError(new C4Error(domain, code));
        }

        /// <summary>
        /// Allows the operation to succeed even if the given error
        /// occurs
        /// </summary>
        /// <returns>The current object for further fluent operations</returns>
        /// <param name="error">The error to allow.</param>
        public NativeHandler AllowError(C4Error error)
        {
            _allowedErrors.Add(error);
            return this;
        }

        /// <summary>
        /// Allows the operation to succeed even if any of the
        /// given errors occur
        /// </summary>
        /// <returns>The current object for further fluent operations</returns>
        /// <param name="errors">The errors to allow.</param>
        public NativeHandler AllowErrors(params C4Error[] errors)
        {
            return AllowErrors((IEnumerable<C4Error>)errors);
        }

        /// <summary>
        /// Allows the operation to succeed even if any of the
        /// given errors occur
        /// </summary>
        /// <returns>The current object for further fluent operations</returns>
        /// <param name="errors">The errors to allow.</param>
        public NativeHandler AllowErrors(IEnumerable<C4Error> errors)
        {
            foreach(var error in errors ?? Enumerable.Empty<C4Error>()) {
                AllowError(error);
            }

            return this;
        }

        /// <summary>
        /// Sets the handler for any exception generated during the operation
        /// that is not allowed by any of the AllowError API calls.  This will
        /// stop the retry handler from throwing.
        /// </summary>
        /// <returns>The current object for further fluent operations</returns>
        /// <param name="exceptionHandler">The logic for handling exceptions</param>
        public NativeHandler HandleExceptions(Action<CouchbaseException> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler;
            return this;
        }

        public unsafe bool Execute(C4TryLogicDelegate1 block)
        {
            Debug.Assert(block != null);

            C4Error err;
            if(block(&err) || err.code == 0) {
                Exception = null;
                return true;
            }

            Exception = CouchbaseException.Create(err);
            ThrowOrHandle();
            return false;
        }

        public unsafe void* Execute(C4TryLogicDelegate2 block)
        { 
            Debug.Assert(block != null);

            C4Error err;
            var retVal = block(&err);
            if(retVal != null || err.code == 0) {
                Exception = null;
                return retVal;
            }

            Exception = CouchbaseException.Create(err);
            ThrowOrHandle();
            return null;
        }

        public unsafe int Execute(C4TryLogicDelegate3 block)
        {
            Debug.Assert(block != null);

            C4Error err;
            var retVal = block(&err);
            if(retVal >= 0 || err.code == 0) {
                Exception = null;
                return retVal;
            }

            Exception = CouchbaseException.Create(err);
            ThrowOrHandle();
            return retVal;
        }

        public unsafe byte[] Execute(C4TryLogicDelegate4 block)
        {
            Debug.Assert(block != null);

            C4Error err;
            var retVal = block(&err);
            if(retVal != null || err.code == 0) {
                Exception = null;
                return retVal;
            }

            Exception = CouchbaseException.Create(err);
            ThrowOrHandle();
            return retVal;
        }

        #endregion

        #region Private Methods

        private void ThrowOrHandle()
        {
            if (Exception == null) {
                return;
            }

            foreach(var error in _allowedErrors) {
                if(error.Equals(Exception.LiteCoreError) || (error.domain == 0 &&
                    error.code.Equals(Exception.LiteCoreError.code))) {
                    return;
                }
            }

            if (_exceptionHandler == null) {
                throw Exception;
            }

            _exceptionHandler(Exception);
        }

        #endregion
    }
}
