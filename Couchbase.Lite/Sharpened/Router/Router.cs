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
using System.Reflection;
using System.Text;
using Couchbase.Lite;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Internal;
using Couchbase.Lite.Replicator;
using Couchbase.Lite.Router;
using Couchbase.Lite.Util;
using Org.Apache.Http.Client;
using Sharpen;

namespace Couchbase.Lite.Router
{
	public class Router : Database.ChangeListener
	{
		private Manager manager;

		private Database db;

		private URLConnection connection;

		private IDictionary<string, string> queries;

		private bool changesIncludesDocs = false;

		private RouterCallbackBlock callbackBlock;

		private bool responseSent = false;

		private bool waiting = false;

		private ReplicationFilter changesFilter;

		private bool longpoll = false;

		public static string GetVersionString()
		{
			return Manager.Version;
		}

		public Router(Manager manager, URLConnection connection)
		{
			this.manager = manager;
			this.connection = connection;
		}

		public virtual void SetCallbackBlock(RouterCallbackBlock callbackBlock)
		{
			this.callbackBlock = callbackBlock;
		}

		public virtual IDictionary<string, string> GetQueries()
		{
			if (queries == null)
			{
				string queryString = connection.GetURL().GetQuery();
				if (queryString != null && queryString.Length > 0)
				{
					queries = new Dictionary<string, string>();
					foreach (string component in queryString.Split("&"))
					{
						int location = component.IndexOf('=');
						if (location > 0)
						{
							string key = Sharpen.Runtime.Substring(component, 0, location);
							string value = Sharpen.Runtime.Substring(component, location + 1);
							queries.Put(key, value);
						}
					}
				}
			}
			return queries;
		}

		public virtual string GetQuery(string param)
		{
			IDictionary<string, string> queries = GetQueries();
			if (queries != null)
			{
				string value = queries.Get(param);
				if (value != null)
				{
					return URLDecoder.Decode(value);
				}
			}
			return null;
		}

		public virtual bool GetBooleanQuery(string param)
		{
			string value = GetQuery(param);
			return (value != null) && !"false".Equals(value) && !"0".Equals(value);
		}

		public virtual int GetIntQuery(string param, int defaultValue)
		{
			int result = defaultValue;
			string value = GetQuery(param);
			if (value != null)
			{
				try
				{
					result = System.Convert.ToInt32(value);
				}
				catch (FormatException)
				{
				}
			}
			//ignore, will return default value
			return result;
		}

		public virtual object GetJSONQuery(string param)
		{
			string value = GetQuery(param);
			if (value == null)
			{
				return null;
			}
			object result = null;
			try
			{
				result = Manager.GetObjectMapper().ReadValue<object>(value);
			}
			catch (Exception e)
			{
				Log.W("Unable to parse JSON Query", e);
			}
			return result;
		}

		public virtual bool CacheWithEtag(string etag)
		{
			string eTag = string.Format("\"%s\"", etag);
			connection.GetResHeader().Add("Etag", eTag);
			string requestIfNoneMatch = connection.GetRequestProperty("If-None-Match");
			return eTag.Equals(requestIfNoneMatch);
		}

		public virtual IDictionary<string, object> GetBodyAsDictionary()
		{
			try
			{
				InputStream contentStream = connection.GetRequestInputStream();
				IDictionary<string, object> bodyMap = Manager.GetObjectMapper().ReadValue<IDictionary
					>(contentStream);
				return bodyMap;
			}
			catch (IOException e)
			{
				Log.W(Database.Tag, "WARNING: Exception parsing body into dictionary", e);
				return null;
			}
		}

		public virtual EnumSet<Database.TDContentOptions> GetContentOptions()
		{
			EnumSet<Database.TDContentOptions> result = EnumSet.NoneOf<Database.TDContentOptions
				>();
			if (GetBooleanQuery("attachments"))
			{
				result.AddItem(Database.TDContentOptions.TDIncludeAttachments);
			}
			if (GetBooleanQuery("local_seq"))
			{
				result.AddItem(Database.TDContentOptions.TDIncludeLocalSeq);
			}
			if (GetBooleanQuery("conflicts"))
			{
				result.AddItem(Database.TDContentOptions.TDIncludeConflicts);
			}
			if (GetBooleanQuery("revs"))
			{
				result.AddItem(Database.TDContentOptions.TDIncludeRevs);
			}
			if (GetBooleanQuery("revs_info"))
			{
				result.AddItem(Database.TDContentOptions.TDIncludeRevsInfo);
			}
			return result;
		}

		public virtual bool GetQueryOptions(QueryOptions options)
		{
			// http://wiki.apache.org/couchdb/HTTP_view_API#Querying_Options
			options.SetSkip(GetIntQuery("skip", options.GetSkip()));
			options.SetLimit(GetIntQuery("limit", options.GetLimit()));
			options.SetGroupLevel(GetIntQuery("group_level", options.GetGroupLevel()));
			options.SetDescending(GetBooleanQuery("descending"));
			options.SetIncludeDocs(GetBooleanQuery("include_docs"));
			options.SetUpdateSeq(GetBooleanQuery("update_seq"));
			if (GetQuery("inclusive_end") != null)
			{
				options.SetInclusiveEnd(GetBooleanQuery("inclusive_end"));
			}
			if (GetQuery("reduce") != null)
			{
				options.SetReduce(GetBooleanQuery("reduce"));
			}
			options.SetGroup(GetBooleanQuery("group"));
			options.SetContentOptions(GetContentOptions());
			IList<object> keys;
			object keysParam = GetJSONQuery("keys");
			if (keysParam != null && !(keysParam is IList))
			{
				return false;
			}
			else
			{
				keys = (IList<object>)keysParam;
			}
			if (keys == null)
			{
				object key = GetJSONQuery("key");
				if (key != null)
				{
					keys = new AList<object>();
					keys.AddItem(key);
				}
			}
			if (keys != null)
			{
				options.SetKeys(keys);
			}
			else
			{
				options.SetStartKey(GetJSONQuery("startkey"));
				options.SetEndKey(GetJSONQuery("endkey"));
			}
			return true;
		}

		public virtual string GetMultipartRequestType()
		{
			string accept = connection.GetRequestProperty("Accept");
			if (accept.StartsWith("multipart/"))
			{
				return accept;
			}
			return null;
		}

		public virtual Status OpenDB()
		{
			if (db == null)
			{
				return new Status(Status.InternalServerError);
			}
			if (!db.Exists())
			{
				return new Status(Status.NotFound);
			}
			if (!db.Open())
			{
				return new Status(Status.InternalServerError);
			}
			return new Status(Status.Ok);
		}

		public static IList<string> SplitPath(Uri url)
		{
			string pathString = url.AbsolutePath;
			if (pathString.StartsWith("/"))
			{
				pathString = Sharpen.Runtime.Substring(pathString, 1);
			}
			IList<string> result = new AList<string>();
			//we want empty string to return empty list
			if (pathString.Length == 0)
			{
				return result;
			}
			foreach (string component in pathString.Split("/"))
			{
				result.AddItem(URLDecoder.Decode(component));
			}
			return result;
		}

		public virtual void SendResponse()
		{
			if (!responseSent)
			{
				responseSent = true;
				if (callbackBlock != null)
				{
					callbackBlock.OnResponseReady();
				}
			}
		}

