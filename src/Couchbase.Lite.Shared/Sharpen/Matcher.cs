//
// Matcher.cs
//
// Author:
//     Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2014 Xamarin Inc
// Copyright (c) 2014 .NET Foundation
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//
//
// Copyright (c) 2014 Couchbase, Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file
// except in compliance with the License. You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the
// License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND,
// either express or implied. See the License for the specific language governing permissions
// and limitations under the License.
//

namespace Sharpen
{
	using System;
	using System.Text.RegularExpressions;

	internal class Matcher
	{
		private int current;
		private MatchCollection matches;
		private Regex regex;
		private string str;

		internal Matcher (Regex regex, string str)
		{
			this.regex = regex;
			this.str = str;
		}

		public int End ()
		{
			if ((matches == null) || (current >= matches.Count)) {
				throw new InvalidOperationException ();
			}
			return (matches[current].Index + matches[current].Length);
		}

		public bool Find ()
		{
			if (matches == null) {
				matches = regex.Matches (str);
				current = 0;
			}
			return (current < matches.Count);
		}

		public bool Find (int index)
		{
			matches = regex.Matches (str, index);
			current = 0;
			return (matches.Count > 0);
		}

		public string Group (int n)
		{
			if ((matches == null) || (current >= matches.Count)) {
				throw new InvalidOperationException ();
			}
			Group grp = matches[current].Groups[n];
			return grp.Success ? grp.Value : null;
		}

		public bool Matches ()
		{
			matches = null;
			return Find ();
		}

		public string ReplaceFirst (string txt)
		{
			return regex.Replace (str, txt, 1);
		}

		public Matcher Reset (CharSequence str)
		{
			return Reset (str.ToString ());
		}

		public Matcher Reset (string str)
		{
			matches = null;
			this.str = str;
			return this;
		}

		public int Start ()
		{
			if ((matches == null) || (current >= matches.Count)) {
				throw new InvalidOperationException ();
			}
			return matches[current].Index;
		}
	}
}
