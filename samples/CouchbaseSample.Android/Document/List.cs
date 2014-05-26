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
using CouchbaseSample.Android;
using CouchbaseSample.Android.Document;
using Couchbase.Lite;
using Couchbase.Lite.Util;
using Sharpen;
using System.Globalization;

namespace CouchbaseSample.Android.Document
{
	public class List
	{
		private const string ViewName = "lists";

		private const string DocType = "list";

		public static Query GetQuery(Database database)
		{
			View view = database.GetView(ViewName);
			if (view.Map == null)
			{
                view.Map += (IDictionary<string, object> document, EmitDelegate emitter) =>
                {
                    string type = (string)document.Get("type");
                    if (List.DocType.Equals(type))
                    {
                        emitter(document.Get("title"), document);
                    }
                };
			}
			Query query = view.CreateQuery();
			return query;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static Couchbase.Lite.Document CreateNewList(Database database, string title, string userId)
		{
			var dateFormatter = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'");
            var calendar = Calendar.CurrentEra;
			string currentTimeString = dateFormatter.Format(calendar.GetTime());
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("type", "list");
			properties.Put("title", title);
			properties.Put("created_at", currentTimeString);
			properties.Put("owner", "profile:" + userId);
			properties.Put("members", new AList<string>());
			Couchbase.Lite.Document document = database.CreateDocument();
			document.PutProperties(properties);
			return document;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static void AssignOwnerToListsIfNeeded(Database database, Couchbase.Lite.Document
			 user)
		{
			QueryEnumerator enumerator = GetQuery(database).Run();
			if (enumerator == null)
			{
				return;
			}
            foreach (var row in enumerator)
            {
                Couchbase.Lite.Document document = row.Document;
                string owner = (string)document.GetProperty("owner");
                if (owner != null)
                {
                    continue;
                }
                IDictionary<string, object> properties = new Dictionary<string, object>();
                properties.PutAll(document.Properties);
                properties.Put("owner", user.Id);
                document.PutProperties(properties);
            }
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static void AddMemberToList(Couchbase.Lite.Document list, Couchbase.Lite.Document
			 user)
		{
			IDictionary<string, object> newProperties = new Dictionary<string, object>();
			newProperties.PutAll(list.Properties);
			IList<string> members = (IList<string>)newProperties.Get("members");
			if (members == null)
			{
				members = new AList<string>();
			}
			members.AddItem(user.Id);
			newProperties.Put("members", members);
			try
			{
				list.PutProperties(newProperties);
			}
			catch (CouchbaseLiteException e)
			{
				Log.E(Application.Tag, "Cannot add member to the list", e);
			}
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static void RemoveMemberFromList(Couchbase.Lite.Document list, Couchbase.Lite.Document
			 user)
		{
			IDictionary<string, object> newProperties = new Dictionary<string, object>();
			newProperties.PutAll(list.Properties);
			IList<string> members = (IList<string>)newProperties.Get("members");
			if (members != null)
			{
				members.Remove(user.Id);
			}
			newProperties.Put("members", members);
			list.PutProperties(newProperties);
		}
	}
}