		public virtual void Start()
		{
			// Refer to: http://wiki.apache.org/couchdb/Complete_HTTP_API_Reference
			// We're going to map the request into a method call using reflection based on the method and path.
			// Accumulate the method name into the string 'message':
			string method = connection.GetRequestMethod();
			if ("HEAD".Equals(method))
			{
				method = "GET";
			}
			string message = string.Format("do_%s", method);
			// First interpret the components of the request:
			IList<string> path = SplitPath(connection.GetURL());
			if (path == null)
			{
				connection.SetResponseCode(Status.BadRequest);
				try
				{
					connection.GetResponseOutputStream().Close();
				}
				catch (IOException)
				{
					Log.E(Database.Tag, "Error closing empty output stream");
				}
				SendResponse();
				return;
			}
			int pathLen = path.Count;
			if (pathLen > 0)
			{
				string dbName = path[0];
				if (dbName.StartsWith("_"))
				{
					message += dbName;
				}
				else
				{
					// special root path, like /_all_dbs
					message += "_Database";
					if (!Manager.IsValidDatabaseName(dbName))
					{
						Header resHeader = connection.GetResHeader();
						if (resHeader != null)
						{
							resHeader.Add("Content-Type", "application/json");
						}
						IDictionary<string, object> result = new Dictionary<string, object>();
						result.Put("error", "Invalid database");
						result.Put("status", Status.BadRequest);
						connection.SetResponseBody(new Body(result));
						ByteArrayInputStream bais = new ByteArrayInputStream(connection.GetResponseBody()
							.GetJson());
						connection.SetResponseInputStream(bais);
						connection.SetResponseCode(Status.BadRequest);
						try
						{
							connection.GetResponseOutputStream().Close();
						}
						catch (IOException)
						{
							Log.E(Database.Tag, "Error closing empty output stream");
						}
						SendResponse();
						return;
					}
					else
					{
						bool mustExist = false;
						db = manager.GetDatabaseWithoutOpening(dbName, mustExist);
						if (db == null)
						{
							connection.SetResponseCode(Status.BadRequest);
							try
							{
								connection.GetResponseOutputStream().Close();
							}
							catch (IOException)
							{
								Log.E(Database.Tag, "Error closing empty output stream");
							}
							SendResponse();
							return;
						}
					}
				}
			}
			else
			{
				message += "Root";
			}
			string docID = null;
			if (db != null && pathLen > 1)
			{
				message = message.ReplaceFirst("_Database", "_Document");
				// Make sure database exists, then interpret doc name:
				Status status = OpenDB();
				if (!status.IsSuccessful())
				{
					connection.SetResponseCode(status.GetCode());
					try
					{
						connection.GetResponseOutputStream().Close();
					}
					catch (IOException)
					{
						Log.E(Database.Tag, "Error closing empty output stream");
					}
					SendResponse();
					return;
				}
				string name = path[1];
				if (!name.StartsWith("_"))
				{
					// Regular document
					if (!Database.IsValidDocumentId(name))
					{
						connection.SetResponseCode(Status.BadRequest);
						try
						{
							connection.GetResponseOutputStream().Close();
						}
						catch (IOException)
						{
							Log.E(Database.Tag, "Error closing empty output stream");
						}
						SendResponse();
						return;
					}
					docID = name;
				}
				else
				{
					if ("_design".Equals(name) || "_local".Equals(name))
					{
						// "_design/____" and "_local/____" are document names
						if (pathLen <= 2)
						{
							connection.SetResponseCode(Status.NotFound);
							try
							{
								connection.GetResponseOutputStream().Close();
							}
							catch (IOException)
							{
								Log.E(Database.Tag, "Error closing empty output stream");
							}
							SendResponse();
							return;
						}
						docID = name + "/" + path[2];
						path.Set(1, docID);
						path.Remove(2);
						pathLen--;
					}
					else
					{
						if (name.StartsWith("_design") || name.StartsWith("_local"))
						{
							// This is also a document, just with a URL-encoded "/"
							docID = name;
						}
						else
						{
							// Special document name like "_all_docs":
							message += name;
							if (pathLen > 2)
							{
								IList<string> subList = path.SubList(2, pathLen - 1);
								StringBuilder sb = new StringBuilder();
								IEnumerator<string> iter = subList.GetEnumerator();
								while (iter.HasNext())
								{
									sb.Append(iter.Next());
									if (iter.HasNext())
									{
										sb.Append("/");
									}
								}
								docID = sb.ToString();
							}
						}
					}
				}
			}
			string attachmentName = null;
			if (docID != null && pathLen > 2)
			{
				message = message.ReplaceFirst("_Document", "_Attachment");
				// Interpret attachment name:
				attachmentName = path[2];
				if (attachmentName.StartsWith("_") && docID.StartsWith("_design"))
				{
					// Design-doc attribute like _info or _view
					message = message.ReplaceFirst("_Attachment", "_DesignDocument");
					docID = Sharpen.Runtime.Substring(docID, 8);
					// strip the "_design/" prefix
					attachmentName = pathLen > 3 ? path[3] : null;
				}
				else
				{
					if (pathLen > 3)
					{
						IList<string> subList = path.SubList(2, pathLen);
						StringBuilder sb = new StringBuilder();
						IEnumerator<string> iter = subList.GetEnumerator();
						while (iter.HasNext())
						{
							sb.Append(iter.Next());
							if (iter.HasNext())
							{
								//sb.append("%2F");
								sb.Append("/");
							}
						}
						attachmentName = sb.ToString();
					}
				}
			}
			//Log.d(TAG, "path: " + path + " message: " + message + " docID: " + docID + " attachmentName: " + attachmentName);
			// Send myself a message based on the components:
			Status status_1 = null;
			try
			{
				MethodInfo m = typeof(Couchbase.Lite.Router.Router).GetMethod(message, typeof(Database
					), typeof(string), typeof(string));
				status_1 = (Status)m.Invoke(this, db, docID, attachmentName);
			}
			catch (NoSuchMethodException)
			{
				try
				{
					string errorMessage = "Router unable to route request to " + message;
					Log.E(Database.Tag, errorMessage);
					IDictionary<string, object> result = new Dictionary<string, object>();
					result.Put("error", "not_found");
					result.Put("reason", errorMessage);
					connection.SetResponseBody(new Body(result));
					MethodInfo m = typeof(Couchbase.Lite.Router.Router).GetMethod("do_UNKNOWN", typeof(
						Database), typeof(string), typeof(string));
					status_1 = (Status)m.Invoke(this, db, docID, attachmentName);
				}
				catch (Exception e)
				{
					//default status is internal server error
					Log.E(Database.Tag, "Router attempted do_UNKNWON fallback, but that threw an exception"
						, e);
					IDictionary<string, object> result = new Dictionary<string, object>();
					result.Put("error", "not_found");
					result.Put("reason", "Router unable to route request");
					connection.SetResponseBody(new Body(result));
					status_1 = new Status(Status.NotFound);
				}
			}
			catch (Exception e)
			{
				string errorMessage = "Router unable to route request to " + message;
				Log.E(Database.Tag, errorMessage, e);
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("error", "not_found");
				result.Put("reason", errorMessage + e.ToString());
				connection.SetResponseBody(new Body(result));
				if (e is CouchbaseLiteException)
				{
					status_1 = ((CouchbaseLiteException)e).GetCBLStatus();
				}
				else
				{
					status_1 = new Status(Status.NotFound);
				}
			}
			// Configure response headers:
			if (status_1.IsSuccessful() && connection.GetResponseBody() == null && connection
				.GetHeaderField("Content-Type") == null)
			{
				connection.SetResponseBody(new Body(Sharpen.Runtime.GetBytesForString("{\"ok\":true}"
					)));
			}
			if (status_1.IsSuccessful() == false && connection.GetResponseBody() == null)
			{
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("status", status_1.GetCode());
				connection.SetResponseBody(new Body(result));
			}
			if (connection.GetResponseBody() != null && connection.GetResponseBody().IsValidJSON
				())
			{
				Header resHeader = connection.GetResHeader();
				if (resHeader != null)
				{
					resHeader.Add("Content-Type", "application/json");
				}
				else
				{
					Log.W(Database.Tag, "Cannot add Content-Type header because getResHeader() returned null"
						);
				}
			}
			// Check for a mismatch between the Accept request header and the response type:
			string accept = connection.GetRequestProperty("Accept");
			if (accept != null && !"*/*".Equals(accept))
			{
				string responseType = connection.GetBaseContentType();
				if (responseType != null && accept.IndexOf(responseType) < 0)
				{
					Log.E(Database.Tag, string.Format("Error 406: Can't satisfy request Accept: %s", 
						accept));
					status_1 = new Status(Status.NotAcceptable);
				}
			}
			connection.GetResHeader().Add("Server", string.Format("Couchbase Lite %s", GetVersionString
				()));
			// If response is ready (nonzero status), tell my client about it:
			if (status_1.GetCode() != 0)
			{
				connection.SetResponseCode(status_1.GetCode());
				if (connection.GetResponseBody() != null)
				{
					ByteArrayInputStream bais = new ByteArrayInputStream(connection.GetResponseBody()
						.GetJson());
					connection.SetResponseInputStream(bais);
				}
				else
				{
					try
					{
						connection.GetResponseOutputStream().Close();
					}
					catch (IOException)
					{
						Log.E(Database.Tag, "Error closing empty output stream");
					}
				}
				SendResponse();
			}
		}

