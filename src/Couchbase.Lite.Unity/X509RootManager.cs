// Copyright (C) 2010 Novell, Inc (http://www.novell.com)
// Copyright 2012-2014 Xamarin Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Couchbase.Lite.Util;
using MSX = Mono.Security.X509;
using Couchbase.Lite.Support;

namespace Couchbase.Lite.Unity {

    /// <summary>
    /// A class for managing trusted TLS root certificates
    /// </summary>
    public static class X509RootManager
    {

        #region Constants

        private const string Tag = "X509RootManager";

        #endregion

        #region Variables

        // In Unity we cannot directly add anything to the X509 trusted store, so we need
        // this separate collection
        private static readonly List<MSX.X509Certificate> _CustomRoots = 
            new List<MSX.X509Certificate>();

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a certificate to the trusted root store (just for application run)
        /// </summary>
        /// <param name="certStream">A stream containing the certificate data</param>
        public static void AddRootCertificate(Stream certStream)
        {
            var data = default(byte[]);
            using (var ms = new MemoryStream()) {
                certStream.CopyTo(ms);
                data = ms.ToArray();
            }
                
            AddRootCertificate(data);
        }

        /// <summary>
        /// Adds a certificate to the trusted root store (just for application run)
        /// </summary>
        /// <param name="certData">The certificate data</param>
        public static void AddRootCertificate(IEnumerable<byte> certData)
        {
            var cert = new MSX.X509Certificate(certData.ToArray());
            Log.I(Tag, "Adding {0} (issued by {1}) to trusted roots", cert.SubjectName, cert.IssuerName);
            _CustomRoots.Add(cert);
            CouchbaseLiteHttpClientFactory.SetupSslVerification();
        }

        #endregion

        #region Internal Methods

        internal static bool Contains(X500DistinguishedName name)
        {
            Func<MSX.X509Certificate, bool> compare = x =>
            {
                Log.V(Tag, "    Evaluating {0}...", name, x.IssuerName);
                return AreEqual(name, new X500DistinguishedName(x.IssuerName));
            };

            return _CustomRoots.Any(compare) || MSX.X509StoreManager.TrustedRootCertificates.Cast<MSX.X509Certificate>()
                .Any(compare);
        }

        #endregion

        #region Private Methods

        private static string Canonize (string s)
        {
            int i = s.IndexOf ('=');
            StringBuilder r = new StringBuilder (s.Substring (0, i + 1));
            // skip any white space starting the value
            while (Char.IsWhiteSpace (s, ++i));
            // ensure we skip white spaces at the end of the value
            s = s.TrimEnd ();
            // keep track of internal multiple spaces
            bool space = false;
            for (; i < s.Length; i++) {
                if (space) {
                    space = Char.IsWhiteSpace (s, i);
                    if (space)
                        continue;
                }
                if (Char.IsWhiteSpace (s, i))
                    space = true;
                r.Append (Char.ToUpperInvariant (s[i]));
            }
            return r.ToString ();
        }

        private static bool AreEqual (X500DistinguishedName name1, X500DistinguishedName name2)
        {
            if (name1 == null)
                return (name2 == null);
            if (name2 == null)
                return false;

            X500DistinguishedNameFlags flags = X500DistinguishedNameFlags.UseNewLines | X500DistinguishedNameFlags.DoNotUseQuotes;
            string[] split = new string[] { Environment.NewLine };
            string[] parts1 = name1.Decode (flags).Split (split, StringSplitOptions.RemoveEmptyEntries);
            string[] parts2 = name2.Decode (flags).Split (split, StringSplitOptions.RemoveEmptyEntries);
            Array.Sort (parts1);
            Array.Sort (parts2);
            if (parts1.Length != parts2.Length)
                return false;

            for (int i = 0; i < parts1.Length; i++) {
                if (Canonize(parts1[i]) != Canonize(parts2[i])) {
                    UnityEngine.Debug.LogFormat("{0} != {1}", Canonize(parts1[i]), Canonize(parts2[i]));
                    return false;
                }
            }
            return true;
        }

        #endregion
    }
}
