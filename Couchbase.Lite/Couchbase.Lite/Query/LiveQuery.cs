//
// LiveQuery.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
* except in compliance with the License. You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software distributed under the
* License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
* either express or implied. See the License for the specific language governing permissions
* and limitations under the License.
*/
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using Couchbase.Lite.Util;
using Sharpen;

namespace Couchbase.Lite
{
    public partial class LiveQuery : Query
    {
    #region Non-public Members

        const Int32 DefaultQueryTimeout = 90000; // milliseconds.

        QueryEnumerator rows;

        volatile Boolean observing;
        volatile Boolean willUpdate;

        Boolean WillUpdate {
            get { return willUpdate; }
            set { willUpdate = value; }
        }

        /// <summary>
        /// If a query is running and the user calls Stop() on this query, the Task
        /// will be used in order to cancel the query in progress.
        /// </summary>
        Task UpdateQueryTask { get; set; }
        CancellationTokenSource UpdateQueryTokenSource { get; set; }

        private void OnDatabaseChanged (object sender, Database.DatabaseChangeEventArgs e)
        {
            if (!willUpdate)
            {
                WillUpdate = true;
                Update();
            }
        }

        /// <summary>Sends the query to the server and returns an enumerator over the result rows (Synchronous).
        ///     </summary>
        /// <remarks>
        /// Sends the query to the server and returns an enumerator over the result rows (Synchronous).
        /// Note: In a CBLLiveQuery you should add a ChangeListener and call start() instead.
        /// </remarks>
        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public override QueryEnumerator Run()
        {
            try
            {
                WaitForRows();
            }
            catch (Exception e)
            {
                LastError = e;
                throw new CouchbaseLiteException(e, StatusCode.InternalServerError);
            }
            return rows == null ? null : new QueryEnumerator (rows);
        }



        /// <summary>
        /// Implements the updating of the <see cref="Rows"/> collection.
        /// </summary>
        private void Update()
        {
            if (View == null)
            {
                throw new CouchbaseLiteException("Cannot start LiveQuery when view is null");
            }

            WillUpdate = false;

            UpdateQueryTokenSource = new CancellationTokenSource();

            UpdateQueryTask = RunAsync(base.Run, UpdateQueryTokenSource.Token)
                .ContinueWith(runTask =>
                    {
                        if (runTask.Status != TaskStatus.RanToCompletion) {
                            Log.W(String.Format("Query Updated task did not run to completion ({0})", runTask.Status), runTask.Exception);
                            return; // NOTE: Assuming that we don't want to lose rows we already retrieved.
                        }

                        rows = runTask.Result; // NOTE: Should this be 'append' instead of 'replace' semantics? If append, use a concurrent collection.

                        var evt = Changed;
                        if (evt == null)
                            return; // No delegates were subscribed, so no work to be done.

                        var args = new QueryChangeEventArgs (this, rows, runTask.Exception);
                        evt (this, args);
                    });
        }

    #endregion

    #region Constructors

        internal LiveQuery(Query query) : base(query.Database, query.View) { }
    
    #endregion

    #region Instance Members
        //Properties
        public QueryEnumerator Rows
        { 
            get
            {
                Start();
                // Have to return a copy because the enumeration has to start at item #0 every time
                return rows == null 
                    ? null 
                    : new QueryEnumerator(rows);
            }
        }

        public Exception LastError { get; private set; }

        //Methods

        /// <summary>Starts observing database changes.</summary>
        /// <remarks>
        /// Starts the <see cref="Couchbase.Lite.LiveQuery"/> and begins observing <see cref="Couchbase.Lite.Database"/> 
        /// changes. When the <see cref="Couchbase.Lite.Database"/> changes in a way that would affect the results of 
        /// the <see cref="Couchbase.Lite.Query"/>, the <see cref="Rows"/> property will be updated and any 
        /// <see cref="Changed"/> delegates will be notified.  Accessing the <see cref="Rows"/>  property or adding a
        /// <see cref="Changed"/> delegate will automatically start the <see cref="Couchbase.Lite.LiveQuery"/>.
        /// </remarks>
        public void Start()
        {
            if (!observing)
            {
                observing = true;
                Database.Changed += OnDatabaseChanged;
            }

            Update();
        }

        /// <summary>Stops observing database changes.</summary>
        /// <remarks>Stops observing database changes. Calling start() or rows() will restart it.</remarks>
        public void Stop()
        {
            if (observing)
            {
                Database.Changed -= OnDatabaseChanged;
                observing = false;
            }

            if (WillUpdate)
            {
                WillUpdate = false;
                if (UpdateQueryTokenSource.Token.CanBeCanceled)
                    UpdateQueryTokenSource.Cancel();
            }
        }


        /// <summary>Blocks until the intial <see cref="Couchbase.Lite.Query"/> finishes.</summary>
        /// <remarks>If an error occurs while executing the <see cref="Couchbase.Lite.Query"/>, <see cref="LastError"/> 
        /// will contain the exception. Can be cancelled if results are not returned after <see cref="DefaultQueryTimeout"/> (90 seconds).</remarks>
        public void WaitForRows()
        {
            Start();
            try
            {
                UpdateQueryTask.Wait(DefaultQueryTimeout, UpdateQueryTokenSource.Token);
                LastError = UpdateQueryTask.Exception;
            }
            catch (ExecutionException ex)
            {
                Log.E(Database.Tag, "Got execution exception waiting for rows", ex);
                throw;
            }
            catch (Exception e)
            {
                Log.E(Database.Tag, "Got interrupted exception waiting for rows", e);
                LastError = e;
            }
        }

        public event EventHandler<QueryChangeEventArgs> Changed;

    #endregion
    
    #region Delegates

    #endregion
    
    }

    #region EventArgs Subclasses
    public class QueryChangeEventArgs : EventArgs 
    {
        internal QueryChangeEventArgs (LiveQuery liveQuery, QueryEnumerator enumerator, Exception error)
        {
            Source = liveQuery;
            Rows = enumerator;
            Error = error;
        }

            //Properties
            public LiveQuery Source { get; private set; }

            public QueryEnumerator Rows { get; private set; }

            public Exception Error { get; private set; }
    }

        #endregion
        
}
