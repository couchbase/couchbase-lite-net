/*
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
using CouchbaseSample.Android;
using CouchbaseSample.Android.Document;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;
using System.Globalization;
using System;
using System.Reflection;

namespace CouchbaseSample.Android.Document
{
    public class List
    {
        private const string DocType = "list";
        private const string ViewName = "lists";

        public static Query GetQuery(Database database)
        {
            var view = database.GetView(ViewName);
            if (view.Map == null)
            {
                view.SetMap((document, emitter) => 
                    {
                        object type;
                        document.TryGetValue("type", out type);

                        if (List.DocType.Equals ((string)type)) {
                            emitter (document["text"], document);
                        }
                    }, "1");
            }
            var query = view.CreateQuery();
            return query;
        }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
//      public static Couchbase.Lite.Document CreateNewList(Database database, string title, string userId)
//      {
//            var currentTimeString = DateTime.UtcNow.ToString("O");
//            var properties = new Dictionary<string, object>();
//            properties["type"] = "list";
//            properties["title"] = title;
//            properties["created_at"] = currentTimeString;
//            properties["owner"] = "profile:" + userId;
//            properties["members"] = new List<string>();
//          Couchbase.Lite.Document document = database.CreateDocument();
//          document.PutProperties(properties);
//          return document;
//      }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
//      public static void AssignOwnerToListsIfNeeded(Database database, Couchbase.Lite.Document
//           user)
//      {
//          QueryEnumerator enumerator = GetQuery(database).Run();
//          if (enumerator == null)
//          {
//              return;
//          }
//            foreach (var row in enumerator)
//            {
//                Couchbase.Lite.Document document = row.Document;
//                string owner = (string)document.GetProperty("owner");
//                if (owner != null)
//                {
//                    continue;
//                }
//                var properties = new Dictionary<string, object>(document.Properties);
//                properties["owner"] = user.Id;
//                document.PutProperties(properties);
//            }
//      }

        /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
//        public static void AddMemberToList(Couchbase.Lite.Document list, Couchbase.Lite.Document user)
//      {
//            var newProperties = new Dictionary<string, object>(list.Properties);
//            var members = (IList<string>)newProperties["members"];
//          if (members == null)
//          {
//              members = new List<string>();
//          }
//          members.Add(user.Id);
//            newProperties["members"] = members;
//          try
//          {
//              list.PutProperties(newProperties);
//          }
//          catch (CouchbaseLiteException e)
//          {
//                Log.E(Tag, "Cannot add member to the list", e);
//          }
//      }
//
//      /// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
//        public static void RemoveMemberFromList(Couchbase.Lite.Document list, Couchbase.Lite.Document user)
//      {
//            var newProperties = new Dictionary<string, object>(list.Properties);
//          var members = (IList<string>)newProperties["members"];
//          if (members != null)
//          {
//              members.Remove(user.Id);
//          }
//            newProperties["members"] = members;
//          list.PutProperties(newProperties);
//      }
    }
}
