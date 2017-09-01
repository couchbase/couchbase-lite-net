// 
// AndroidRuntimePlatform.cs
// 
// Author:
//     Jim Borden  <jim.borden@couchbase.com>
// 
// Copyright (c) 2017 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using System;
using Android.OS;
using Couchbase.Lite.DI;

namespace Couchbase.Lite.Support
{
	internal sealed class AndroidRuntimePlatform : IRuntimePlatform
	{
		public string OSDescription => $"Android {Build.VERSION.Release} [API {(int)Build.VERSION.SdkInt}]";

		public string HardwareName 
		{
			get {
				var manufacturer = Build.Manufacturer;
				var model = Build.Model;
				if (model.StartsWith(manufacturer, StringComparison.InvariantCultureIgnoreCase)) {
					return Capitalize(model);
				} else {
					return $"{Capitalize(manufacturer)} {model}";
				}
			}
		}

		private string Capitalize(string input)
		{
			if(String.IsNullOrWhiteSpace(input) || Char.IsUpper(input[0])) {
				return input;
			}

			return $"{Char.ToUpperInvariant(input[0])}{input.Substring((1))}";
		}
	}
}
