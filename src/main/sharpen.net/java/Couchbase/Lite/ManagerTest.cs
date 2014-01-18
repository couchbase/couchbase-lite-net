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
using Couchbase.Lite;
using Sharpen;

namespace Couchbase.Lite
{
	public class ManagerTest : LiteTestCase
	{
		public virtual void TestServer()
		{
			//to ensure this test is easily repeatable we will explicitly remove
			//any stale foo.cblite
			bool mustExist = true;
			Database old = manager.GetDatabaseWithoutOpening("foo", mustExist);
			if (old != null)
			{
				old.Delete();
			}
			mustExist = false;
			Database db = manager.GetDatabaseWithoutOpening("foo", mustExist);
			NUnit.Framework.Assert.IsNotNull(db);
			NUnit.Framework.Assert.AreEqual("foo", db.GetName());
			NUnit.Framework.Assert.IsTrue(db.GetPath().StartsWith(GetServerPath()));
			NUnit.Framework.Assert.IsFalse(db.Exists());
			// because foo doesn't exist yet
			IList<string> databaseNames = manager.GetAllDatabaseNames();
			NUnit.Framework.Assert.IsTrue(!databaseNames.Contains("foo"));
			NUnit.Framework.Assert.IsTrue(db.Open());
			NUnit.Framework.Assert.IsTrue(db.Exists());
			databaseNames = manager.GetAllDatabaseNames();
			NUnit.Framework.Assert.IsTrue(databaseNames.Contains("foo"));
			db.Close();
			db.Delete();
		}

		/// <exception cref="System.Exception"></exception>
		public virtual void TestUpgradeOldDatabaseFiles()
		{
			string directoryName = "test-directory-" + Runtime.CurrentTimeMillis();
			string normalFilesDir = GetRootDirectory().GetAbsolutePath();
			string fakeFilesDir = string.Format("%s/%s", normalFilesDir, directoryName);
			FilePath directory = new FilePath(fakeFilesDir);
			if (!directory.Exists())
			{
				bool result = directory.Mkdir();
				if (!result)
				{
					throw new IOException("Unable to create directory " + directory);
				}
			}
			FilePath oldTouchDbFile = new FilePath(directory, string.Format("old%s", Manager.
				DatabaseSuffixOld));
			oldTouchDbFile.CreateNewFile();
			FilePath newCbLiteFile = new FilePath(directory, string.Format("new%s", Manager.DatabaseSuffix
				));
			newCbLiteFile.CreateNewFile();
			FilePath migratedOldFile = new FilePath(directory, string.Format("old%s", Manager
				.DatabaseSuffix));
			migratedOldFile.CreateNewFile();
			base.StopCBLite();
			manager = new Manager(new FilePath(GetRootDirectory(), directoryName), Manager.DefaultOptions
				);
			NUnit.Framework.Assert.IsTrue(migratedOldFile.Exists());
			//cannot rename old.touchdb in old.cblite, old.cblite already exists
			NUnit.Framework.Assert.IsTrue(oldTouchDbFile.Exists());
			NUnit.Framework.Assert.IsTrue(newCbLiteFile.Exists());
			FilePath dir = new FilePath(GetRootDirectory(), directoryName);
			NUnit.Framework.Assert.AreEqual(3, dir.ListFiles().Length);
			base.StopCBLite();
			migratedOldFile.Delete();
			manager = new Manager(new FilePath(GetRootDirectory(), directoryName), Manager.DefaultOptions
				);
			//rename old.touchdb in old.cblite, previous old.cblite already doesn't exist
			NUnit.Framework.Assert.IsTrue(migratedOldFile.Exists());
			NUnit.Framework.Assert.IsTrue(oldTouchDbFile.Exists() == false);
			NUnit.Framework.Assert.IsTrue(newCbLiteFile.Exists());
			dir = new FilePath(GetRootDirectory(), directoryName);
			NUnit.Framework.Assert.AreEqual(2, dir.ListFiles().Length);
		}
	}
}
