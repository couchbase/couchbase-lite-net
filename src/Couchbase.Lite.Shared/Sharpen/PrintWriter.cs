//
// PrintWriter.cs
//
// Author:
//	Zachary Gramana  <zack@xamarin.com>
//
// Copyright (c) 2013, 2014 Xamarin Inc (http://www.xamarin.com)
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
/**
* Original iOS version by Jens Alfke
* Ported to Android by Marty Schoch, Traun Leyden
*
* Copyright (c) 2012, 2013, 2014 Couchbase, Inc. All rights reserved.
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
namespace Sharpen
{
	using System;
	using System.IO;
	using System.Text;

	internal class PrintWriter : TextWriter
	{
		TextWriter writer;
		
		public PrintWriter (FilePath path)
		{
			writer = new StreamWriter (path);
		}

		public PrintWriter (TextWriter other)
		{
			writer = other;
		}

		public override Encoding Encoding {
			get { return writer.Encoding; }
		}
		
		public override void Close ()
		{
			writer.Close ();
		}
	
		public override void Flush ()
		{
			writer.Flush ();
		}
	
		public override System.IFormatProvider FormatProvider {
			get {
				return writer.FormatProvider;
			}
		}
	
		public override string NewLine {
			get {
				return writer.NewLine;
			}
			set {
				writer.NewLine = value;
			}
		}
	
		public override void Write (char[] buffer, int index, int count)
		{
			writer.Write (buffer, index, count);
		}
	
		public override void Write (char[] buffer)
		{
			writer.Write (buffer);
		}
	
		public override void Write (string format, object arg0, object arg1, object arg2)
		{
			writer.Write (format, arg0, arg1, arg2);
		}
	
		public override void Write (string format, object arg0, object arg1)
		{
			writer.Write (format, arg0, arg1);
		}
	
		public override void Write (string format, object arg0)
		{
			writer.Write (format, arg0);
		}
	
		public override void Write (string format, params object[] arg)
		{
			writer.Write (format, arg);
		}
	
		public override void WriteLine (char[] buffer, int index, int count)
		{
			writer.WriteLine (buffer, index, count);
		}
	
		public override void WriteLine (char[] buffer)
		{
			writer.WriteLine (buffer);
		}
	
		public override void WriteLine (string format, object arg0, object arg1, object arg2)
		{
			writer.WriteLine (format, arg0, arg1, arg2);
		}
	
		public override void WriteLine (string format, object arg0, object arg1)
		{
			writer.WriteLine (format, arg0, arg1);
		}
	
		public override void WriteLine (string format, object arg0)
		{
			writer.WriteLine (format, arg0);
		}
	
		public override void WriteLine (string format, params object[] arg)
		{
			writer.WriteLine (format, arg);
		}
	
		public override void WriteLine (ulong value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (uint value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (string value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (float value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (object value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (long value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (int value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (double value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (decimal value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (char value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine (bool value)
		{
			writer.WriteLine (value);
		}
	
		public override void WriteLine ()
		{
			writer.WriteLine ();
		}
	
		public override void Write (bool value)
		{
			writer.Write (value);
		}
	
		public override void Write (char value)
		{
			writer.Write (value);
		}
	
		public override void Write (decimal value)
		{
			writer.Write (value);
		}
	
		public override void Write (double value)
		{
			writer.Write (value);
		}
	
		public override void Write (int value)
		{
			writer.Write (value);
		}
	
		public override void Write (long value)
		{
			writer.Write (value);
		}
	
		public override void Write (object value)
		{
			writer.Write (value);
		}
	
		public override void Write (float value)
		{
			writer.Write (value);
		}
	
		public override void Write (string value)
		{
			writer.Write (value);
		}
	
		public override void Write (uint value)
		{
			writer.Write (value);
		}
	
		public override void Write (ulong value)
		{
			writer.Write (value);
		}
	}
}
