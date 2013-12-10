/**
 * Couchbase Lite for .NET
 *
 * Original iOS version by Jens Alfke
 * Android Port by Marty Schoch, Traun Leyden
 * C# Port by Zack Gramana
 *
 * Copyright (c) 2012, 2013 Couchbase, Inc. All rights reserved.
 * Portions (c) 2013 Xamarin, Inc. All rights reserved.
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
using Couchbase;
using Couchbase.Support;
using Couchbase.Util;
using Sharpen;

namespace Couchbase
{
	/// <summary>Manages a directory containing CBLDatabases.</summary>
	/// <remarks>Manages a directory containing CBLDatabases.</remarks>
	public class CBLServer
	{
		private static readonly JsonConvert mapper = new JsonConvert();

		public const string LegalCharacters = "[^a-z]{1,}[^a-z0-9_$()/+-]*$";

		public const string DatabaseSuffixOld = ".touchdb";

		public const string DatabaseSuffix = ".cblite";

		private FilePath directory;

		private IDictionary<string, CBLDatabase> databases;

		private HttpClientFactory defaultHttpClientFactory;

		private ScheduledExecutorService workExecutor;

		private CBLManager manager;

		public static JsonConvert GetObjectMapper()
		{
			return mapper;
		}

		/// <exception cref="System.IO.IOException"></exception>
		public CBLServer(string directoryName) : this(directoryName, new CBLManager())
		{
		}

		/// <exception cref="System.IO.IOException"></exception>
		public CBLServer(string directoryName, CBLManager manager)
		{
			this.directory = new FilePath(directoryName);
			this.databases = new Dictionary<string, CBLDatabase>();
			//create the directory, but don't fail if it already exists
			if (!directory.Exists())
			{
				bool result = directory.Mkdir();
				if (!result)
				{
					throw new IOException("Unable to create directory " + directory);
				}
			}
			UpgradeOldDatabaseFiles(this.directory);
			workExecutor = Executors.NewSingleThreadScheduledExecutor();
			manager.SetServer(this);
			this.manager = manager;
		}

		private void UpgradeOldDatabaseFiles(FilePath directory)
		{
			FilePath[] files = directory.ListFiles(new _FilenameFilter_87());
			foreach (FilePath file in files)
			{
				string oldFilename = file.GetName();
				string newFilename = FilenameWithNewExtension(oldFilename, DatabaseSuffixOld, DatabaseSuffix
					);
				FilePath newFile = new FilePath(directory, newFilename);
				if (newFile.Exists())
				{
					string msg = string.Format("Cannot rename %s to %s, %s already exists", oldFilename
						, newFilename, newFilename);
					Log.W(CBLDatabase.Tag, msg);
					continue;
				}
				bool ok = file.RenameTo(newFile);
				if (!ok)
				{
					string msg = string.Format("Unable to rename %s to %s", oldFilename, newFilename);
					throw new InvalidOperationException(msg);
				}
			}
		}

		private sealed class _FilenameFilter_87 : FilenameFilter
		{
			public _FilenameFilter_87()
			{
			}

			public bool Accept(FilePath file, string name)
			{
				return name.EndsWith(Couchbase.CBLServer.DatabaseSuffixOld);
			}
		}

		private string FilenameWithNewExtension(string oldFilename, string oldExtension, 
			string newExtension)
		{
			string oldExtensionRegex = string.Format("%s$", oldExtension);
			return oldFilename.ReplaceAll(oldExtensionRegex, newExtension);
		}

		public virtual CBLManager GetManager()
		{
			return manager;
		}

		private string PathForName(string name)
		{
			if ((name == null) || (name.Length == 0) || Sharpen.Pattern.Matches(LegalCharacters
				, name))
			{
				return null;
			}
			name = name.Replace('/', ':');
			string result = directory.GetPath() + FilePath.separator + name + DatabaseSuffix;
			return result;
		}

		public virtual CBLDatabase GetDatabaseNamed(string name, bool create)
		{
			CBLDatabase db = databases.Get(name);
			if (db == null)
			{
				string path = PathForName(name);
				if (path == null)
				{
					return null;
				}
				db = new CBLDatabase(path, manager);
				if (!create && !db.Exists())
				{
					return null;
				}
				db.SetName(name);
				databases.Put(name, db);
			}
			return db;
		}

		public virtual CBLDatabase GetDatabaseNamed(string name)
		{
			return GetDatabaseNamed(name, true);
		}

		public virtual CBLDatabase GetExistingDatabaseNamed(string name)
		{
			CBLDatabase db = GetDatabaseNamed(name, false);
			if ((db != null) && !db.Open())
			{
				return null;
			}
			return db;
		}

		public virtual bool DeleteDatabaseNamed(string name)
		{
			CBLDatabase db = databases.Get(name);
			if (db == null)
			{
				return false;
			}
			db.DeleteDatabase();
			Sharpen.Collections.Remove(databases, name);
			return true;
		}

		public virtual IList<string> AllDatabaseNames()
		{
			string[] databaseFiles = directory.List(new _FilenameFilter_171());
			IList<string> result = new AList<string>();
			foreach (string databaseFile in databaseFiles)
			{
				string trimmed = Sharpen.Runtime.Substring(databaseFile, 0, databaseFile.Length -
					 DatabaseSuffix.Length);
				string replaced = trimmed.Replace(':', '/');
				result.AddItem(replaced);
			}
			result.Sort();
			return result;
		}

		private sealed class _FilenameFilter_171 : FilenameFilter
		{
			public _FilenameFilter_171()
			{
			}

			public bool Accept(FilePath dir, string filename)
			{
				if (filename.EndsWith(Couchbase.CBLServer.DatabaseSuffix))
				{
					return true;
				}
				return false;
			}
		}

		public virtual ICollection<CBLDatabase> AllOpenDatabases()
		{
			return databases.Values;
		}

		public virtual void Close()
		{
			Future<object> closeFuture = workExecutor.Submit(new _Runnable_196(this));
			try
			{
				closeFuture.Get();
				// FIXME not happy with this solution, but it helps to
				// avoid accumulating lots of these
				// now schedule ourselves to shutdown after 60 second delay
				// enough time for the remaining tasks to be scheduled
				workExecutor.Schedule(new _Runnable_214(this), 60, TimeUnit.Seconds);
			}
			catch (Exception e)
			{
				Log.E(CBLDatabase.Tag, "Exception while closing", e);
			}
		}

		private sealed class _Runnable_196 : Runnable
		{
			public _Runnable_196(CBLServer _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Run()
			{
				foreach (CBLDatabase database in this._enclosing.databases.Values)
				{
					database.Close();
				}
				this._enclosing.databases.Clear();
			}

			private readonly CBLServer _enclosing;
		}

		private sealed class _Runnable_214 : Runnable
		{
			public _Runnable_214(CBLServer _enclosing)
			{
				this._enclosing = _enclosing;
			}

			public void Run()
			{
				Log.V(CBLDatabase.Tag, "Shutting Down");
				this._enclosing.workExecutor.Shutdown();
			}

			private readonly CBLServer _enclosing;
		}

		public virtual ScheduledExecutorService GetWorkExecutor()
		{
			return workExecutor;
		}

		public virtual HttpClientFactory GetDefaultHttpClientFactory()
		{
			return defaultHttpClientFactory;
		}

		public virtual void SetDefaultHttpClientFactory(HttpClientFactory defaultHttpClientFactory
			)
		{
			this.defaultHttpClientFactory = defaultHttpClientFactory;
		}
	}
}
