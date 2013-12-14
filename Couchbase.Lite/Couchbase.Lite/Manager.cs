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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Com.Couchbase.Lite;
using Com.Couchbase.Lite.Auth;
using Com.Couchbase.Lite.Internal;
using Com.Couchbase.Lite.Replicator;
using Com.Couchbase.Lite.Support;
using Com.Couchbase.Lite.Util;
using Sharpen;
using Newtonsoft.Json;

namespace Com.Couchbase.Lite
{
	/// <summary>Top-level CouchbaseLite object; manages a collection of databases as a CouchDB server does.
	/// 	</summary>
	/// <remarks>Top-level CouchbaseLite object; manages a collection of databases as a CouchDB server does.
	/// 	</remarks>
	public class Manager
	{
		public const string VersionString = "1.0.0-beta";

		public const string HttpErrorDomain = "CBLHTTP";

		private static readonly JsonConvert mapper = new JsonConvert();

		public const string DatabaseSuffixOld = ".touchdb";

		public const string DatabaseSuffix = ".cblite";

		public static readonly ManagerOptions DefaultOptions = new ManagerOptions(false, 
			false);

		public const string LegalCharacters = "[^a-z]{1,}[^a-z0-9_$()/+-]*$";

		private ManagerOptions options;

		private FilePath directoryFile;

		private IDictionary<string, Database> databases;

		private IList<Replication> replications;

		private ScheduledExecutorService workExecutor;

		private HttpClientFactory defaultHttpClientFactory;

		public static JsonConvert GetObjectMapper()
		{
			return mapper;
		}

		/// <summary>Constructor</summary>
		/// <exception cref="System.NotSupportedException">- not currently supported</exception>
		[InterfaceAudience.Public]
		public Manager()
		{
			string detailMessage = "Parameterless constructor is not a valid API call on Android. "
				 + " Pure java version coming soon.";
			throw new NotSupportedException(detailMessage);
		}

		/// <summary>Constructor</summary>
		[InterfaceAudience.Public]
		public Manager(FilePath directoryFile, ManagerOptions options)
		{
			this.directoryFile = directoryFile;
			this.options = (options != null) ? options : DefaultOptions;
			this.databases = new Dictionary<string, Database>();
			this.replications = new AList<Replication>();
			//create the directory, but don't fail if it already exists
			if (!directoryFile.Exists())
			{
				bool result = directoryFile.Mkdir();
				if (!result)
				{
					throw new RuntimeException("Unable to create directory " + directoryFile);
				}
			}
			UpgradeOldDatabaseFiles(directoryFile);
			workExecutor = Executors.NewSingleThreadScheduledExecutor();
		}

		/// <summary>Get shared instance</summary>
		/// <exception cref="System.NotSupportedException">- not currently supported</exception>
		[InterfaceAudience.Public]
		public static Com.Couchbase.Lite.Manager GetSharedInstance()
		{
			string detailMessage = "getSharedInstance() is not a valid API call on Android. "
				 + " Pure java version coming soon";
			throw new NotSupportedException(detailMessage);
		}

		/// <summary>Returns YES if the given name is a valid database name.</summary>
		/// <remarks>
		/// Returns YES if the given name is a valid database name.
		/// (Only the characters in "abcdefghijklmnopqrstuvwxyz0123456789_$()+-/" are allowed.)
		/// </remarks>
		[InterfaceAudience.Public]
		public static bool IsValidDatabaseName(string databaseName)
		{
			if (databaseName.Length > 0 && databaseName.Length < 240 && ContainsOnlyLegalCharacters
				(databaseName) && System.Char.IsLower(databaseName[0]))
			{
				return true;
			}
			return databaseName.Equals(Replication.ReplicatorDatabaseName);
		}

		/// <summary>The root directory of this manager (as specified at initialization time.)
		/// 	</summary>
		[InterfaceAudience.Public]
		public virtual string GetDirectory()
		{
			return directoryFile.GetAbsolutePath();
		}

