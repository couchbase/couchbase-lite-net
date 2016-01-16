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
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Mono.Security.Authenticode;
using Mono.Security.X509;

namespace Couchbase.Lite.Security
{
    //http://www.freekpaans.nl/2015/04/creating-self-signed-x-509-certificates-using-mono-security/
    internal static class SSLGenerator
    {
        public static X509Certificate2 GenerateCert(string certificateName, RSA key, string password)
        {
            byte[] sn = GenerateSerialNumber();
            string subject = string.Format("CN={0}", certificateName);
            DateTime notBefore = DateTime.Now;
            DateTime notAfter = DateTime.Now.AddYears(20);
            string hashName = "SHA512";

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
            p12.Password = password;
            Hashtable attributes = GetAttributes();
            p12.AddCertificate(new Mono.Security.X509.X509Certificate(rawcert), attributes);
            p12.AddPkcs8ShroudedKeyBag(key, attributes);
            rawcert = p12.GetBytes();
            return new X509Certificate2(rawcert, password, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);
        }

        public static void InstallCertificateForListener(X509Certificate2 cert, ushort port)
        {
            if(Type.GetType("Mono.Runtime") == null) {
                InstallCertificateMSFT(cert, port);
            } else {
                InstallCertificateMono(cert, port);
            }
        }

        public static X509Certificate2 GetOrCreateClientCert()
        {
            string dirname = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine(dirname, ".couchbase");
            Directory.CreateDirectory(path);
            path = Path.Combine(path, "client.pfx");
            if (File.Exists(path)) {
                return new X509Certificate2(path);
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
            Hashtable attributes = GetAttributes();
            p12.AddCertificate(new Mono.Security.X509.X509Certificate(rawcert),attributes);
            p12.AddPkcs8ShroudedKeyBag(subjectKey,attributes);

            rawcert = p12.GetBytes();
            WriteCertificate(path, rawcert);

            return new X509Certificate2(rawcert);
        }

        private static void InstallCertificateMSFT(X509Certificate2 cert, ushort port)
        {
            var store = new System.Security.Cryptography.X509Certificates.X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            try {
                store.Open(OpenFlags.ReadWrite);
            } catch(CryptographicException e) {
                throw new InvalidOperationException("The process does not have the appropriate permissions to install an SSL certificate.  Please run the process as an administrator");
            }

            var existingCert = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false);
            if(existingCert.Count == 0) {
                store.Add(cert);
            }

            store.Close();

            var process = new Process {
                StartInfo = new ProcessStartInfo {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    FileName = "cmd.exe",
                    Verb = "runas",
                    Arguments = String.Format("/c netsh http add sslcert ipport=0.0.0.0:{0} appid={{{1}}} certhash={2}",
                    port, Guid.NewGuid().ToString(), cert.Thumbprint)
                }
            };
            process.Start();
            process.WaitForExit();
        }

        private static void InstallCertificateMono(X509Certificate2 cert, ushort port)
        {
            string dirname = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string path = Path.Combine(dirname, ".mono");
            path = Path.Combine(path, "httplistener");

            string cert_file = Path.Combine(path, String.Format("{0}.cer", port));
            string pvk_file = Path.Combine(path, String.Format("{0}.pvk", port));
            Directory.CreateDirectory(path);
            WriteCertificate(cert_file, cert.GetRawCertData());
            var privKey = new PrivateKey();
            privKey.RSA = (RSA)cert.PrivateKey;
            privKey.Save(pvk_file);
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

