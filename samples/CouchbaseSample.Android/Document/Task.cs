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
using System.IO;
using Android.Graphics;
using CouchbaseSample.Android.Document;
using Couchbase.Lite;
using Sharpen;

namespace CouchbaseSample.Android.Document
{
	public class Task
	{
		private const string ViewName = "tasks";

		private const string DocType = "task";

		public static Query GetQuery(Database database, string listDocId)
		{
			View view = database.GetView(ViewName);
			if (view.Map == null)
			{
                view.Map += (IDictionary<string, object> document, EmitDelegate emitter)=> 
                {
                    if (Task.DocType.Equals(document.Get("type")))
                    {
                        var keys = new AList<object>();
                        keys.AddItem(document.Get("list_id"));
                        keys.AddItem(document.Get("created_at"));
                        emitter(keys, document);
                    }
                };
            }
			Query query = view.CreateQuery();
            query.Descending = true;
			IList<object> startKeys = new AList<object>();
			startKeys.AddItem(listDocId);
			startKeys.AddItem(new Dictionary<string, object>());
			IList<object> endKeys = new AList<object>();
			endKeys.AddItem(listDocId);
            query.StartKey = startKeys;
            query.EndKey = endKeys;
			return query;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static Couchbase.Lite.Document CreateTask(Database database, string title, 
			Bitmap image, string listId)
		{
			SimpleDateFormat dateFormatter = new SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'"
				);
			Calendar calendar = GregorianCalendar.GetInstance();
			string currentTimeString = dateFormatter.Format(calendar.GetTime());
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.Put("type", DocType);
			properties.Put("title", title);
			properties.Put("checked", false);
			properties.Put("created_at", currentTimeString);
			properties.Put("list_id", listId);
			Couchbase.Lite.Document document = database.CreateDocument();
			UnsavedRevision revision = document.CreateRevision();
			revision.SetUserProperties(properties);
			if (image != null)
			{
				ByteArrayOutputStream @out = new ByteArrayOutputStream();
				image.Compress(Bitmap.CompressFormat.Jpeg, 50, @out);
				ByteArrayInputStream @in = new ByteArrayInputStream(@out.ToByteArray());
				revision.SetAttachment("image", "image/jpg", @in);
			}
			revision.Save();
			return document;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static void AttachImage(Couchbase.Lite.Document task, Bitmap image)
		{
			if (task == null || image == null)
			{
				return;
			}
			UnsavedRevision revision = task.CreateRevision();
			ByteArrayOutputStream @out = new ByteArrayOutputStream();
			image.Compress(Bitmap.CompressFormat.Jpeg, 50, @out);
			ByteArrayInputStream @in = new ByteArrayInputStream(@out.ToByteArray());
			revision.SetAttachment("image", "image/jpg", @in);
			revision.Save();
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static void UpdateCheckedStatus(Couchbase.Lite.Document task, bool @checked
			)
		{
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties.PutAll(task.GetProperties());
			properties.Put("checked", @checked);
			task.PutProperties(properties);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public static void DeleteTask(Couchbase.Lite.Document task)
		{
			task.Delete();
		}
	}
}
