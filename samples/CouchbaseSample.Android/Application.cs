/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013, 2014 Xamarin, Inc. All rights reserved.
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
using System.IO;
using Android.App;
using Android.Content;
using CouchbaseSample.Android;
using Couchbase.Lite;
using Couchbase.Lite.Android;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Util;
using Sharpen;

namespace CouchbaseSample.Android
{
    public class Application : global::Android.App.Application
    {
        public const string Tag = "ToDoLite";

        private const string DatabaseName = "todos";

        private const string SyncUrl = "http://sync.couchbasecloud.com:4984/todos4/";

        private const string PrefCurrentListId = "CurrentListId";

        private const string PrefCurrentUserId = "CurrentUserId";

        private Manager manager;

        private Database database;

        private int syncCompletedChangedCount;

        private int syncTotalChangedCount;

        private Application.OnSyncProgressChangeObservable onSyncProgressChangeObservable;

        private Application.OnSyncUnauthorizedObservable onSyncUnauthorizedObservable;

        public enum AuthenticationType
        {
            Facebook,
            CustomCookie
        }

        private Application.AuthenticationType authenticationType = Application.AuthenticationType
            .Facebook;

        // By default, this should be set to FACEBOOK.  To test "custom cookie" auth,
        // set this to CUSTOM_COOKIE.
        private void InitDatabase()
        {
            try
            {
//              Manager.EnableLogging(Log.Tag, Log.Verbose);
//              Manager.EnableLogging(Log.TagSync, Log.Debug);
//              Manager.EnableLogging(Log.TagQuery, Log.Debug);
//              Manager.EnableLogging(Log.TagView, Log.Debug);
//              Manager.EnableLogging(Log.TagDatabase, Log.Debug);
//              manager = new Manager(new AndroidContext(GetApplicationContext()), Manager.DefaultOptions
//                  );
            }
            catch (IOException e)
            {
                Log.E(Tag, "Cannot create Manager object", e);
                return;
            }
            try
            {
                database = manager.GetDatabase(DatabaseName);
            }
            catch (CouchbaseLiteException e)
            {
                Log.E(Tag, "Cannot get Database", e);
                return;
            }
        }

        private void InitObservable()
        {
            onSyncProgressChangeObservable = new Application.OnSyncProgressChangeObservable();
            onSyncUnauthorizedObservable = new Application.OnSyncUnauthorizedObservable();
        }

        private void UpdateSyncProgress(int completedCount, int totalCount)
        {
            lock (this)
            {
                onSyncProgressChangeObservable.NotifyChanges(completedCount, totalCount);
            }
        }

        public virtual void StartReplicationSyncWithCustomCookie(string name, string value
            , string path, DateTime expirationDate, bool secure, bool httpOnly)
        {
            Replication[] replications = CreateReplications();
            Replication pullRep = replications[0];
            Replication pushRep = replications[1];
            pullRep.SetCookie(name, value, path, expirationDate, secure, httpOnly);
            pushRep.SetCookie(name, value, path, expirationDate, secure, httpOnly);
            pullRep.Start();
            pushRep.Start();
            Log.V(Tag, "Start Replication Sync ...");
        }

        public virtual void StartReplicationSyncWithStoredCustomCookie()
        {
            Replication[] replications = CreateReplications();
            Replication pullRep = replications[0];
            Replication pushRep = replications[1];
            pullRep.Start();
            pushRep.Start();
            Log.V(Tag, "Start Replication Sync ...");
        }

        public virtual void StartReplicationSyncWithFacebookLogin(string accessToken, string
             email)
        {
            Authenticator facebookAuthenticator = AuthenticatorFactory.CreateFacebookAuthenticator
                (accessToken);
            Replication[] replications = CreateReplications();
            Replication pullRep = replications[0];
            Replication pushRep = replications[1];
            pullRep.SetAuthenticator(facebookAuthenticator);
            pushRep.SetAuthenticator(facebookAuthenticator);
            pullRep.Start();
            pushRep.Start();
            Log.V(Tag, "Start Replication Sync ...");
        }

