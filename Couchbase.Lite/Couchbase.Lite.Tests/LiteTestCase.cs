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
using System.Collections.Generic;
using System.IO;
using Couchbase.Lite;
using Couchbase.Lite.Internal;

using Couchbase.Lite.Support;
using Couchbase.Lite.Util;
using NUnit.Framework;
using Sharpen;
using Couchbase.Lite.Tests;
using System.Diagnostics;
using System.Net;

namespace Couchbase.Lite
{
    [TestFixture]
	public abstract class LiteTestCase
	{
		public const string Tag = "LiteTestCase";

		protected internal ObjectWriter mapper = new ObjectWriter();

		protected internal Manager manager = null;

		protected internal Database database = null;

		protected internal string DefaultTestDb = "cblitetest";

		/// <exception cref="System.Exception"></exception>
        [TestFixtureSetUp]
		protected void SetUp()
		{
            Trace.Listeners.Add(new ConsoleTraceListener());
			Log.V(Tag, "setUp");
            LoadCustomProperties();
			StartCBLite();
            StartDatabase();
		}

		protected internal virtual InputStream GetAsset(string name)
		{
            return this.GetType().GetResourceAsStream("Couchbase.Lite.Tests.Assets." + name);
		}

        protected internal virtual DirectoryInfo GetRootDirectory()
		{
            var rootDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var rootDirectory = new DirectoryInfo(Path.Combine(rootDirectoryPath, "couchbase/tests/files"));
			return rootDirectory;
		}