		public virtual void Stop()
		{
			callbackBlock = null;
			if (db != null)
			{
				db.RemoveChangeListener(this);
			}
		}

		public virtual Status Do_UNKNOWN(Database db, string docID, string attachmentName
			)
		{
			return new Status(Status.BadRequest);
		}

		public virtual void SetResponseLocation(Uri url)
		{
			string location = url.ToExternalForm();
			string query = url.GetQuery();
			if (query != null)
			{
				int startOfQuery = location.IndexOf(query);
				if (startOfQuery > 0)
				{
					location = Sharpen.Runtime.Substring(location, 0, startOfQuery);
				}
			}
			connection.GetResHeader().Add("Location", location);
		}

		/// <summary>SERVER REQUESTS:</summary>
		public virtual Status Do_GETRoot(Database _db, string _docID, string _attachmentName
			)
		{
			IDictionary<string, object> info = new Dictionary<string, object>();
			info.Put("CBLite", "Welcome");
			info.Put("couchdb", "Welcome");
			// for compatibility
			info.Put("version", GetVersionString());
			connection.SetResponseBody(new Body(info));
			return new Status(Status.Ok);
		}

		public virtual Status Do_GET_all_dbs(Database _db, string _docID, string _attachmentName
			)
		{
			IList<string> dbs = manager.GetAllDatabaseNames();
			connection.SetResponseBody(new Body(dbs));
			return new Status(Status.Ok);
		}

		public virtual Status Do_GET_session(Database _db, string _docID, string _attachmentName
			)
		{
			// Send back an "Admin Party"-like response
			IDictionary<string, object> session = new Dictionary<string, object>();
			IDictionary<string, object> userCtx = new Dictionary<string, object>();
			string[] roles = new string[] { "_admin" };
			session.Put("ok", true);
			userCtx.Put("name", null);
			userCtx.Put("roles", roles);
			session.Put("userCtx", userCtx);
			connection.SetResponseBody(new Body(session));
			return new Status(Status.Ok);
		}

		public virtual Status Do_POST_replicate(Database _db, string _docID, string _attachmentName
			)
		{
			Replication replicator;
			// Extract the parameters from the JSON request body:
			// http://wiki.apache.org/couchdb/Replication
			IDictionary<string, object> body = GetBodyAsDictionary();
			if (body == null)
			{
				return new Status(Status.BadRequest);
			}
			try
			{
				replicator = manager.GetReplicator(body);
			}
			catch (CouchbaseLiteException e)
			{
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("error", e.ToString());
				connection.SetResponseBody(new Body(result));
				return e.GetCBLStatus();
			}
			bool cancelBoolean = (bool)body.Get("cancel");
			bool cancel = (cancelBoolean != null && cancelBoolean);
			if (!cancel)
			{
				replicator.Start();
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("session_id", replicator.GetSessionID());
				connection.SetResponseBody(new Body(result));
			}
			else
			{
				// Cancel replication:
				replicator.Stop();
			}
			return new Status(Status.Ok);
		}

		public virtual Status Do_GET_uuids(Database _db, string _docID, string _attachmentName
			)
		{
			int count = Math.Min(1000, GetIntQuery("count", 1));
			IList<string> uuids = new AList<string>(count);
			for (int i = 0; i < count; i++)
			{
				uuids.AddItem(Database.GenerateDocumentId());
			}
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("uuids", uuids);
			connection.SetResponseBody(new Body(result));
			return new Status(Status.Ok);
		}

		public virtual Status Do_GET_active_tasks(Database _db, string _docID, string _attachmentName
			)
		{
			// http://wiki.apache.org/couchdb/HttpGetActiveTasks
			IList<IDictionary<string, object>> activities = new AList<IDictionary<string, object
				>>();
			foreach (Database db in manager.AllOpenDatabases())
			{
				IList<Replication> activeReplicators = db.GetAllReplications();
				if (activeReplicators != null)
				{
					foreach (Replication replicator in activeReplicators)
					{
						string source = replicator.GetRemoteUrl().ToExternalForm();
						string target = db.GetName();
						if (!replicator.IsPull())
						{
							string tmp = source;
							source = target;
							target = tmp;
						}
						int processed = replicator.GetCompletedChangesCount();
						int total = replicator.GetChangesCount();
						string status = string.Format("Processed %d / %d changes", processed, total);
						int progress = (total > 0) ? Math.Round(100 * processed / (float)total) : 0;
						IDictionary<string, object> activity = new Dictionary<string, object>();
						activity.Put("type", "Replication");
						activity.Put("task", replicator.GetSessionID());
						activity.Put("source", source);
						activity.Put("target", target);
						activity.Put("status", status);
						activity.Put("progress", progress);
						if (replicator.GetLastError() != null)
						{
							string msg = string.Format("Replicator error: %s.  Repl: %s.  Source: %s, Target: %s"
								, replicator.GetLastError(), replicator, source, target);
							Log.E(Database.Tag, msg);
							Exception error = replicator.GetLastError();
							int statusCode = 400;
							if (error is HttpResponseException)
							{
								statusCode = ((HttpResponseException)error).GetStatusCode();
							}
							object[] errorObjects = new object[] { statusCode, replicator.GetLastError().ToString
								() };
							activity.Put("error", errorObjects);
						}
						activities.AddItem(activity);
					}
				}
			}
			connection.SetResponseBody(new Body(activities));
			return new Status(Status.Ok);
		}

