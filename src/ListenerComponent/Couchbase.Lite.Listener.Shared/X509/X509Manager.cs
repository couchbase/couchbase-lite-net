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
using Couchbase.Lite.Util;

namespace Couchbase.Lite.Security
{
    //http://www.freekpaans.nl/2015/04/creating-self-signed-x-509-certificates-using-mono-security/
    public static class X509Manager
    {
        private static readonly string Tag = typeof(X509Manager).Name;

        /// <summary>
        /// Generates an X509 certificate for temporary use.  It is not persisted to disk.
        /// </summary>
        /// <returns>The created certificate</returns>
        /// <param name="certificateName">The subject name for the certificate</param>
        public static X509Certificate2 GenerateTransientCertificate(string certificateName)
        {
            var rawcert = CreateRawCert(certificateName, "tmp");
            return new X509Certificate2(rawcert, "tmp");
        }

        /// <summary>
        /// Gets an existing X509 certificate from the specified path, or creates one
        /// if none exists and saves it to the specified path.
        /// </summary>
        /// <returns>The created or retrieved certificate</returns>
        /// <param name="certificateName">The name to set or verify on the certificate</param>
        /// <param name="password">The password to set or use on the file.</param>
        /// <param name="savePath">The path to read from or write to</param>
        /// <exception cref="System.InvalidDataException>Thrown if the given certificateName
        /// does not match the one on the saved certificate</exception>
        public static X509Certificate2 GetPersistentCertificate(string certificateName, string password, string savePath)
        {
            var retVal = GetExistingPersistentCertificate(certificateName, password, savePath);
            if (retVal == null) {
                var rawcert = CreateRawCert(certificateName, password);
                WriteCertificate(savePath, rawcert);
                retVal = new X509Certificate2(rawcert, password);
            }

            return retVal;
        }

        /// <summary>
        /// Gets an existing X509 certificate from the specified path.
        /// </summary>
        /// <returns>The retrieved certificate, or null if it does not exist</returns>
        /// <param name="certificateName">The subject name to check on the existing certificate</param></param>
        /// <param name="password">The password to use to open the certificate file.</param>
        /// <param name="savePath">The path to read the certificate from</param>
        /// <exception cref="System.InvalidDataException>Thrown if the given certificateName
        /// does not match the one on the saved certificate</exception>
        public static X509Certificate2 GetExistingPersistentCertificate(string certificateName, string password, string filePath)
        {
            if (File.Exists(filePath)) {
                var retVal = new X509Certificate2(filePath, password);
                var cn = "CN=" + certificateName;
                if (retVal.Subject != cn) {
                    Log.To.Listener.E(Tag, "Certificate name doesn't match for {0}, " +
                        "expecting {1} but found {2}, throwing...", filePath, certificateName, retVal.Subject.Substring(3));
                    throw new InvalidDataException(String.Format("Certificate name doesn't match for {0}, " +
                        "expecting {1} but found {2}", filePath, certificateName, retVal.Subject.Substring(3)));
                }

                return retVal;
            } 

            return null;
        }

        /// <summary>
        /// Deletes and recreates a certificate at the given path
        /// </summary>
        /// <returns>The created certificate</returns>
        /// <param name="certificateName">The subject name to write to the certificate</param>
        /// <param name="password">The password to use on the certificate file.</param>
        /// <param name="savePath">The path to save the certificate to.</param>
        public static X509Certificate2 RecreatePersistentCertificate(string certificateName, string password, string savePath)
        {
            File.Delete(savePath);
            return GetPersistentCertificate(certificateName, password, savePath);
        }

        /// <summary>
        /// Reads an X509 certificate from the given stream
        /// </summary>
        /// <returns>The retrieved certificate</returns>
        /// <param name="stream">The stream that contains the X509 certificate data.</param>
        /// <param name="password">The password to use to open the certificate.</param>
        public static X509Certificate2 GetExistingPersistentCertificate(Stream stream, string password)
        {
            return new X509Certificate2(stream.ReadAllBytes(), password);
        }

        private static void WriteCertificate(string path, byte[] rawcert)
        {
            FileStream fs = File.Open(path, FileMode.Create, FileAccess.Write);
            fs.Write(rawcert, 0, rawcert.Length);
            fs.Close();
        }

        private static byte[] CreateRawCert(string certName, string password)
        {
            if (String.IsNullOrEmpty(certName)) {
                Log.To.Listener.E(Tag, "An empty certName was received in CreateRawCert, throwing...");
                throw new ArgumentException("Must contain a non-empty name", "certName");
            }

            if (String.IsNullOrEmpty(password)) {
                Log.To.Listener.E(Tag, "An empty password was received in CreateRawCert, throwing...");
                throw new ArgumentException("Must contain a non-empty password", "password");
            }

            byte[] sn = GenerateSerialNumber();
            string subject = string.Format("CN={0}", certName);
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

            Log.To.Listener.I(Tag, "Generating X509 certificate, this is expensive...");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            byte[] rawcert = cb.Sign(key);
            sw.Stop();
            Log.To.Listener.I(Tag, "Finished generating X509 certificate; took {0} sec", sw.ElapsedMilliseconds / 1000.0);
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
