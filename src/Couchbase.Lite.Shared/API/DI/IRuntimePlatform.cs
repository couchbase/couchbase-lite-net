// 
// IRuntimePlatform.cs
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

using JetBrains.Annotations;

namespace Couchbase.Lite.DI
{
    /// <summary>
    /// An interface for getting OS and hardware information from a runtime platform
    /// </summary>
    public interface IRuntimePlatform
	{
        /// <summary>
        /// Gets the operating system name and version (and possibly other info)
        /// </summary>
        [NotNull]
		string OSDescription { get; }

        /// <summary>
        /// Gets the name of the device that is running the program, if possible
        /// </summary>
        [NotNull]
		string HardwareName { get; }
	}
}