		/// <summary>DATABASE REQUESTS:</summary>
		public virtual Status Do_GET_Database(Database _db, string _docID, string _attachmentName
			)
		{
			// http://wiki.apache.org/couchdb/HTTP_database_API#Database_Information
			Status status = OpenDB();
			if (!status.IsSuccessful())
			{
				return status;
			}
			int num_docs = db.GetDocumentCount();
			long update_seq = db.GetLastSequenceNumber();
			long instanceStartTimeMicroseconds = db.GetStartTime() * 1000;
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("db_name", db.GetName());
			result.Put("db_uuid", db.PublicUUID());
			result.Put("doc_count", num_docs);
			result.Put("update_seq", update_seq);
			result.Put("disk_size", db.TotalDataSize());
			result.Put("instance_start_time", instanceStartTimeMicroseconds);
			connection.SetResponseBody(new Body(result));
			return new Status(Status.Ok);
		}

		public virtual Status Do_PUT_Database(Database _db, string _docID, string _attachmentName
			)
		{
			if (db.Exists())
			{
				return new Status(Status.PreconditionFailed);
			}
			if (!db.Open())
			{
				return new Status(Status.InternalServerError);
			}
			SetResponseLocation(connection.GetURL());
			return new Status(Status.Created);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_DELETE_Database(Database _db, string _docID, string _attachmentName
			)
		{
			if (GetQuery("rev") != null)
			{
				return new Status(Status.BadRequest);
			}
			// CouchDB checks for this; probably meant to be a document deletion
			db.Delete();
			return new Status(Status.Ok);
		}

		/// <summary>
		/// This is a hack to deal with the fact that there is currently no custom
		/// serializer for QueryRow.
		/// </summary>
		/// <remarks>
		/// This is a hack to deal with the fact that there is currently no custom
		/// serializer for QueryRow.  Instead, just convert everything to generic Maps.
		/// </remarks>
		private void ConvertCBLQueryRowsToMaps(IDictionary<string, object> allDocsResult)
		{
			IList<IDictionary<string, object>> rowsAsMaps = new AList<IDictionary<string, object
				>>();
			IList<QueryRow> rows = (IList<QueryRow>)allDocsResult.Get("rows");
			foreach (QueryRow row in rows)
			{
				rowsAsMaps.AddItem(row.AsJSONDictionary());
			}
			allDocsResult.Put("rows", rowsAsMaps);
		}

		public virtual Status Do_POST_Database(Database _db, string _docID, string _attachmentName
			)
		{
			Status status = OpenDB();
			if (!status.IsSuccessful())
			{
				return status;
			}
			return Update(db, null, GetBodyAsDictionary(), false);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_GET_Document_all_docs(Database _db, string _docID, string
			 _attachmentName)
		{
			QueryOptions options = new QueryOptions();
			if (!GetQueryOptions(options))
			{
				return new Status(Status.BadRequest);
			}
			IDictionary<string, object> result = db.GetAllDocs(options);
			ConvertCBLQueryRowsToMaps(result);
			if (result == null)
			{
				return new Status(Status.InternalServerError);
			}
			connection.SetResponseBody(new Body(result));
			return new Status(Status.Ok);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_POST_Document_all_docs(Database _db, string _docID, string
			 _attachmentName)
		{
			QueryOptions options = new QueryOptions();
			if (!GetQueryOptions(options))
			{
				return new Status(Status.BadRequest);
			}
			IDictionary<string, object> body = GetBodyAsDictionary();
			if (body == null)
			{
				return new Status(Status.BadRequest);
			}
			IList<object> keys = (IList<object>)body.Get("keys");
			options.SetKeys(keys);
			IDictionary<string, object> result = null;
			result = db.GetAllDocs(options);
			ConvertCBLQueryRowsToMaps(result);
			if (result == null)
			{
				return new Status(Status.InternalServerError);
			}
			connection.SetResponseBody(new Body(result));
			return new Status(Status.Ok);
		}

		public virtual Status Do_POST_facebook_token(Database _db, string _docID, string 
			_attachmentName)
		{
			IDictionary<string, object> body = GetBodyAsDictionary();
			if (body == null)
			{
				return new Status(Status.BadRequest);
			}
			string email = (string)body.Get("email");
			string remoteUrl = (string)body.Get("remote_url");
			string accessToken = (string)body.Get("access_token");
			if (email != null && remoteUrl != null && accessToken != null)
			{
				try
				{
					Uri siteUrl = new Uri(remoteUrl);
				}
				catch (UriFormatException e)
				{
					IDictionary<string, object> result = new Dictionary<string, object>();
					result.Put("error", "invalid remote_url: " + e.GetLocalizedMessage());
					connection.SetResponseBody(new Body(result));
					return new Status(Status.BadRequest);
				}
				try
				{
					FacebookAuthorizer.RegisterAccessToken(accessToken, email, remoteUrl);
				}
				catch (Exception e)
				{
					IDictionary<string, object> result = new Dictionary<string, object>();
					result.Put("error", "error registering access token: " + e.GetLocalizedMessage());
					connection.SetResponseBody(new Body(result));
					return new Status(Status.BadRequest);
				}
				IDictionary<string, object> result_1 = new Dictionary<string, object>();
				result_1.Put("ok", "registered");
				connection.SetResponseBody(new Body(result_1));
				return new Status(Status.Ok);
			}
			else
			{
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("error", "required fields: access_token, email, remote_url");
				connection.SetResponseBody(new Body(result));
				return new Status(Status.BadRequest);
			}
		}

		public virtual Status Do_POST_persona_assertion(Database _db, string _docID, string
			 _attachmentName)
		{
			IDictionary<string, object> body = GetBodyAsDictionary();
			if (body == null)
			{
				return new Status(Status.BadRequest);
			}
			string assertion = (string)body.Get("assertion");
			if (assertion == null)
			{
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("error", "required fields: assertion");
				connection.SetResponseBody(new Body(result));
				return new Status(Status.BadRequest);
			}
			try
			{
				string email = PersonaAuthorizer.RegisterAssertion(assertion);
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("ok", "registered");
				result.Put("email", email);
				connection.SetResponseBody(new Body(result));
				return new Status(Status.Ok);
			}
			catch (Exception e)
			{
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("error", "error registering persona assertion: " + e.GetLocalizedMessage
					());
				connection.SetResponseBody(new Body(result));
				return new Status(Status.BadRequest);
			}
		}

		public virtual Status Do_POST_Document_bulk_docs(Database _db, string _docID, string
			 _attachmentName)
		{
			IDictionary<string, object> bodyDict = GetBodyAsDictionary();
			if (bodyDict == null)
			{
				return new Status(Status.BadRequest);
			}
			IList<IDictionary<string, object>> docs = (IList<IDictionary<string, object>>)bodyDict
				.Get("docs");
			bool allObj = false;
			if (GetQuery("all_or_nothing") == null || (GetQuery("all_or_nothing") != null && 
				(System.Convert.ToBoolean(GetQuery("all_or_nothing")))))
			{
				allObj = true;
			}
			//   allowConflict If false, an error status 409 will be returned if the insertion would create a conflict, i.e. if the previous revision already has a child.
			bool allOrNothing = (allObj && allObj != false);
			bool noNewEdits = true;
			if (GetQuery("new_edits") == null || (GetQuery("new_edits") != null && (System.Convert.ToBoolean
				(GetQuery("new_edits")))))
			{
				noNewEdits = false;
			}
			bool ok = false;
			db.BeginTransaction();
			IList<IDictionary<string, object>> results = new AList<IDictionary<string, object
				>>();
			try
			{
				foreach (IDictionary<string, object> doc in docs)
				{
					string docID = (string)doc.Get("_id");
					RevisionInternal rev = null;
					Status status = new Status(Status.BadRequest);
					Body docBody = new Body(doc);
					if (noNewEdits)
					{
						rev = new RevisionInternal(docBody, db);
						if (rev.GetRevId() == null || rev.GetDocId() == null || !rev.GetDocId().Equals(docID
							))
						{
							status = new Status(Status.BadRequest);
						}
						else
						{
							IList<string> history = Database.ParseCouchDBRevisionHistory(doc);
							db.ForceInsert(rev, history, null);
						}
					}
					else
					{
						Status outStatus = new Status();
						rev = Update(db, docID, docBody, false, allOrNothing, outStatus);
						status.SetCode(outStatus.GetCode());
					}
					IDictionary<string, object> result = null;
					if (status.IsSuccessful())
					{
						result = new Dictionary<string, object>();
						result.Put("ok", true);
						result.Put("id", docID);
						if (rev != null)
						{
							result.Put("rev", rev.GetRevId());
						}
					}
					else
					{
						if (allOrNothing)
						{
							return status;
						}
						else
						{
							// all_or_nothing backs out if there's any error
							if (status.GetCode() == Status.Forbidden)
							{
								result = new Dictionary<string, object>();
								result.Put("error", "validation failed");
								result.Put("id", docID);
							}
							else
							{
								if (status.GetCode() == Status.Conflict)
								{
									result = new Dictionary<string, object>();
									result.Put("error", "conflict");
									result.Put("id", docID);
								}
								else
								{
									return status;
								}
							}
						}
					}
					// abort the whole thing if something goes badly wrong
					if (result != null)
					{
						results.AddItem(result);
					}
				}
				Log.W(Database.Tag, string.Format("%s finished inserting %d revisions in bulk", this
					, docs.Count));
				ok = true;
			}
			catch (Exception e)
			{
				Log.W(Database.Tag, string.Format("%s: Exception inserting revisions in bulk", this
					), e);
			}
			finally
			{
				db.EndTransaction(ok);
			}
			Log.D(Database.Tag, "results: " + results.ToString());
			connection.SetResponseBody(new Body(results));
			return new Status(Status.Created);
		}

		public virtual Status Do_POST_Document_revs_diff(Database _db, string _docID, string
			 _attachmentName)
		{
			// http://wiki.apache.org/couchdb/HttpPostRevsDiff
			// Collect all of the input doc/revision IDs as TDRevisions:
			RevisionList revs = new RevisionList();
			IDictionary<string, object> body = GetBodyAsDictionary();
			if (body == null)
			{
				return new Status(Status.BadJson);
			}
			foreach (string docID in body.Keys)
			{
				IList<string> revIDs = (IList<string>)body.Get(docID);
				foreach (string revID in revIDs)
				{
					RevisionInternal rev = new RevisionInternal(docID, revID, false, db);
					revs.AddItem(rev);
				}
			}
			// Look them up, removing the existing ones from revs:
			if (!db.FindMissingRevisions(revs))
			{
				return new Status(Status.DbError);
			}
			// Return the missing revs in a somewhat different format:
			IDictionary<string, object> diffs = new Dictionary<string, object>();
			foreach (RevisionInternal rev_1 in revs)
			{
				string docID_1 = rev_1.GetDocId();
				IList<string> missingRevs = null;
				IDictionary<string, object> idObj = (IDictionary<string, object>)diffs.Get(docID_1
					);
				if (idObj != null)
				{
					missingRevs = (IList<string>)idObj.Get("missing");
				}
				else
				{
					idObj = new Dictionary<string, object>();
				}
				if (missingRevs == null)
				{
					missingRevs = new AList<string>();
					idObj.Put("missing", missingRevs);
					diffs.Put(docID_1, idObj);
				}
				missingRevs.AddItem(rev_1.GetRevId());
			}
			// FIXME add support for possible_ancestors
			connection.SetResponseBody(new Body(diffs));
			return new Status(Status.Ok);
		}

		public virtual Status Do_POST_Document_compact(Database _db, string _docID, string
			 _attachmentName)
		{
			Status status = _db.Compact();
			if (status.GetCode() < 300)
			{
				Status outStatus = new Status();
				outStatus.SetCode(202);
				// CouchDB returns 202 'cause it's an async operation
				return outStatus;
			}
			else
			{
				return status;
			}
		}

		public virtual Status Do_POST_Document_purge(Database _db, string ignored1, string
			 ignored2)
		{
			IDictionary<string, object> body = GetBodyAsDictionary();
			if (body == null)
			{
				return new Status(Status.BadRequest);
			}
			// convert from Map<String,Object> -> Map<String, List<String>> - is there a cleaner way?
			IDictionary<string, IList<string>> docsToRevs = new Dictionary<string, IList<string
				>>();
			foreach (string key in body.Keys)
			{
				object val = body.Get(key);
				if (val is IList)
				{
					docsToRevs.Put(key, (IList<string>)val);
				}
			}
			IDictionary<string, object> purgedRevisions = db.PurgeRevisions(docsToRevs);
			IDictionary<string, object> responseMap = new Dictionary<string, object>();
			responseMap.Put("purged", purgedRevisions);
			Body responseBody = new Body(responseMap);
			connection.SetResponseBody(responseBody);
			return new Status(Status.Ok);
		}

		public virtual Status Do_POST_Document_ensure_full_commit(Database _db, string _docID
			, string _attachmentName)
		{
			return new Status(Status.Ok);
		}

		/// <summary>CHANGES:</summary>
		public virtual IDictionary<string, object> ChangesDictForRevision(RevisionInternal
			 rev)
		{
			IDictionary<string, object> changesDict = new Dictionary<string, object>();
			changesDict.Put("rev", rev.GetRevId());
			IList<IDictionary<string, object>> changes = new AList<IDictionary<string, object
				>>();
			changes.AddItem(changesDict);
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("seq", rev.GetSequence());
			result.Put("id", rev.GetDocId());
			result.Put("changes", changes);
			if (rev.IsDeleted())
			{
				result.Put("deleted", true);
			}
			if (changesIncludesDocs)
			{
				result.Put("doc", rev.GetProperties());
			}
			return result;
		}

		public virtual IDictionary<string, object> ResponseBodyForChanges(IList<RevisionInternal
			> changes, long since)
		{
			IList<IDictionary<string, object>> results = new AList<IDictionary<string, object
				>>();
			foreach (RevisionInternal rev in changes)
			{
				IDictionary<string, object> changeDict = ChangesDictForRevision(rev);
				results.AddItem(changeDict);
			}
			if (changes.Count > 0)
			{
				since = changes[changes.Count - 1].GetSequence();
			}
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("results", results);
			result.Put("last_seq", since);
			return result;
		}

		public virtual IDictionary<string, object> ResponseBodyForChangesWithConflicts(IList
			<RevisionInternal> changes, long since)
		{
			// Assumes the changes are grouped by docID so that conflicts will be adjacent.
			IList<IDictionary<string, object>> entries = new AList<IDictionary<string, object
				>>();
			string lastDocID = null;
			IDictionary<string, object> lastEntry = null;
			foreach (RevisionInternal rev in changes)
			{
				string docID = rev.GetDocId();
				if (docID.Equals(lastDocID))
				{
					IDictionary<string, object> changesDict = new Dictionary<string, object>();
					changesDict.Put("rev", rev.GetRevId());
					IList<IDictionary<string, object>> inchanges = (IList<IDictionary<string, object>
						>)lastEntry.Get("changes");
					inchanges.AddItem(changesDict);
				}
				else
				{
					lastEntry = ChangesDictForRevision(rev);
					entries.AddItem(lastEntry);
					lastDocID = docID;
				}
			}
			// After collecting revisions, sort by sequence:
			entries.Sort(new _IComparer_1081());
			long lastSeq = (long)entries[entries.Count - 1].Get("seq");
			if (lastSeq == null)
			{
				lastSeq = since;
			}
			IDictionary<string, object> result = new Dictionary<string, object>();
			result.Put("results", entries);
			result.Put("last_seq", lastSeq);
			return result;
		}

		private sealed class _IComparer_1081 : IComparer<IDictionary<string, object>>
		{
			public _IComparer_1081()
			{
			}

			public int Compare(IDictionary<string, object> e1, IDictionary<string, object> e2
				)
			{
				return Misc.TDSequenceCompare((long)e1.Get("seq"), (long)e2.Get("seq"));
			}
		}

		public virtual void SendContinuousChange(RevisionInternal rev)
		{
			IDictionary<string, object> changeDict = ChangesDictForRevision(rev);
			try
			{
				string jsonString = Manager.GetObjectMapper().WriteValueAsString(changeDict);
				if (callbackBlock != null)
				{
					byte[] json = Sharpen.Runtime.GetBytesForString((jsonString + "\n"));
					OutputStream os = connection.GetResponseOutputStream();
					try
					{
						os.Write(json);
						os.Flush();
					}
					catch (Exception e)
					{
						Log.E(Database.Tag, "IOException writing to internal streams", e);
					}
				}
			}
			catch (Exception e)
			{
				Log.W("Unable to serialize change to JSON", e);
			}
		}

		public virtual void Changed(Database.ChangeEvent @event)
		{
			IList<DocumentChange> changes = @event.GetChanges();
			foreach (DocumentChange change in changes)
			{
				RevisionInternal rev = change.GetAddedRevision();
				IDictionary<string, object> paramsFixMe = null;
				// TODO: these should not be null
				bool allowRevision = @event.GetSource().RunFilter(changesFilter, paramsFixMe, rev
					);
				if (!allowRevision)
				{
					return;
				}
				if (longpoll)
				{
					Log.W(Database.Tag, "Router: Sending longpoll response");
					SendResponse();
					IList<RevisionInternal> revs = new AList<RevisionInternal>();
					revs.AddItem(rev);
					IDictionary<string, object> body = ResponseBodyForChanges(revs, 0);
					if (callbackBlock != null)
					{
						byte[] data = null;
						try
						{
							data = Manager.GetObjectMapper().WriteValueAsBytes(body);
						}
						catch (Exception e)
						{
							Log.W(Database.Tag, "Error serializing JSON", e);
						}
						OutputStream os = connection.GetResponseOutputStream();
						try
						{
							os.Write(data);
							os.Close();
						}
						catch (IOException e)
						{
							Log.E(Database.Tag, "IOException writing to internal streams", e);
						}
					}
				}
				else
				{
					Log.W(Database.Tag, "Router: Sending continous change chunk");
					SendContinuousChange(rev);
				}
			}
		}

		public virtual Status Do_GET_Document_changes(Database _db, string docID, string 
			_attachmentName)
		{
			// http://wiki.apache.org/couchdb/HTTP_database_API#Changes
			ChangesOptions options = new ChangesOptions();
			changesIncludesDocs = GetBooleanQuery("include_docs");
			options.SetIncludeDocs(changesIncludesDocs);
			string style = GetQuery("style");
			if (style != null && style.Equals("all_docs"))
			{
				options.SetIncludeConflicts(true);
			}
			options.SetContentOptions(GetContentOptions());
			options.SetSortBySequence(!options.IsIncludeConflicts());
			options.SetLimit(GetIntQuery("limit", options.GetLimit()));
			int since = GetIntQuery("since", 0);
			string filterName = GetQuery("filter");
			if (filterName != null)
			{
				changesFilter = db.GetFilter(filterName);
				if (changesFilter == null)
				{
					return new Status(Status.NotFound);
				}
			}
			RevisionList changes = db.ChangesSince(since, options, changesFilter);
			if (changes == null)
			{
				return new Status(Status.InternalServerError);
			}
			string feed = GetQuery("feed");
			longpoll = "longpoll".Equals(feed);
			bool continuous = !longpoll && "continuous".Equals(feed);
			if (continuous || (longpoll && changes.Count == 0))
			{
				connection.SetChunked(true);
				connection.SetResponseCode(Status.Ok);
				SendResponse();
				if (continuous)
				{
					foreach (RevisionInternal rev in changes)
					{
						SendContinuousChange(rev);
					}
				}
				db.AddChangeListener(this);
				// Don't close connection; more data to come
				return new Status(0);
			}
			else
			{
				if (options.IsIncludeConflicts())
				{
					connection.SetResponseBody(new Body(ResponseBodyForChangesWithConflicts(changes, 
						since)));
				}
				else
				{
					connection.SetResponseBody(new Body(ResponseBodyForChanges(changes, since)));
				}
				return new Status(Status.Ok);
			}
		}

		/// <summary>DOCUMENT REQUESTS:</summary>
		public virtual string GetRevIDFromIfMatchHeader()
		{
			string ifMatch = connection.GetRequestProperty("If-Match");
			if (ifMatch == null)
			{
				return null;
			}
			// Value of If-Match is an ETag, so have to trim the quotes around it:
			if (ifMatch.Length > 2 && ifMatch.StartsWith("\"") && ifMatch.EndsWith("\""))
			{
				return Sharpen.Runtime.Substring(ifMatch, 1, ifMatch.Length - 2);
			}
			else
			{
				return null;
			}
		}

		public virtual string SetResponseEtag(RevisionInternal rev)
		{
			string eTag = string.Format("\"%s\"", rev.GetRevId());
			connection.GetResHeader().Add("Etag", eTag);
			return eTag;
		}

		public virtual Status Do_GET_Document(Database _db, string docID, string _attachmentName
			)
		{
			try
			{
				// http://wiki.apache.org/couchdb/HTTP_Document_API#GET
				bool isLocalDoc = docID.StartsWith("_local");
				EnumSet<Database.TDContentOptions> options = GetContentOptions();
				string openRevsParam = GetQuery("open_revs");
				if (openRevsParam == null || isLocalDoc)
				{
					// Regular GET:
					string revID = GetQuery("rev");
					// often null
					RevisionInternal rev = null;
					if (isLocalDoc)
					{
						rev = db.GetLocalDocument(docID, revID);
					}
					else
					{
						rev = db.GetDocumentWithIDAndRev(docID, revID, options);
						// Handle ?atts_since query by stubbing out older attachments:
						//?atts_since parameter - value is a (URL-encoded) JSON array of one or more revision IDs.
						// The response will include the content of only those attachments that changed since the given revision(s).
						//(You can ask for this either in the default JSON or as multipart/related, as previously described.)
						IList<string> attsSince = (IList<string>)GetJSONQuery("atts_since");
						if (attsSince != null)
						{
							string ancestorId = db.FindCommonAncestorOf(rev, attsSince);
							if (ancestorId != null)
							{
								int generation = RevisionInternal.GenerationFromRevID(ancestorId);
								db.StubOutAttachmentsIn(rev, generation + 1);
							}
						}
					}
					if (rev == null)
					{
						return new Status(Status.NotFound);
					}
					if (CacheWithEtag(rev.GetRevId()))
					{
						return new Status(Status.NotModified);
					}
					// set ETag and check conditional GET
					connection.SetResponseBody(rev.GetBody());
				}
				else
				{
					IList<IDictionary<string, object>> result = null;
					if (openRevsParam.Equals("all"))
					{
						// Get all conflicting revisions:
						RevisionList allRevs = db.GetAllRevisionsOfDocumentID(docID, true);
						result = new AList<IDictionary<string, object>>(allRevs.Count);
						foreach (RevisionInternal rev in allRevs)
						{
							try
							{
								db.LoadRevisionBody(rev, options);
							}
							catch (CouchbaseLiteException e)
							{
								if (e.GetCBLStatus().GetCode() != Status.InternalServerError)
								{
									IDictionary<string, object> dict = new Dictionary<string, object>();
									dict.Put("missing", rev.GetRevId());
									result.AddItem(dict);
								}
								else
								{
									throw;
								}
							}
							IDictionary<string, object> dict_1 = new Dictionary<string, object>();
							dict_1.Put("ok", rev.GetProperties());
							result.AddItem(dict_1);
						}
					}
					else
					{
						// ?open_revs=[...] returns an array of revisions of the document:
						IList<string> openRevs = (IList<string>)GetJSONQuery("open_revs");
						if (openRevs == null)
						{
							return new Status(Status.BadRequest);
						}
						result = new AList<IDictionary<string, object>>(openRevs.Count);
						foreach (string revID in openRevs)
						{
							RevisionInternal rev = db.GetDocumentWithIDAndRev(docID, revID, options);
							if (rev != null)
							{
								IDictionary<string, object> dict = new Dictionary<string, object>();
								dict.Put("ok", rev.GetProperties());
								result.AddItem(dict);
							}
							else
							{
								IDictionary<string, object> dict = new Dictionary<string, object>();
								dict.Put("missing", revID);
								result.AddItem(dict);
							}
						}
					}
					string acceptMultipart = GetMultipartRequestType();
					if (acceptMultipart != null)
					{
						//FIXME figure out support for multipart
						throw new NotSupportedException();
					}
					else
					{
						connection.SetResponseBody(new Body(result));
					}
				}
				return new Status(Status.Ok);
			}
			catch (CouchbaseLiteException e)
			{
				return e.GetCBLStatus();
			}
		}

		public virtual Status Do_GET_Attachment(Database _db, string docID, string _attachmentName
			)
		{
			try
			{
				// http://wiki.apache.org/couchdb/HTTP_Document_API#GET
				EnumSet<Database.TDContentOptions> options = GetContentOptions();
				options.AddItem(Database.TDContentOptions.TDNoBody);
				string revID = GetQuery("rev");
				// often null
				RevisionInternal rev = db.GetDocumentWithIDAndRev(docID, revID, options);
				if (rev == null)
				{
					return new Status(Status.NotFound);
				}
				if (CacheWithEtag(rev.GetRevId()))
				{
					return new Status(Status.NotModified);
				}
				// set ETag and check conditional GET
				string type = null;
				string acceptEncoding = connection.GetRequestProperty("accept-encoding");
				Attachment contents = db.GetAttachmentForSequence(rev.GetSequence(), _attachmentName
					);
				if (contents == null)
				{
					return new Status(Status.NotFound);
				}
				type = contents.GetContentType();
				if (type != null)
				{
					connection.GetResHeader().Add("Content-Type", type);
				}
				if (acceptEncoding != null && acceptEncoding.Contains("gzip") && contents.GetGZipped
					())
				{
					connection.GetResHeader().Add("Content-Encoding", "gzip");
				}
				connection.SetResponseInputStream(contents.GetContent());
				return new Status(Status.Ok);
			}
			catch (CouchbaseLiteException e)
			{
				return e.GetCBLStatus();
			}
		}

		/// <summary>NOTE this departs from the iOS version, returning revision, passing status back by reference
		/// 	</summary>
		public virtual RevisionInternal Update(Database _db, string docID, Body body, bool
			 deleting, bool allowConflict, Status outStatus)
		{
			bool isLocalDoc = docID != null && docID.StartsWith(("_local"));
			string prevRevID = null;
			if (!deleting)
			{
				bool deletingBoolean = (bool)body.GetPropertyForKey("_deleted");
				deleting = (deletingBoolean != null && deletingBoolean);
				if (docID == null)
				{
					if (isLocalDoc)
					{
						outStatus.SetCode(Status.MethodNotAllowed);
						return null;
					}
					// POST's doc ID may come from the _id field of the JSON body, else generate a random one.
					docID = (string)body.GetPropertyForKey("_id");
					if (docID == null)
					{
						if (deleting)
						{
							outStatus.SetCode(Status.BadRequest);
							return null;
						}
						docID = Database.GenerateDocumentId();
					}
				}
				// PUT's revision ID comes from the JSON body.
				prevRevID = (string)body.GetPropertyForKey("_rev");
			}
			else
			{
				// DELETE's revision ID comes from the ?rev= query param
				prevRevID = GetQuery("rev");
			}
			// A backup source of revision ID is an If-Match header:
			if (prevRevID == null)
			{
				prevRevID = GetRevIDFromIfMatchHeader();
			}
			RevisionInternal rev = new RevisionInternal(docID, null, deleting, db);
			rev.SetBody(body);
			RevisionInternal result = null;
			try
			{
				if (isLocalDoc)
				{
					result = _db.PutLocalRevision(rev, prevRevID);
				}
				else
				{
					result = _db.PutRevision(rev, prevRevID, allowConflict);
				}
				if (deleting)
				{
					outStatus.SetCode(Status.Ok);
				}
				else
				{
					outStatus.SetCode(Status.Created);
				}
			}
			catch (CouchbaseLiteException e)
			{
				Sharpen.Runtime.PrintStackTrace(e);
				Log.E(Database.Tag, e.ToString());
				outStatus.SetCode(e.GetCBLStatus().GetCode());
			}
			return result;
		}

		public virtual Status Update(Database _db, string docID, IDictionary<string, object
			> bodyDict, bool deleting)
		{
			Body body = new Body(bodyDict);
			Status status = new Status();
			if (docID != null && docID.IsEmpty() == false)
			{
				// On PUT/DELETE, get revision ID from either ?rev= query or doc body:
				string revParam = GetQuery("rev");
				if (revParam != null && bodyDict != null && bodyDict.Count > 0)
				{
					string revProp = (string)bodyDict.Get("_rev");
					if (revProp == null)
					{
						// No _rev property in body, so use ?rev= query param instead:
						bodyDict.Put("_rev", revParam);
						body = new Body(bodyDict);
					}
					else
					{
						if (!revParam.Equals(revProp))
						{
							throw new ArgumentException("Mismatch between _rev and rev");
						}
					}
				}
			}
			RevisionInternal rev = Update(_db, docID, body, deleting, false, status);
			if (status.IsSuccessful())
			{
				CacheWithEtag(rev.GetRevId());
				// set ETag
				if (!deleting)
				{
					Uri url = connection.GetURL();
					string urlString = url.ToExternalForm();
					if (docID != null)
					{
						urlString += "/" + rev.GetDocId();
						try
						{
							url = new Uri(urlString);
						}
						catch (UriFormatException e)
						{
							Log.W("Malformed URL", e);
						}
					}
					SetResponseLocation(url);
				}
				IDictionary<string, object> result = new Dictionary<string, object>();
				result.Put("ok", true);
				result.Put("id", rev.GetDocId());
				result.Put("rev", rev.GetRevId());
				connection.SetResponseBody(new Body(result));
			}
			return status;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_PUT_Document(Database _db, string docID, string _attachmentName
			)
		{
			Status status = new Status(Status.Created);
			IDictionary<string, object> bodyDict = GetBodyAsDictionary();
			if (bodyDict == null)
			{
				throw new CouchbaseLiteException(Status.BadRequest);
			}
			if (GetQuery("new_edits") == null || (GetQuery("new_edits") != null && (System.Convert.ToBoolean
				(GetQuery("new_edits")))))
			{
				// Regular PUT
				status = Update(_db, docID, bodyDict, false);
			}
			else
			{
				// PUT with new_edits=false -- forcible insertion of existing revision:
				Body body = new Body(bodyDict);
				RevisionInternal rev = new RevisionInternal(body, _db);
				if (rev.GetRevId() == null || rev.GetDocId() == null || !rev.GetDocId().Equals(docID
					))
				{
					throw new CouchbaseLiteException(Status.BadRequest);
				}
				IList<string> history = Database.ParseCouchDBRevisionHistory(body.GetProperties()
					);
				db.ForceInsert(rev, history, null);
			}
			return status;
		}

		public virtual Status Do_DELETE_Document(Database _db, string docID, string _attachmentName
			)
		{
			return Update(_db, docID, null, true);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status UpdateAttachment(string attachment, string docID, InputStream
			 contentStream)
		{
			Status status = new Status(Status.Ok);
			string revID = GetQuery("rev");
			if (revID == null)
			{
				revID = GetRevIDFromIfMatchHeader();
			}
			RevisionInternal rev = db.UpdateAttachment(attachment, contentStream, connection.
				GetRequestProperty("content-type"), docID, revID);
			IDictionary<string, object> resultDict = new Dictionary<string, object>();
			resultDict.Put("ok", true);
			resultDict.Put("id", rev.GetDocId());
			resultDict.Put("rev", rev.GetRevId());
			connection.SetResponseBody(new Body(resultDict));
			CacheWithEtag(rev.GetRevId());
			if (contentStream != null)
			{
				SetResponseLocation(connection.GetURL());
			}
			return status;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_PUT_Attachment(Database _db, string docID, string _attachmentName
			)
		{
			return UpdateAttachment(_attachmentName, docID, connection.GetRequestInputStream(
				));
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_DELETE_Attachment(Database _db, string docID, string _attachmentName
			)
		{
			return UpdateAttachment(_attachmentName, docID, null);
		}

		/// <summary>VIEW QUERIES:</summary>
		public virtual View CompileView(string viewName, IDictionary<string, object> viewProps
			)
		{
			string language = (string)viewProps.Get("language");
			if (language == null)
			{
				language = "javascript";
			}
			string mapSource = (string)viewProps.Get("map");
			if (mapSource == null)
			{
				return null;
			}
			Mapper mapBlock = View.GetCompiler().CompileMap(mapSource, language);
			if (mapBlock == null)
			{
				Log.W(Database.Tag, string.Format("View %s has unknown map function: %s", viewName
					, mapSource));
				return null;
			}
			string reduceSource = (string)viewProps.Get("reduce");
			Reducer reduceBlock = null;
			if (reduceSource != null)
			{
				reduceBlock = View.GetCompiler().CompileReduce(reduceSource, language);
				if (reduceBlock == null)
				{
					Log.W(Database.Tag, string.Format("View %s has unknown reduce function: %s", viewName
						, reduceBlock));
					return null;
				}
			}
			View view = db.GetView(viewName);
			view.SetMapAndReduce(mapBlock, reduceBlock, "1");
			string collation = (string)viewProps.Get("collation");
			if ("raw".Equals(collation))
			{
				view.SetCollation(View.TDViewCollation.TDViewCollationRaw);
			}
			return view;
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status QueryDesignDoc(string designDoc, string viewName, IList<object
			> keys)
		{
			string tdViewName = string.Format("%s/%s", designDoc, viewName);
			View view = db.GetExistingView(tdViewName);
			if (view == null || view.GetMap() == null)
			{
				// No TouchDB view is defined, or it hasn't had a map block assigned;
				// see if there's a CouchDB view definition we can compile:
				RevisionInternal rev = db.GetDocumentWithIDAndRev(string.Format("_design/%s", designDoc
					), null, EnumSet.NoneOf<Database.TDContentOptions>());
				if (rev == null)
				{
					return new Status(Status.NotFound);
				}
				IDictionary<string, object> views = (IDictionary<string, object>)rev.GetProperties
					().Get("views");
				IDictionary<string, object> viewProps = (IDictionary<string, object>)views.Get(viewName
					);
				if (viewProps == null)
				{
					return new Status(Status.NotFound);
				}
				// If there is a CouchDB view, see if it can be compiled from source:
				view = CompileView(tdViewName, viewProps);
				if (view == null)
				{
					return new Status(Status.InternalServerError);
				}
			}
			QueryOptions options = new QueryOptions();
			//if the view contains a reduce block, it should default to reduce=true
			if (view.GetReduce() != null)
			{
				options.SetReduce(true);
			}
			if (!GetQueryOptions(options))
			{
				return new Status(Status.BadRequest);
			}
			if (keys != null)
			{
				options.SetKeys(keys);
			}
			view.UpdateIndex();
			long lastSequenceIndexed = view.GetLastSequenceIndexed();
			// Check for conditional GET and set response Etag header:
			if (keys == null)
			{
				long eTag = options.IsIncludeDocs() ? db.GetLastSequenceNumber() : lastSequenceIndexed;
				if (CacheWithEtag(string.Format("%d", eTag)))
				{
					return new Status(Status.NotModified);
				}
			}
			// convert from QueryRow -> Map
			IList<QueryRow> queryRows = view.QueryWithOptions(options);
			IList<IDictionary<string, object>> rows = new AList<IDictionary<string, object>>(
				);
			foreach (QueryRow queryRow in queryRows)
			{
				rows.AddItem(queryRow.AsJSONDictionary());
			}
			IDictionary<string, object> responseBody = new Dictionary<string, object>();
			responseBody.Put("rows", rows);
			responseBody.Put("total_rows", rows.Count);
			responseBody.Put("offset", options.GetSkip());
			if (options.IsUpdateSeq())
			{
				responseBody.Put("update_seq", lastSequenceIndexed);
			}
			connection.SetResponseBody(new Body(responseBody));
			return new Status(Status.Ok);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_GET_DesignDocument(Database _db, string designDocID, string
			 viewName)
		{
			return QueryDesignDoc(designDocID, viewName, null);
		}

		/// <exception cref="Couchbase.Lite.CouchbaseLiteException"></exception>
		public virtual Status Do_POST_DesignDocument(Database _db, string designDocID, string
			 viewName)
		{
			IDictionary<string, object> bodyDict = GetBodyAsDictionary();
			if (bodyDict == null)
			{
				return new Status(Status.BadRequest);
			}
			IList<object> keys = (IList<object>)bodyDict.Get("keys");
			return QueryDesignDoc(designDocID, viewName, keys);
		}

		public override string ToString()
		{
			string url = "Unknown";
			if (connection != null && connection.GetURL() != null)
			{
				url = connection.GetURL().ToExternalForm();
			}
			return string.Format("Router [%s]", url);
		}
	}
}
