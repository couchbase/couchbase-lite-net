
//
//  Program.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
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
using Couchbase.Lite;
using Couchbase.Lite.Listener;
using Couchbase.Lite.Listener.Tcp;
using Couchbase.Lite.Store;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Lite.Security;
using Mono.Options;
using System.Linq;
using Couchbase.Lite.Auth;
using System.Threading;

namespace Listener
{
    class MainClass
    {
        private const int DefaultPort = 59840;
        private static readonly ConsoleColor DefaultColor = Console.ForegroundColor;
        private static readonly ColorConsoleLogger Logger = new ColorConsoleLogger(); 

        public static void Main(string[] args)
        {
            Couchbase.Lite.Util.Log.SetLogger(Logger);
            var alternateDir = default(string);
            var pullUrl = default(Uri);
            var pushUrl = default(Uri);
            var portToUse = DefaultPort; 
            var readOnly = false;
            var requiresAuth = false;
            var createTarget = false;
            var continuous = false;
            var userName = default(string);
            var password = default(string);
            var useSSL = false;
            var sslCertPath = default(string);
            var sslCertPass = default(string);
            var storageType = "SQLite";
            var passwordMap = new Dictionary<string, string>();
            var showHelp = false;

            var options = new OptionSet {
                { "dir=", "Specifies an alternate directory to store databases in", v => alternateDir = v },
                { "port=", "Specifies the port to listen on (default 59840)", v => portToUse = Int32.Parse(v) },
                { "readonly", "Enables readonly mode", v => readOnly = v != null },
                { "auth", "Set listener to require HTTP auth", v => requiresAuth = v != null },
                { "pull=", "Specifies a remote database to pull from", v => pullUrl = new Uri(v) },
                { "push=", "Specifies a remote database to push to", v => pushUrl = new Uri(v) },
                { "create-target", "Creates the replication target database, if pull", v => createTarget = v != null },
                { "continuous", "Specifies continuous replication", v => continuous = v != null },
                { "user=", "Specifies a username for connecting to a remote database", v => userName = v },
                { "password=", "Specifies a password for connection to a remote database", v => password = v },
                { "ssl", "Serve over SSL", v => useSSL = v != null },
                { "sslcert=", "Path to the SSL certificate to use", v => sslCertPath = v },
                { "sslpass=", "Password for the SSL certificate", v => sslCertPass = v },
                { "storage=", "Set default storage engine ('SQLite' (default) or 'ForestDB')", v => storageType = v },
                { "dbpassword=", "Register password to open a database (name=password)", v => RegisterPassword(passwordMap, v) },
                { "help|h|?", "Show this help message", v => showHelp = v != null }
            };

            try {
                var remaining = options.Parse(args);
                foreach(var arg in remaining) {
                    Logger.W("Listener", "Unrecognized argument {0}, ignoring...", arg);
                }
            } catch(Exception e) {
                Logger.E("Listener", "Error parsing arguments", e);
                ShowHelp(options);
                Exit();
                return;
            }

            if (showHelp) {
                ShowHelp(options);
                Exit();
                return;
            }

            Couchbase.Lite.Storage.ForestDB.Plugin.Register();
            Couchbase.Lite.Storage.SQLCipher.Plugin.Register();

            var manager = alternateDir != null ? new Manager(new DirectoryInfo(alternateDir), ManagerOptions.Default)
                : Manager.SharedInstance;
            manager.StorageType = storageType;

            if (passwordMap.Count > 0) {
                
                foreach (var entry in passwordMap) {
                    manager.RegisterEncryptionKey(entry.Key, new SymmetricKey(entry.Value));
                }
            }
 
            var tcpOptions = CouchbaseLiteTcpOptions.Default | CouchbaseLiteTcpOptions.AllowBasicAuth;
            var sslCert = default(X509Certificate2);
            if (useSSL) {
                tcpOptions |= CouchbaseLiteTcpOptions.UseTLS;
                if (sslCertPath != null) {
                    if (!File.Exists(sslCertPath)) {
                        Logger.E("Listener", "No file exists at given path for SSL cert ({0})", sslCertPath);
                        Exit();
                        return;
                    }

                    try {
                        sslCert = new X509Certificate2(sslCertPath, sslCertPass);
                    } catch(Exception e) {
                        Logger.E("Listener", "Error reading SSL cert ({0}), {1}", sslCertPath, e);
                        Exit();
                        return;
                    }
                }
            }

            var replicator = default(Replication);
            if (pushUrl != null || pullUrl != null) {
                replicator = SetupReplication(manager, continuous, createTarget,
                    pushUrl ?? pullUrl, pullUrl != null, userName, password);
                if (replicator == null) {
                    Exit();
                    return;
                }
            }

            CouchbaseLiteServiceListener listener = new CouchbaseLiteTcpListener(manager, (ushort)portToUse, tcpOptions, sslCert);
            listener.ReadOnly = readOnly;
            if (requiresAuth) {
                var random = new Random();
                var generatedPassword = random.Next().ToString();
                listener.SetPasswords(new Dictionary<string, string> { { "cbl", generatedPassword } });
                Logger.I("Listener", "Auth required: user='cbl', password='{0}'", generatedPassword);
            }

            listener.Start();
            Logger.I("Listener", "LISTENING...");
            var wait = new ManualResetEventSlim();
            Console.WriteLine("Press Ctrl+C to end the process");
            Console.CancelKeyPress += (sender, e) => wait.Set();
            wait.Wait();
            Console.WriteLine("Shutting down now");
            wait.Dispose();

            if (replicator != null) {
                replicator.Stop();
                Thread.Sleep(5000);
            }
        }

        private static Replication SetupReplication(Manager manager, bool continuous, bool createTarget,
            Uri remoteUri, bool isPull, string user, string password)
        {
            if (remoteUri == null) {
                return null;
            }

            var databaseName = remoteUri.Segments.Last();
            var authenticator = default(IAuthenticator);
            if (user != null && password != null) {
                Logger.I("Listener", "Setting session credentials for user '{0}'", user);
                authenticator = AuthenticatorFactory.CreateBasicAuthenticator(user, password);
            }

            if (isPull) {
                Logger.I("Listener", "Pulling from <{0}> --> {1}", remoteUri, databaseName);
            } else {
                Logger.I("Listener", "Pushing {0} --> <{1}>", databaseName, remoteUri);
            }

            var db = manager.GetExistingDatabase(databaseName);
            if (isPull && db == null) {
                db = manager.GetDatabase(databaseName);
            }

            if (db == null) {
                Logger.E("Listener", "Couldn't open database {0}", databaseName);
                return null;
            }

            var repl = isPull ? db.CreatePullReplication(remoteUri) : db.CreatePushReplication(remoteUri);
            repl.Continuous = continuous;
            repl.CreateTarget = createTarget;
            repl.Authenticator = authenticator;
            repl.Changed += (sender, e) => 
            {
                Logger.I("Listener", "*** Replicator status changed ({0} {1}/{2}) ***", e.Status, e.CompletedChangesCount,
                    e.ChangesCount);
                if(e.LastError != null) {
                    Logger.W("Listener", "*** Replicator reported error ***", e);
                } else if(e.Status == ReplicationStatus.Stopped) {
                    Logger.I("Listener", "*** Replicator finished ***");
                }
            };

            repl.Start();
            return repl;
        }

        private static void Exit()
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey(true);
        }

        private static void ShowHelp(OptionSet options)
        {
            Console.WriteLine("Usage: Listener.exe [options]");
            options.WriteOptionDescriptions(Console.Out);
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
