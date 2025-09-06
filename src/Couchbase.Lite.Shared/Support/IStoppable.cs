// 
//  IStoppable.cs
// 
//  Copyright (c) 2020 Couchbase, Inc All rights reserved.
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

namespace Couchbase.Lite;

// The sync version where Stop will remove the active stoppable
// before returning
internal interface IStoppable
{
    void Stop();
}

// Similar to the above, except the active stoppable is removed later
// Sometimes it can fail too, so provide a pollable property
internal interface IAsyncStoppable : IStoppable
{
    bool IsStopped { get;  }
}