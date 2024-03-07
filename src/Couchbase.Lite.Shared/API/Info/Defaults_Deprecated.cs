//
// Copyright (c) 2024-present Couchbase, Inc All rights reserved.
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

using Couchbase.Lite.Sync;
using Couchbase.Lite.Logging;

using System;

namespace Couchbase.Lite.Info;

public static partial class Constants
{
    /// <summary>
	/// Default value for <see cref="LogFileConfiguration.UsePlaintext" /> (false)
	/// Plaintext is not used, and instead binary encoding is used in log files
	/// </summary>
    [Obsolete("Use Constants.UsePlantext instead")]
    public static readonly bool DefaultLogFileUsePlainText = DefaultLogFileUsePlaintext;

    /// <summary>
	/// Default value for <see cref="ReplicatorConfiguration.MaxAttemptsWaitTime" /> (TimeSpan.FromSeconds(300))
	/// Max wait time between retry attempts in seconds
	/// </summary>
    [Obsolete("Use Constants.DefaultReplicatorMaxAttemptsWaitTime (attempt -> attemps) instead")]
    public static readonly TimeSpan DefaultReplicatorMaxAttemptWaitTime = DefaultReplicatorMaxAttemptsWaitTime;
}
