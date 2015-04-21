//
// ServiceType.cs
//
// Authors:
//    Aaron Bockover  <abockover@novell.com>
//
// Copyright (C) 2006-2007 Novell, Inc (http://www.novell.com)
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

namespace Mono.Zeroconf.Providers.Bonjour
{
    internal enum ServiceType : ushort {
        A         = 1,      /* Host address. */
        NS        = 2,      /* Authoritative server. */
        MD        = 3,      /* Mail destination. */
        MF        = 4,      /* Mail forwarder. */
        CNAME     = 5,      /* Canonical name. */
        SOA       = 6,      /* Start of authority zone. */
        MB        = 7,      /* Mailbox domain name. */
        MG        = 8,      /* Mail group member. */
        MR        = 9,      /* Mail rename name. */
        NULL      = 10,     /* Null resource record. */
        WKS       = 11,     /* Well known service. */
        PTR       = 12,     /* Domain name pointer. */
        HINFO     = 13,     /* Host information. */
        MINFO     = 14,     /* Mailbox information. */
        MX        = 15,     /* Mail routing information. */
        TXT       = 16,     /* One or more text strings. */
        RP        = 17,     /* Responsible person. */
        AFSDB     = 18,     /* AFS cell database. */
        X25       = 19,     /* X_25 calling address. */
        ISDN      = 20,     /* ISDN calling address. */
        RT        = 21,     /* Router. */
        NSAP      = 22,     /* NSAP address. */
        NSAP_PTR  = 23,     /* Reverse NSAP lookup (deprecated). */
        SIG       = 24,     /* Security signature. */
        KEY       = 25,     /* Security key. */
        PX        = 26,     /* X.400 mail mapping. */
        GPOS      = 27,     /* Geographical position (withdrawn). */
        AAAA      = 28,     /* IPv6 Address. */
        LOC       = 29,     /* Location Information. */
        NXT       = 30,     /* Next domain (security). */
        EID       = 31,     /* Endpoint identifier. */
        NIMLOC    = 32,     /* Nimrod Locator. */
        SRV       = 33,     /* Server Selection. */
        ATMA      = 34,     /* ATM Address */
        NAPTR     = 35,     /* Naming Authority PoinTeR */
        KX        = 36,     /* Key Exchange */
        CERT      = 37,     /* Certification record */
        A6        = 38,     /* IPv6 Address (deprecated) */
        DNAME     = 39,     /* Non-terminal DNAME (for IPv6) */
        SINK      = 40,     /* Kitchen sink (experimentatl) */
        OPT       = 41,     /* EDNS0 option (meta-RR) */
        TKEY      = 249,    /* Transaction key */
        TSIG      = 250,    /* Transaction signature. */
        IXFR      = 251,    /* Incremental zone transfer. */
        AXFR      = 252,    /* Transfer zone of authority. */
        MAILB     = 253,    /* Transfer mailbox records. */
        MAILA     = 254,    /* Transfer mail agent records. */
        ANY       = 255     /* Wildcard match. */
    }
}
