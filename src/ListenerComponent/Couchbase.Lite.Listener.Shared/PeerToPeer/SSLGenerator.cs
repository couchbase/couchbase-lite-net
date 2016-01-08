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
using System.Security.Cryptography;
using System.IO;
using Mono.Security.X509;
using System.Collections;
using Mono.Security.Authenticode;
using System.Security.Cryptography.X509Certificates;

namespace Couchbase.Lite.Listener.Security
{
    //http://www.freekpaans.nl/2015/04/creating-self-signed-x-509-certificates-using-mono-security/
    internal static class SSLGenerator
    {
        //adapted from https://github.com/mono/mono/blob/master/mcs/tools/security/makecert.cs
        public static void GenerateTempKeyAndCert(string certificateName, ushort port, bool overwrite = false)
        {
            if(Type.GetType("Mono.Runtime") == null) {
                throw new PlatformNotSupportedException("Windows is not supported via this method, please install your certificate using netsh.exe");
            }

            string dirname = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine(dirname, ".mono");
            path = Path.Combine(path, "httplistener");

            string cert_file = Path.Combine(path, String.Format("{0}.cer", port));
            string pvk_file = Path.Combine(path, String.Format("{0}.pvk", port));
            if (!overwrite && File.Exists(cert_file) && File.Exists(pvk_file)) {
                return;
            }

            byte[] sn = GenerateSerialNumber();
            string subject = string.Format("CN={0}", certificateName);

            DateTime notBefore = DateTime.Now;
            DateTime notAfter = DateTime.Now.AddYears(20);

            RSA subjectKey = new RSACryptoServiceProvider(2048);
            PrivateKey privKey = new PrivateKey();
            privKey.RSA = subjectKey;

            string hashName = "SHA512";

            X509CertificateBuilder cb = new X509CertificateBuilder(3);
            cb.SerialNumber = sn;
            cb.IssuerName = subject;
            cb.NotBefore = notBefore;
            cb.NotAfter = notAfter;
            cb.SubjectName = subject;
            cb.SubjectPublicKey = subjectKey;
            cb.Hash = hashName;

            byte[] rawcert = cb.Sign(subjectKey);
            
            Directory.CreateDirectory(path);
            WriteCertificate(cert_file, rawcert);
            privKey.Save(pvk_file);
        }

        public static X509Certificate2 GetOrCreateClientCert(string password)
        {
            string dirname = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine(dirname, ".couchbase");
            Directory.CreateDirectory(path);
            path = Path.Combine(path, "client.pfx");
            if (File.Exists(path)) {
                return new X509Certificate2(path, password);
            }

            byte[] sn = GenerateSerialNumber();
            string subject = string.Format("CN=CouchbaseClient");

            DateTime notBefore = DateTime.Now;
            DateTime notAfter = DateTime.Now.AddYears(20);

            RSA subjectKey = new RSACryptoServiceProvider(2048);
            PrivateKey privKey = new PrivateKey();
            privKey.RSA = subjectKey;

            string hashName = "SHA512";

            X509CertificateBuilder cb = new X509CertificateBuilder(3);
            cb.SerialNumber = sn;
            cb.IssuerName = subject;
            cb.NotBefore = notBefore;
            cb.NotAfter = notAfter;
            cb.SubjectName = subject;
            cb.SubjectPublicKey = subjectKey;
            cb.Hash = hashName;

            byte[] rawcert = cb.Sign(subjectKey);

            PKCS12 p12 = new PKCS12();
            p12.Password = password;
            Hashtable attributes = GetAttributes();
            p12.AddCertificate(new Mono.Security.X509.X509Certificate(rawcert),attributes);
            p12.AddPkcs8ShroudedKeyBag(subjectKey,attributes);

            rawcert = p12.GetBytes();
            WriteCertificate(path, rawcert);

            return new X509Certificate2(rawcert, password);
        }

        private static void WriteCertificate(string filename, byte[] rawcert)
        {
            FileStream fs = File.Open(filename, FileMode.Create, FileAccess.Write);
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