		protected internal virtual string GetServerPath()
		{
            var filesDir = GetRootDirectory().FullName;
			return filesDir;
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual void StartCBLite()
		{
			string serverPath = GetServerPath();
            var path = new DirectoryInfo(serverPath);

            if (path.Exists)
                path.Delete(true);

            path.Create();

            var testPath = path.CreateSubdirectory("tests");
            manager = new Manager(testPath, Manager.DefaultOptions);
		}

		protected internal virtual void StopCBLite()
		{
			if (manager != null)
			{
				manager.Close();
			}
		}

		protected internal virtual Database StartDatabase()
		{
			database = EnsureEmptyDatabase(DefaultTestDb);
			return database;
		}

		protected internal virtual void StopDatabse()
		{
			if (database != null)
			{
				database.Close();
			}
		}

		protected internal virtual Database EnsureEmptyDatabase(string dbName)
		{
			Database db = manager.GetExistingDatabase(dbName);
			if (db != null)
			{
                var status = false;;

                try {
                    db.Delete ();
                    status = true;
                } catch (Exception) { }

				NUnit.Framework.Assert.IsTrue(status);
            }
            db = manager.GetDatabase(dbName);
			return db;
		}

		/// <exception cref="System.IO.IOException"></exception>
		protected internal virtual void LoadCustomProperties()
		{
            var systemProperties = Runtime.Properties;
			InputStream mainProperties = GetAsset("test.properties");
			if (mainProperties != null)
			{
                systemProperties.Load(mainProperties);
			}
			try
			{
                InputStream localProperties = GetAsset("local-test.properties");
				if (localProperties != null)
				{
                    systemProperties.Load(localProperties);
				}
			}
			catch (IOException)
			{
				Log.W(Tag, "Error trying to read from local-test.properties, does this file exist?");
			}
		}

		protected internal virtual string GetReplicationProtocol()
		{
			return Runtime.GetProperty("replicationProtocol");
		}

		protected internal virtual string GetReplicationServer()
		{
			return Runtime.GetProperty("replicationServer");
		}

		protected internal virtual int GetReplicationPort()
		{
			return System.Convert.ToInt32(Runtime.GetProperty("replicationPort"));
		}

		protected internal virtual string GetReplicationAdminUser()
		{
			return Runtime.GetProperty("replicationAdminUser");
		}

		protected internal virtual string GetReplicationAdminPassword()
		{
			return Runtime.GetProperty("replicationAdminPassword");
		}

		protected internal virtual string GetReplicationDatabase()
		{
			return Runtime.GetProperty("replicationDatabase");
		}

		protected internal virtual Uri GetReplicationURL()
		{
            String path = null;
			try
			{
                if (GetReplicationAdminUser() != null && GetReplicationAdminUser().Trim().Length > 0)
				{
                    path = string.Format("{0}://{1}:{2}@{3}:{4}/{5}", GetReplicationProtocol(), GetReplicationAdminUser
                        (), GetReplicationAdminPassword(), GetReplicationServer(), GetReplicationPort(), 
                        GetReplicationDatabase());
                    return new Uri(path);
				}
				else
				{
                    path = string.Format("{0}://{1}:{2}/{3}", GetReplicationProtocol(), GetReplicationServer
                        (), GetReplicationPort(), GetReplicationDatabase());
                    return new Uri(path);
				}
			}
			catch (UriFormatException e)
			{
                throw new ArgumentException(String.Format("Invalid replication URL: {0}", path), e);
			}
		}

		/// <exception cref="System.UriFormatException"></exception>
		protected internal virtual Uri GetReplicationURLWithoutCredentials()
		{
            return new Uri(string.Format("{0}://{1}:{2}/{3}", GetReplicationProtocol(), GetReplicationServer(), GetReplicationPort(), GetReplicationDatabase()));
		}

		/// <exception cref="System.Exception"></exception>
        [TestFixtureTearDown]
        protected void TearDown()
		{
			Log.V(Tag, "tearDown");
			StopDatabse();
			StopCBLite();
		}

		protected internal virtual IDictionary<string, object> UserProperties(IDictionary
			<string, object> properties)
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			foreach (string key in properties.Keys)
			{
				if (!key.StartsWith ("_", StringComparison.Ordinal))
				{
					result.Put(key, properties[key]);
				}
			}
			return result;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual IDictionary<string, object> GetReplicationAuthParsedJson()
		{
			var authJson = "{\n" + "    \"facebook\" : {\n" + "        \"email\" : \"jchris@couchbase.com\"\n"
				 + "     }\n" + "   }\n";
            mapper = new ObjectWriter();
            var authProperties = mapper.ReadValue<Dictionary<string, object>>(authJson);
			return authProperties;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual IDictionary<string, object> GetPushReplicationParsedJson()
		{
			IDictionary<string, object> authProperties = GetReplicationAuthParsedJson();
			IDictionary<string, object> targetProperties = new Dictionary<string, object>();
			targetProperties.Put("url", GetReplicationURL().ToString());
			targetProperties["auth"] = authProperties;
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["source"] = DefaultTestDb;
			properties["target"] = targetProperties;
			return properties;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public virtual IDictionary<string, object> GetPullReplicationParsedJson()
		{
			IDictionary<string, object> authProperties = GetReplicationAuthParsedJson();
			IDictionary<string, object> sourceProperties = new Dictionary<string, object>();
			sourceProperties.Put("url", GetReplicationURL().ToString());
			sourceProperties["auth"] = authProperties;
			IDictionary<string, object> properties = new Dictionary<string, object>();
			properties["source"] = sourceProperties;
			properties["target"] = DefaultTestDb;
			return properties;
		}

        protected internal virtual HttpURLConnection SendRequest(string method, string path, 
            IDictionary<string, string> headers, IDictionary<string, object> bodyObj)
		{
			try
			{
                var url = new Uri(new Uri((string)bodyObj["remote_url"]), path);
                var conn = url.OpenConnection();
				conn.SetDoOutput(true);
				conn.SetRequestMethod(method);
				if (headers != null)
				{
					foreach (string header in headers.Keys)
					{
						conn.SetRequestProperty(header, headers[header]);
					}
				}
                var allProperties = conn.GetRequestProperties();
				if (bodyObj != null)
				{
                    //conn.SetDoInput(true);
					var bais = mapper.WriteValueAsBytes(bodyObj);
                    conn.SetRequestInputStream(bais);
				}
/*                var router = new Couchbase.Lite.Router.Router(manager, conn);
				router.Start();
*/				return conn;
			}
			catch (UriFormatException)
			{
                Assert.Fail();
			}
			catch (IOException)
			{
                Assert.Fail();
			}
			return null;
		}

        protected internal virtual object ParseJSONResponse(HttpURLConnection conn)
		{
            Object result = null;
            var stream = conn.GetOutputStream();
            var bytesRead = 0L;
            const Int32 chunkSize = 8192;
             
            var bytes = stream.ReadAllBytes();

            var responseBody = new Body(bytes);
			if (responseBody != null)
			{
                var json = responseBody.GetJson();
                String jsonString = null;
				if (json != null)
				{
					jsonString = Sharpen.Runtime.GetStringForBytes(json);
					try
					{
						result = mapper.ReadValue<object>(jsonString);
					}
					catch (Exception)
					{
                        Assert.Fail();
					}
				}
			}
			return result;
		}

        protected internal virtual object SendBody(string method, string path, IDictionary<string, object> bodyObj
			, int expectedStatus, object expectedResult)
		{
            var conn = SendRequest(method, path, null, bodyObj);
			object result = ParseJSONResponse(conn);
			Log.V(Tag, string.Format("%s %s --> %d", method, path, conn.GetResponseCode()));
			NUnit.Framework.Assert.AreEqual(expectedStatus, conn.GetResponseCode());
			if (expectedResult != null)
			{
				NUnit.Framework.Assert.AreEqual(expectedResult, result);
			}
			return result;
		}

        protected internal virtual object Send(string method, string path, HttpStatusCode expectedStatus
			, object expectedResult)
		{
            return SendBody(method, path, null, (int)expectedStatus, expectedResult);
		}
	}
}
