//
// SSLGenerator.cs
//
// Author:
// 	Jim Borden  <jim.borden@couchbase.com>
//
// Copyright (c) 2015 Couchbase, Inc All rights reserved.
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
using System.Collections;
using System.IO;
using System.Security.Cryptography;

using Mono.Security.X509;

namespace Couchbase.Lite.Security
{
    //http://www.freekpaans.nl/2015/04/creating-self-signed-x-509-certificates-using-mono-security/
    internal static class SSLGenerator
    {
        public static byte[] GenerateCert(string certificateName, string password)
        {
            if (String.IsNullOrEmpty(certificateName)) {
                throw new ArgumentException("Must contain a non-empty name", "certificateName");
            }

            if (String.IsNullOrEmpty(password)) {
                throw new ArgumentException("Must contain a non-empty password", "password");
            }

            byte[] sn = GenerateSerialNumber();
            string subject = string.Format("CN={0}", certificateName);
            DateTime notBefore = DateTime.Now;
            DateTime notAfter = DateTime.Now.AddYears(20);
            string hashName = "SHA512";
            var key = new RSACryptoServiceProvider(2048);

            X509CertificateBuilder cb = new X509CertificateBuilder(3);
            cb.SerialNumber = sn;
            cb.IssuerName = subject;
            cb.NotBefore = notBefore;
            cb.NotAfter = notAfter;
            cb.SubjectName = subject;
            cb.SubjectPublicKey = key;
            cb.Hash = hashName;

            byte[] rawcert = cb.Sign(key);
            PKCS12 p12 = new PKCS12();
            if (!String.IsNullOrEmpty(password)) {
                p12.Password = password;
            }
            Hashtable attributes = GetAttributes();
            p12.AddCertificate(new X509Certificate(rawcert), attributes);
            p12.AddPkcs8ShroudedKeyBag(key, attributes);
            rawcert = p12.GetBytes();
            return rawcert;
        }

        public static void WriteCertificate(string path, byte[] rawcert)
        {
            FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write);
            fs.Write(rawcert, 0, rawcert.Length);
            fs.Close();
        }

        private static Hashtable GetAttributes()
        {
            ArrayList list = new ArrayList();
            // we use a fixed array to avoid endianess issues 
            // (in case some tools requires the ID to be 1).
            list.Add(new byte[4] { 1, 0, 0, 0 });
            Hashtable attributes = new Hashtable(1);
            attributes.Add(PKCS9.localKeyId, list);
            return attributes;
        }

        private static byte[] GenerateSerialNumber()
        {
            byte[] sn = Guid.NewGuid().ToByteArray();

            //must be positive
            if((sn[0] & 0x80) == 0x80)
                sn[0] -= 0x80;
            return sn;
        }
    }
}

