// 
//  IMockConnectionErrorLogic.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
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

#if COUCHBASE_ENTERPRISE
using System;
using System.Collections.Generic;
using System.Text;

using Couchbase.Lite.P2P;

namespace Couchbase.Lite
{
    [Flags]
    public enum MockConnectionLifecycleLocation
    {
        Connect = 1 << 0,
        Send = 1 << 1,
        Receive = 1 << 2,
        Close = 1 << 3
    }

    public interface IMockConnectionErrorLogic
    {
        bool ShouldClose(MockConnectionLifecycleLocation location);

        MessagingException CreateException();
    }
}
#endif