		/// <summary>An array of the names of all existing databases.</summary>
		/// <remarks>An array of the names of all existing databases.</remarks>
		[InterfaceAudience.Public]
		public virtual IList<string> GetAllDatabaseNames()
		{
			string[] databaseFiles = directoryFile.List(new _FilenameFilter_131());
			IList<string> result = new AList<string>();
			foreach (string databaseFile in databaseFiles)
			{
				string trimmed = Sharpen.Runtime.Substring(databaseFile, 0, databaseFile.Length -
					 Com.Couchbase.Lite.Manager.DatabaseSuffix.Length);
				string replaced = trimmed.Replace(':', '/');
				result.AddItem(replaced);
			}
			result.Sort();
			return Sharpen.Collections.UnmodifiableList(result);
		}

		private sealed class _FilenameFilter_131 : FilenameFilter
		{
			public _FilenameFilter_131()
			{
			}

			public bool Accept(FilePath dir, string filename)
			{
				if (filename.EndsWith(Com.Couchbase.Lite.Manager.DatabaseSuffix))
				{
					return true;
				}
				return false;
			}
		}

		/// <summary>Releases all resources used by the Manager instance and closes all its databases.
		/// 	</summary>
		/// <remarks>Releases all resources used by the Manager instance and closes all its databases.
		/// 	</remarks>
		[InterfaceAudience.Public]
		public virtual void Close()
		{
			Log.I(Database.Tag, "Closing " + this);
			foreach (Database database in databases.Values)
			{
				IList<Replication> replicators = database.GetAllReplications();
				if (replicators != null)
				{
					foreach (Replication replicator in replicators)
					{
						replicator.Stop();
					}
				}
				database.Close();
			}
			databases.Clear();
			Log.I(Database.Tag, "Closed " + this);
		}

		/// <summary>Returns the database with the given name, or creates it if it doesn't exist.
		/// 	</summary>
		/// <remarks>
		/// Returns the database with the given name, or creates it if it doesn't exist.
		/// Multiple calls with the same name will return the same Database instance.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Database GetDatabase(string name)
		{
			Database db = databases.Get(name);
			if (db == null)
			{
				if (!IsValidDatabaseName(name))
				{
					throw new ArgumentException("Invalid database name: " + name);
				}
				if (options.IsReadOnly())
				{
					return null;
				}
				string path = PathForName(name);
				if (path == null)
				{
					return null;
				}
				db = new Database(path, this);
				db.SetName(name);
				databases.Put(name, db);
			}
			return db;
		}

		/// <summary>Returns the database with the given name, or null if it doesn't exist.</summary>
		/// <remarks>
		/// Returns the database with the given name, or null if it doesn't exist.
		/// Multiple calls with the same name will return the same Database instance.
		/// </remarks>
		[InterfaceAudience.Public]
		public virtual Database GetExistingDatabase(string name)
		{
			return databases.Get(name);
		}

		/// <summary>Replaces or installs a database from a file.</summary>
		/// <remarks>
		/// Replaces or installs a database from a file.
		/// This is primarily used to install a canned database on first launch of an app, in which case
		/// you should first check .exists to avoid replacing the database if it exists already. The
		/// canned database would have been copied into your app bundle at build time.
		/// </remarks>
		/// <param name="databaseName">The name of the database to replace.</param>
		/// <param name="databasePath">Path of the database file that should replace it.</param>
		/// <param name="attachmentsPath">Path of the associated attachments directory, or nil if there are no attachments.
		/// 	</param>
		/// <exception cref="System.IO.IOException"></exception>
		[InterfaceAudience.Public]
		public virtual void ReplaceDatabase(string databaseName, string databasePath, string
			 attachmentsPath)
		{
			Database database = GetDatabase(databaseName);
			string dstAttachmentsPath = database.GetAttachmentStorePath();
			FilePath sourceFile = new FilePath(databasePath);
			FilePath destFile = new FilePath(database.GetPath());
			FileDirUtils.CopyFile(sourceFile, destFile);
			FilePath attachmentsFile = new FilePath(dstAttachmentsPath);
			FileDirUtils.DeleteRecursive(attachmentsFile);
			attachmentsFile.Mkdirs();
			if (attachmentsPath != null)
			{
				FileDirUtils.CopyFolder(new FilePath(attachmentsPath), attachmentsFile);
			}
			database.ReplaceUUIDs();
		}

