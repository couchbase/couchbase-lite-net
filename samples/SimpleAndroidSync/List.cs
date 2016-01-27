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

using Couchbase.Lite;
using CouchbaseSample.Android.Document;

namespace CouchbaseSample.Android.Document
{
    public class List
    {
        private const string DocType = "list";
        private const string ViewName = "lists";

        private const string DeletedKey = "_deleted";

        public static Query GetQuery(Database database)
        {
            var view = database.GetView(ViewName);
            if (view.Map == null)
            {
                view.SetMap((document, emitter) => 
                   {
                    object deleted;
                    document.TryGetValue(DeletedKey, out deleted);

                    if(deleted == null)
                        emitter (document["text"], document["checked"]);
                    }, "2");
            }
            var query = view.CreateQuery();
            return query;
        }
    }
}
