//
// LiteCoreBridge.cs
//
// Copyright (c) 2016 Couchbase, Inc All rights reserved.
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
using LiteCore.Interop;

namespace LiteCore
{
    internal unsafe delegate void C4DocumentActionDelegate(C4Document* doc);

    internal unsafe delegate void C4RawDocumentActionDelegate(C4RawDocument* doc);

    internal unsafe delegate bool C4RevisionSelector(C4Document* doc);

    internal static unsafe class LiteCoreBridge
    {
        public static void Check(C4TryLogicDelegate1 block)
        {
            NativeHandler.Create().Execute(block);
        }

        public static void* Check(C4TryLogicDelegate2 block)
        {
            return NativeHandler.Create().Execute(block);
        }

        public static void Check(C4TryLogicDelegate3 block)
        {
            NativeHandler.Create().Execute(block);
        }
    }
}
