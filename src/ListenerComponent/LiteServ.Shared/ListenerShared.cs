//
//  LiteServ.cs
//
//  Authors:
//      Jed Foss-Alfke  <jed.foss-alfke@couchbase.com>
//      Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//
using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Lite;
using Couchbase.Lite.Store;
using Couchbase.Lite.Auth;
using Couchbase.Lite.Listener;
using Couchbase.Lite.Listener.Tcp;
using Newtonsoft.Json;

namespace Listener
{
    public static class ListenerShared
    {
        private const int DefaultPort = 59840;

        public static void StartListener()
        {
            // INTERNAL API
            CouchbaseLiteRouter.InsecureMode = true;
            // INTERNAL API

            var alternateDir = default(string);
            var pullUrl      = default(Uri);
            var pushUrl      = default(Uri);
            var portToUse    = DefaultPort;
            var readOnly     = false;
            var requiresAuth = false;
            var createTarget = false;
            var continuous   = false;
            var userName     = default(string);
            var password     = default(string);
            var useSSL       = false;
            var sslCertPath  = default(string);
            var sslCertPass  = default(string);
            var storageType  = "SQLite";
            var passwordMap  = new Dictionary<string, string>();
            var revsLimit    = 0;

            View.Compiler = new JSViewCompiler();
            Database.FilterCompiler = new JSFilterCompiler();

            string json;

            using (var httpListener = new HttpListener())
            {
                try {
                    httpListener.Prefixes.Add($"http://*:{DefaultPort}/test/");
                    httpListener.Start();
                } catch (HttpListenerException e) {
                    Console.Error.WriteLine("Error setting up parameter listener: {0}", e);
                    return;
                }

                var context = httpListener.GetContext();
                var reader = new StreamReader(context.Request.InputStream);
                json = reader.ReadToEnd();
            }

            Dictionary<string, object> optionsDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            foreach (var option in optionsDict) {
                switch (option.Key) {
                    
                case "dir":           alternateDir = (string)option.Value; break;
                case "port":          portToUse    = Convert.ToInt32(option.Value); break;
                case "readonly":      readOnly     = (bool)option.Value; break;
                case "auth":          requiresAuth = (bool)option.Value; break;
                case "push":          pushUrl      = new Uri((string)option.Value); break;
                case "pull":          pullUrl      = new Uri((string)option.Value); break;
                case "create-target": createTarget = (bool)option.Value; break;
                case "continuous":    continuous   = (bool)option.Value; break;
                case "user":          userName     = (string)option.Value; break;
                case "password":      password     = (string)option.Value; break;
                case "revs_limit":    revsLimit    = Convert.ToInt32(option.Value); break;
                case "ssl":           useSSL       = (bool)option.Value; break;
                case "sslcert":       sslCertPath  = (string)option.Value; break;
                case "sslpass":       sslCertPass  = (string)option.Value; break;
                case "storage":       storageType  = (string)option.Value; break;
                case "dbpassword":
                    RegisterPassword(passwordMap, (string)option.Value);
                    break;

                default:
                    Console.Error.WriteLine("Unrecognized argument {0}, ignoring...", option.Key);
                    break;
                }
            }

            Couchbase.Lite.Storage.ForestDB.Plugin.Register();
            Couchbase.Lite.Storage.SQLCipher.Plugin.Register();

            var manager = alternateDir != null ? new Manager(new DirectoryInfo(alternateDir), ManagerOptions.Default) : Manager.SharedInstance;
            manager.StorageType = storageType;
            if (revsLimit > 0) {
                // Note: Internal API (used for testing)
                manager.DefaultMaxRevTreeDepth = revsLimit;
            }

            if (passwordMap.Count > 0) {

                foreach (var entry in passwordMap) {
#pragma warning disable 618
                    manager.RegisterEncryptionKey(entry.Key, new SymmetricKey(entry.Value));
#pragma warning restore 618
                }
            }

            var tcpOptions = CouchbaseLiteTcpOptions.Default | CouchbaseLiteTcpOptions.AllowBasicAuth;
            var sslCert = default(X509Certificate2);
            if (useSSL) {
                tcpOptions |= CouchbaseLiteTcpOptions.UseTLS;
                if (sslCertPath != null) {
                    if (!File.Exists(sslCertPath)) {
                        Console.Error.WriteLine("No file exists at given path for SSL cert ({0})", sslCertPath);
                        return;
                    }

                    try {
                        sslCert = new X509Certificate2(sslCertPath, sslCertPass);
                    } catch (Exception e) {
                        Console.Error.WriteLine("Error reading SSL cert ({0}), {1}", sslCertPath, e);
                        return;
                    }
                }
            }

            var replicator = default(Replication);
            if (pushUrl != null || pullUrl != null) {
                replicator = SetupReplication(manager, continuous, createTarget, pushUrl ?? pullUrl, pullUrl != null, userName, password);
                if (replicator == null) {
                    return;
                }
            }

            CouchbaseLiteServiceListener listener = new CouchbaseLiteTcpListener(manager, (ushort)portToUse, tcpOptions, sslCert);
            listener.ReadOnly = readOnly;
            if (requiresAuth) {
                var random = new Random();
                var generatedPassword = random.Next().ToString();
                listener.SetPasswords(new Dictionary<string, string> { { "cbl", generatedPassword } });
                Console.WriteLine("Auth required: user='cbl', password='{0}'", generatedPassword);
            }

            listener.Start();
            Console.WriteLine("LISTENING...");
        }

        private static Replication SetupReplication(Manager manager, bool continuous, bool createTarget, Uri remoteUri, bool isPull, string user, string password)
        {
            if (remoteUri == null) {
                return null;
            }

            var databaseName = remoteUri.Segments.Last();
            var authenticator = default(IAuthenticator);
            if (user != null && password != null) {
                Console.WriteLine("Setting session credentials for user '{0}'", user);
                authenticator = AuthenticatorFactory.CreateBasicAuthenticator(user, password);
            }

            if (isPull) {
                Console.WriteLine("Pulling from <{0}> --> {1}", remoteUri, databaseName);
            } else {
                Console.WriteLine("Pushing {0} --> <{1}>", databaseName, remoteUri);
            }

            var db = manager.GetExistingDatabase(databaseName);
            if (isPull && db == null) {
                db = manager.GetDatabase(databaseName);
            }

            if (db == null) {
                Console.Error.WriteLine("Couldn't open database {0}", databaseName);
                return null;
            }

            var repl = isPull ? db.CreatePullReplication(remoteUri) : db.CreatePushReplication(remoteUri);
            repl.Continuous = continuous;
            repl.CreateTarget = createTarget;
            repl.Authenticator = authenticator;
            repl.Changed += (sender, e) =>
            {
                Console.WriteLine("*** Replicator status changed ({0} {1}/{2}) ***", e.Status, e.CompletedChangesCount, e.ChangesCount);
                if (e.LastError != null) {
                    Console.Error.WriteLine("*** Replicator reported error ***", e);
                } else if (e.Status == ReplicationStatus.Stopped) {
                    Console.WriteLine("*** Replicator finished ***");
                }
            };

            repl.Start();
            return repl;
        }

        private static void RegisterPassword(IDictionary<string, string> collection, string unparsed)
        {
            var userAndPass = unparsed.Split('=');
            if (userAndPass.Length != 2) {
                throw new ArgumentException($"Invalid entry for dbpassword ({unparsed}), must be in " +
                    "the format <name>=<password>");
            }

            collection[userAndPass[0]] = userAndPass[1];
        }
    }
}