		private static bool ContainsOnlyLegalCharacters(string databaseName)
		{
			Sharpen.Pattern p = Sharpen.Pattern.Compile("^[abcdefghijklmnopqrstuvwxyz0123456789_$()+-/]+$"
				);
			Matcher matcher = p.Matcher(databaseName);
			return matcher.Matches();
		}

		private void UpgradeOldDatabaseFiles(FilePath directory)
		{
			FilePath[] files = directory.ListFiles(new _FilenameFilter_239());
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
					Log.W(Database.Tag, msg);
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

		private sealed class _FilenameFilter_239 : FilenameFilter
		{
			public _FilenameFilter_239()
			{
			}

			public bool Accept(FilePath file, string name)
			{
				return name.EndsWith(Com.Couchbase.Lite.Manager.DatabaseSuffixOld);
			}
		}

		private string FilenameWithNewExtension(string oldFilename, string oldExtension, 
			string newExtension)
		{
			string oldExtensionRegex = string.Format("%s$", oldExtension);
			return oldFilename.ReplaceAll(oldExtensionRegex, newExtension);
		}

		public virtual ICollection<Database> AllOpenDatabases()
		{
			return databases.Values;
		}

		/// <summary>Asynchronously dispatches a callback to run on a background thread.</summary>
		/// <remarks>
		/// Asynchronously dispatches a callback to run on a background thread. The callback will be passed
		/// Database instance.  There is not currently a known reason to use it, it may not make
		/// sense on the Android API, but it was added for the purpose of having a consistent API with iOS.
		/// </remarks>
		public virtual Future RunAsync(string databaseName, AsyncTask function)
		{
			Database database = GetDatabase(databaseName);
			return RunAsync(new _Runnable_288(function, database));
		}

		private sealed class _Runnable_288 : Runnable
		{
			public _Runnable_288(AsyncTask function, Database database)
			{
				this.function = function;
				this.database = database;
			}

			public void Run()
			{
				function.Run(database);
			}

			private readonly AsyncTask function;

			private readonly Database database;
		}

		internal virtual Future RunAsync(Runnable runnable)
		{
			return workExecutor.Submit(runnable);
		}

		private string PathForName(string name)
		{
			if ((name == null) || (name.Length == 0) || Sharpen.Pattern.Matches(LegalCharacters
				, name))
			{
				return null;
			}
			name = name.Replace('/', ':');
			string result = directoryFile.GetPath() + FilePath.separator + name + Com.Couchbase.Lite.Manager
				.DatabaseSuffix;
			return result;
		}

		private IDictionary<string, object> ParseSourceOrTarget(IDictionary<string, object
			> properties, string key)
		{
			IDictionary<string, object> result = new Dictionary<string, object>();
			object value = properties.Get(key);
			if (value is string)
			{
				result.Put("url", (string)value);
			}
			else
			{
				if (value is IDictionary)
				{
					result = (IDictionary<string, object>)value;
				}
			}
			return result;
		}

		[InterfaceAudience.Private]
		internal virtual Replication ReplicationWithDatabase(Database db, Uri remote, bool
			 push, bool create, bool start)
		{
			foreach (Replication replicator in replications)
			{
				if (replicator.GetLocalDatabase() == db && replicator.GetRemoteUrl().Equals(remote
					) && replicator.IsPull() == !push)
				{
					return replicator;
				}
			}
			if (!create)
			{
				return null;
			}
			Replication replicator_1 = null;
			if (push)
			{
				replicator_1 = new Pusher(db, remote, true, GetWorkExecutor());
			}
			else
			{
				replicator_1 = new Puller(db, remote, true, GetWorkExecutor());
			}
			replications.AddItem(replicator_1);
			if (start)
			{
				replicator_1.Start();
			}
			return replicator_1;
		}

		/// <exception cref="Com.Couchbase.Lite.CouchbaseLiteException"></exception>
		[InterfaceAudience.Private]
		public virtual Replication GetReplicator(IDictionary<string, object> properties)
		{
			Authorizer authorizer = null;
			Replication repl = null;
			Uri remote = null;
			IDictionary<string, object> remoteMap;
			IDictionary<string, object> sourceMap = ParseSourceOrTarget(properties, "source");
			IDictionary<string, object> targetMap = ParseSourceOrTarget(properties, "target");
			string source = (string)sourceMap.Get("url");
			string target = (string)targetMap.Get("url");
			bool createTargetBoolean = (bool)properties.Get("create_target");
			bool createTarget = (createTargetBoolean != null && createTargetBoolean);
			bool continuousBoolean = (bool)properties.Get("continuous");
			bool continuous = (continuousBoolean != null && continuousBoolean);
			bool cancelBoolean = (bool)properties.Get("cancel");
			bool cancel = (cancelBoolean != null && cancelBoolean);
			// Map the 'source' and 'target' JSON params to a local database and remote URL:
			if (source == null || target == null)
			{
				throw new CouchbaseLiteException("source and target are both null", new Status(Status
					.BadRequest));
			}
			bool push = false;
			Database db = GetExistingDatabase(source);
			string remoteStr = null;
			if (db != null)
			{
				remoteStr = target;
				push = true;
				remoteMap = targetMap;
			}
			else
			{
				remoteStr = source;
				if (createTarget && !cancel)
				{
					db = GetDatabase(target);
					if (!db.Open())
					{
						throw new CouchbaseLiteException("cannot open database: " + db, new Status(Status
							.InternalServerError));
					}
				}
				else
				{
					db = GetExistingDatabase(target);
				}
				if (db == null)
				{
					throw new CouchbaseLiteException("database is null", new Status(Status.NotFound));
				}
				remoteMap = sourceMap;
			}
			IDictionary<string, object> authMap = (IDictionary<string, object>)remoteMap.Get(
				"auth");
			if (authMap != null)
			{
				IDictionary<string, object> persona = (IDictionary<string, object>)authMap.Get("persona"
					);
				if (persona != null)
				{
					string email = (string)persona.Get("email");
					authorizer = new PersonaAuthorizer(email);
				}
				IDictionary<string, object> facebook = (IDictionary<string, object>)authMap.Get("facebook"
					);
				if (facebook != null)
				{
					string email = (string)facebook.Get("email");
					authorizer = new FacebookAuthorizer(email);
				}
			}
			try
			{
				remote = new Uri(remoteStr);
			}
			catch (UriFormatException)
			{
				throw new CouchbaseLiteException("malformed remote url: " + remoteStr, new Status
					(Status.BadRequest));
			}
			if (remote == null || !remote.Scheme.StartsWith("http"))
			{
				throw new CouchbaseLiteException("remote URL is null or non-http: " + remoteStr, 
					new Status(Status.BadRequest));
			}
			if (!cancel)
			{
				repl = db.GetReplicator(remote, GetDefaultHttpClientFactory(), push, continuous, 
					GetWorkExecutor());
				if (repl == null)
				{
					throw new CouchbaseLiteException("unable to create replicator with remote: " + remote
						, new Status(Status.InternalServerError));
				}
				if (authorizer != null)
				{
					repl.SetAuthorizer(authorizer);
				}
				string filterName = (string)properties.Get("filter");
				if (filterName != null)
				{
					repl.SetFilter(filterName);
					IDictionary<string, object> filterParams = (IDictionary<string, object>)properties
						.Get("query_params");
					if (filterParams != null)
					{
						repl.SetFilterParams(filterParams);
					}
				}
				if (push)
				{
					((Pusher)repl).SetCreateTarget(createTarget);
				}
			}
			else
			{
				// Cancel replication:
				repl = db.GetActiveReplicator(remote, push);
				if (repl == null)
				{
					throw new CouchbaseLiteException("unable to lookup replicator with remote: " + remote
						, new Status(Status.NotFound));
				}
			}
			return repl;
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