        public virtual Replication[] CreateReplications()
        {
            Uri syncUrl;
            try
            {
                syncUrl = new Uri(SyncUrl);
            }
            catch (UriFormatException e)
            {
                Log.E(Tag, "Invalid Sync Url", e);
                throw new RuntimeException(e);
            }
            Replication pullRep = database.CreatePullReplication(syncUrl);
            pullRep.SetContinuous(true);
            pullRep.AddChangeListener(GetReplicationChangeListener());
            Replication pushRep = database.CreatePushReplication(syncUrl);
            pushRep.SetContinuous(true);
            pushRep.AddChangeListener(GetReplicationChangeListener());
            return new Replication[] { pullRep, pushRep };
        }

        private Replication.ChangeListener GetReplicationChangeListener()
        {
            return new _ChangeListener_151(this);
        }

        private sealed class _ChangeListener_151 : Replication.ChangeListener
        {
            public _ChangeListener_151(Application _enclosing)
            {
                this._enclosing = _enclosing;
            }

            public void Changed(Replication.ChangeEvent @event)
            {
                Replication replication = @event.GetSource();
                if (replication.GetLastError() != null)
                {
                    Exception lastError = replication.GetLastError();
                    if (lastError is HttpResponseException)
                    {
                        HttpResponseException responseException = (HttpResponseException)lastError;
                        if (responseException.GetStatusCode() == 401)
                        {
                            this._enclosing.onSyncUnauthorizedObservable.NotifyChanges();
                        }
                    }
                }
                this._enclosing.UpdateSyncProgress(replication.GetCompletedChangesCount(), replication
                    .GetChangesCount());
            }

            private readonly Application _enclosing;
        }

        public override void OnCreate()
        {
            base.OnCreate();
            InitDatabase();
            InitObservable();
        }

        public virtual Database GetDatabase()
        {
            return this.database;
        }

        public virtual string GetCurrentListId()
        {
            SharedPreferences sp = PreferenceManager.GetDefaultSharedPreferences(GetApplicationContext
                ());
            return sp.GetString(PrefCurrentListId, null);
        }

        public virtual void SetCurrentListId(string id)
        {
            SharedPreferences sp = PreferenceManager.GetDefaultSharedPreferences(GetApplicationContext
                ());
            if (id != null)
            {
                sp.Edit().PutString(PrefCurrentListId, id).Apply();
            }
            else
            {
                sp.Edit().Remove(PrefCurrentListId);
            }
        }

        public virtual string GetCurrentUserId()
        {
            SharedPreferences sp = PreferenceManager.GetDefaultSharedPreferences(GetApplicationContext
                ());
            string userId = sp.GetString(PrefCurrentUserId, null);
            return userId;
        }

        public virtual void SetCurrentUserId(string id)
        {
            SharedPreferences sp = PreferenceManager.GetDefaultSharedPreferences(GetApplicationContext
                ());
            if (id != null)
            {
                sp.Edit().PutString(PrefCurrentUserId, id).Apply();
            }
            else
            {
                sp.Edit().Remove(PrefCurrentUserId).Apply();
            }
        }

        public virtual Application.OnSyncProgressChangeObservable GetOnSyncProgressChangeObservable
            ()
        {
            return onSyncProgressChangeObservable;
        }

        public virtual Application.OnSyncUnauthorizedObservable GetOnSyncUnauthorizedObservable
            ()
        {
            return onSyncUnauthorizedObservable;
        }

        public virtual Application.AuthenticationType GetAuthenticationType()
        {
            return authenticationType;
        }

        internal class OnSyncProgressChangeObservable : Observable
        {
            private void NotifyChanges(int completedCount, int totalCount)
            {
                Application.SyncProgress progress = new Application.SyncProgress();
                progress.completedCount = completedCount;
                progress.totalCount = totalCount;
                SetChanged();
                NotifyObservers(progress);
            }
        }

        internal class OnSyncUnauthorizedObservable : Observable
        {
            private void NotifyChanges()
            {
                SetChanged();
                NotifyObservers();
            }
        }

        internal class SyncProgress
        {
            public int completedCount;

            public int totalCount;
        }
    }
}
