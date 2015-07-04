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
                var headRef = File.ReadAllText(headPath).TrimEnd('\r', '\n').Substring(5);
                var refPath = Path.Combine(gitFolder.FullName, headRef);
                var fullHash = File.ReadAllText(refPath).TrimEnd('\r', '\n');
                hash = fullHash.Substring(0, 7);
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
    }
}
