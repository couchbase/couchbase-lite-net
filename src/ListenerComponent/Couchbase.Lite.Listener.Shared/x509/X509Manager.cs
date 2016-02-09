//
// SSLGenerator.cs
//
// Author:
//  Jim Borden  <jim.borden@couchbase.com>
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
using System.Security.Cryptography.X509Certificates;

using Mono.Security.X509;

namespace Couchbase.Lite.Security.X509
{
    //http://www.freekpaans.nl/2015/04/creating-self-signed-x-509-certificates-using-mono-security/
    /// <summary>
    /// A utility for managing X509 certificates for use with the Couchbase Lite listener
    /// </summary>
    public static class X509Manager
    {
        public static X509Certificate2 GenerateTransientCertificate(string certificateName, string password)
        {
            var rawcert = CreateRawCert(certificateName, password);
            return new X509Certificate2(rawcert, password);
        }

        public static X509Certificate2 GetOrCreatePersistentCertificate(string certificateName, string password, string savePath)
        {
            if (File.Exists(savePath)) {
                var retVal = new X509Certificate2(savePath, password);
                var cn = String.Format("CN={0}", certificateName);
                if (retVal.Subject != cn) {
                    throw new InvalidDataException(String.Format("Certificate found at {0} has invalid name; expecting" +
                    " {1} but found {2}", savePath, certificateName, retVal.Subject));
                }

                return retVal;
            }

            var rawcert = CreateRawCert(certificateName, password);
            WriteCertificate(savePath, rawcert);
            return new X509Certificate2(rawcert, password);
        }

        public static X509Certificate2 ReadCertificate(Stream source, string password)
        {
            return new X509Certificate2(source.ReadAllBytes(), password);
        }

        public static X509Certificate2 RecreatePersistentCertificate(string certificateName, string password, string savePath)
        {
            File.Delete(savePath);
            return GetOrCreatePersistentCertificate(certificateName, password, savePath);
        }

        private static void WriteCertificate(string path, byte[] rawcert)
        {
            FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write);
            fs.Write(rawcert, 0, rawcert.Length);
            fs.Close();
        }

        private static byte[] CreateRawCert(string certificateName, string password)
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
            p12.AddCertificate(new Mono.Security.X509.X509Certificate(rawcert), attributes);
            p12.AddPkcs8ShroudedKeyBag(key, attributes);
            return p12.GetBytes();
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

