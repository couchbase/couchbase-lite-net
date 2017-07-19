// Main.cs: Touch.Unit Simple Server
//
// Authors:
//	Sebastien Pouliot  <sebastien@xamarin.com>
//
// Copyright 2011-2012 Xamarin Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//   http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

using Mono.Options;

public static class BinaryReaderExt
{
    public static string ReadLine(this BinaryReader reader, out bool more)
    {
        var result = new StringBuilder();
        try {
            char character;
            while((character = reader.ReadChar()) != '\n') {
                if(character != '\r' && character != '\n') {
                    result.Append(character);
                }
            }

            more = true;
            return result.ToString();
        } catch(EndOfStreamException) {
            more = false;
            return result.ToString();
        }
    }
}

// a simple, blocking (i.e. one device/app at the time), listener
class SimpleListener {
	TcpListener server;
	ManualResetEvent stopped = new ManualResetEvent (false);
	ManualResetEvent connected = new ManualResetEvent (false);
	
	IPAddress Address { get; set; }
	int Port { get; set; }
	bool AutoExit { get; set; }

	public bool WaitForConnection (TimeSpan ts)
	{
		return connected.WaitOne (ts);
	}

	public void Cancel ()
	{
		try {
			// wait a second just in case more data arrives.
			if (!stopped.WaitOne (TimeSpan.FromSeconds (1))) 
				server.Stop ();
		} catch {
			// We might have stopped already, so just swallow any exceptions.
		}
	}

	public void Initialize ()
	{
		server = new TcpListener (Address, Port);
		server.Start ();

		if (Port == 0)
			Port = ((IPEndPoint) server.LocalEndpoint).Port;

		Console.WriteLine ("Touch.Unit Simple Server listening on: {0}:{1}", Address, Port);
	}
	
	public int Start ()
	{
		bool processed;
    bool success;

		try {
			
			do {
				using (TcpClient client = server.AcceptTcpClient ()) {
					processed = Processing (client, out success);
				}
			} while (!AutoExit || !processed);
		}
		catch (Exception e) {
			Console.WriteLine ("[{0}] : {1}", DateTime.Now, e);
			return 1;
		}
		finally {
			try {
				server.Stop ();
			} finally {
				stopped.Set ();
			}
		}
		
		return success ? 0 : 1;
	}

	public bool Processing (TcpClient client, out bool success)
	{
		string remote = client.Client.RemoteEndPoint.ToString ();
		Console.WriteLine ("Connection from {0}", remote);
		connected.Set ();

		// a few extra bits of data only available from this side
		string header = String.Format ("[Local Date/Time:\t{1}]{0}[Remote Address:\t{2}]{0}", 
				Environment.NewLine, DateTime.Now, remote);
			Console.Out.Write (header);
			Console.Out.Flush ();
			// now simply copy what we receive
			int total = 0;
			NetworkStream stream = client.GetStream ();
      string line;
      string lastLine = null;
      using(var br = new BinaryReader(stream, Encoding.UTF8)) {
          var more = true;
          do {
              line = br.ReadLine(out more);
              if(line.StartsWith("Tests run")) {
                  lastLine = line;
                  more = false;
              }

              Console.Out.WriteLine(line);
              Console.Out.Flush();
              total += line.Length;
          } while(more);
      }

			if (total < 16) {
				// This wasn't a test run, but a connection from the app (on device) to find
				// the ip address we're reachable on.
        success = false;
				return false;
			}

    success = AllPassed(lastLine);
		
		return true;
	}

  public static bool AllPassed(string lastLine)
  { 
      if(lastLine == null) {
          return false;
      }

      var splitLine = lastLine.Split();
      var totalTests = Int32.Parse(splitLine[2]);
      var passed = Int32.Parse(splitLine[4]);
      return totalTests == passed;
  }

	static void ShowHelp (OptionSet os)
	{
		Console.WriteLine ("Usage: mono Touch.Server.exe [options]");
		os.WriteOptionDescriptions (Console.Out);
	}

	public static int Main (string[] args)
	{ 
		Console.WriteLine ("Touch.Unit Simple Server");
		Console.WriteLine ("Copyright 2011, Xamarin Inc. All rights reserved.");
        
        bool help = false;
        var port = 12345;
        var os = new OptionSet () {
			{ "h|?|help", "Display help", v => help = true },
            { "p|port=", "TCP port to listen (default: 12345)", v => port = Int32.Parse(v) }
        };
		
        os.Parse(args);
        if(help) {
            ShowHelp(os);
            return 0;
        }
        
		try {
			var listener = new SimpleListener ();
			
			listener.Address = IPAddress.Any;
		    listener.Port = port;
			listener.AutoExit = true;
			listener.Initialize ();
			
			var lastErrorDataReceived = new AutoResetEvent (true);
			var lastOutDataReceived = new AutoResetEvent (true);
			
      var result = listener.Start ();
			lastErrorDataReceived.WaitOne (2000);
			lastOutDataReceived.WaitOne (2000);
			return result;
		} catch (Exception ex) {
			Console.WriteLine (ex);
			return 1;
		}
	}   
}
