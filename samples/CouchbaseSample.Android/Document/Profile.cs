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

using System.Collections.Generic;
using CouchbaseSample.Android.Document;
using Couchbase.Lite;
using Couchbase.Lite.Portable;

using Sharpen;

namespace CouchbaseSample.Android.Document
{
    public class Profile
    {
        private const string ViewName = "profiles";

        private const string ByIdViewName = "profiles_by_id";

        private const string DocType = "profile";

        public static IQuery GetQuery(IDatabase database, string ignoreUserId)
        {
            IView view = database.GetView(ViewName);
            if (view.GetMap() == null)
            {
                Mapper map = new _Mapper_30(ignoreUserId);
                view.SetMap(map, null);
            }
            IQuery query = view.CreateQuery();
            return query;
        }

        private sealed class _Mapper_30 : Mapper
        {
            public _Mapper_30(string ignoreUserId)
            {
                this.ignoreUserId = ignoreUserId;
            }

            public void Map(IDictionary<string, object> document, Emitter emitter)
            {
                if (Profile.DocType.Equals(document.Get("type")))
                {
                    if (ignoreUserId == null || (ignoreUserId != null && !ignoreUserId.Equals(document
                        .Get("user_id"))))
                    {
                        emitter.Emit(document.Get("name"), document);
                    }
                }
            }

            private readonly string ignoreUserId;
        }

        public static Query GetQueryById(Database database, string userId)
        {
            View view = database.GetView(ByIdViewName);
            if (view.GetMap() == null)
            {
                Mapper map = new _Mapper_52();
                view.SetMap(map, null);
            }
            Query query = view.CreateQuery();
            IList<object> keys = new AList<object>();
            keys.AddItem(userId);
            query.SetKeys(keys);
            return query;
        }

        private sealed class _Mapper_52 : Mapper
        {
            public _Mapper_52()
            {
            }

            public void Map(IDictionary<string, object> document, Emitter emitter)
            {
                if (Profile.DocType.Equals(document.Get("type")))
                {
                    emitter.Emit(document.Get("user_id"), document);
                }
            }
        }

        public static Couchbase.Lite.Document GetUserProfileById(Database database, string
             userId)
        {
            Couchbase.Lite.Document profile = null;
            try
            {
                QueryEnumerator enumerator = Profile.GetQueryById(database, userId).Run();
                profile = enumerator != null && enumerator.GetCount() > 0 ? enumerator.GetRow(0).
                    GetDocument() : null;
            }
            catch (CouchbaseLiteException)
            {
            }
            return profile;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
        public static Couchbase.Lite.Document CreateProfile(Database database, string userId
            , string name)
        {
            SimpleDateFormat dateFormatter = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'"
                );
            Calendar calendar = GregorianCalendar.GetInstance();
            string currentTimeString = dateFormatter.Format(calendar.GetTime());
            IDictionary<string, object> properties = new Dictionary<string, object>();
            properties.Put("type", DocType);
            properties.Put("user_id", userId);
            properties.Put("name", name);
            Couchbase.Lite.Document document = database.GetDocument("profile:" + userId);
            document.PutProperties(properties);
            return document;
        }
    }
}
