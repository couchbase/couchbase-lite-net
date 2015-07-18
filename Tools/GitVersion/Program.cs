//
//  Program.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace GitVersion
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            if(args.Length < 2) {
                Console.Error.WriteLine("Not enough arguments!  Must specify an input directory and output file.");
                return;
            }
            
            string hash = "No git information";
            DirectoryInfo gitFolder = FindGitFolder(new DirectoryInfo(args[0]));
            if(gitFolder != null) {
                var headPath = Path.Combine(gitFolder.FullName, "HEAD");
                var headContent = File.ReadAllText(headPath).TrimEnd();
                if(!headContent.StartsWith("ref:")) {
                    hash = String.Format("detached HEAD: {0}", headContent.Substring(0, 7));
                } else {
                    var sb = new StringBuilder();
                    foreach(var component in headContent.Split('/').Skip(2)) {
                        if(sb.Length != 0) {
                            sb.Append('/');
                        }
                        
                        sb.Append(component);
                    }
                    
                    headPath = Path.Combine(gitFolder.FullName, "logs", "HEAD");
                    var logLine = LastFullLineOfFile(headPath);
                    string possibleHash = HashFromLogLine(logLine);
                    hash = String.Format("{0}: {1}", sb, possibleHash ?? "Unknown hash");
                }
            }
            
            File.WriteAllText(args[1], hash);
        }
        
        private static DirectoryInfo FindGitFolder(DirectoryInfo startingPath)
        {
            if(startingPath.FullName == "/") {
                return null;
            }
            
            foreach(var dir in startingPath.EnumerateDirectories(".git")) {
                return dir;
            }
            
            return FindGitFolder(startingPath.Parent);
        }
        
        private static string LastFullLineOfFile(string path)
        {
            using(var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                int nextByte = 0;
                bool foundNewline = false;
                bool foundText = false;
                fs.Seek(-1, SeekOrigin.End);
                do {
                    nextByte = fs.ReadByte();
                    foundNewline = nextByte == '\n';
                    foundText = foundText || !foundNewline;
                    fs.Seek(-2, SeekOrigin.Current);
                } while(fs.Position > 0 && (!foundNewline || !foundText));
                
                fs.Seek(2, SeekOrigin.Current);
                var length = fs.Length - fs.Position;
                var buffer = new byte[length];
                fs.Read(buffer, 0, buffer.Length);
                
                return Encoding.UTF8.GetString(buffer).TrimEnd('\r', '\n');
            }
        }
        
        private static string HashFromLogLine(string line)
        {
            var split = line.Split(' ');
            if(split.Length < 2) {
                Console.WriteLine("Failed to parse git log");
                return null;
            }
            
            return split[1].Substring(0, 7);
        }
    }
}
