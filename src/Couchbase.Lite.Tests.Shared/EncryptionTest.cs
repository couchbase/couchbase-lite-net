//
//  EncryptionTest.cs
//
//  Author:
//  	Jim Borden  <jim.borden@couchbase.com>
//
//  Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System;
using NUnit.Framework;
using System.Collections.Generic;

namespace Couchbase.Lite
{
    public class EncryptionTest : LiteTestCase
    {

        [Test]
        public void TestUnencryptedDB()
        {
            Database.EnableMockEncryption = true;

            // Create unencrypted DB:
            var seekrit = manager.GetDatabase("seekrit");
            Assert.IsNotNull(seekrit, "Failed to create db");
            CreateDocumentWithProperties(seekrit, new Dictionary<string, object> {
                { "answer", 42 }
            });

            Assert.IsTrue(seekrit.Close());

            manager.RegisterEncryptionKey("wrong", "seekrit");
            var status = new Status(StatusCode.Ok);
            seekrit = manager.GetDatabase("seekrit", status);

            Assert.IsNull(seekrit, "Shouldn't have been able to reopen encrypted db with wrong password");
            Assert.AreEqual(StatusCode.Unauthorized, status.Code);

            manager.RegisterEncryptionKey(null, "seekrit");
            seekrit = manager.GetDatabase("seekrit");
            Assert.IsNotNull(seekrit, "Failed to reopen db");
            Assert.AreEqual(1, seekrit.DocumentCount);
        }

        protected override void TearDown()
        {
            base.TearDown();
            Database.EnableMockEncryption = false;
        }
    }
}

