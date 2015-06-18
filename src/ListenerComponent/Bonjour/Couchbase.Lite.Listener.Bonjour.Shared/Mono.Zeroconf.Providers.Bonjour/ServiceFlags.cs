//
// ServiceFlags.cs
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

using System;

namespace Mono.Zeroconf.Providers.Bonjour
{
    [Flags]
    public enum ServiceFlags {
        None = 0,
        MoreComing          = 0x1,
        /* MoreComing indicates to a callback that at least one more result is
        * queued and will be delivered following immediately after this one.
        * Applications should not update their UI to display browse
        * results when the MoreComing flag is set, because this would
        * result in a great deal of ugly flickering on the screen.
        * Applications should instead wait until until MoreComing is not set,
        * and then update their UI.
        * When MoreComing is not set, that doesn't mean there will be no more
        * answers EVER, just that there are no more answers immediately
        * available right now at this instant. If more answers become available
        * in the future they will be delivered as usual.
        */

        Add                 = 0x2,
        Default             = 0x4,
        /* Flags for domain enumeration and browse/query reply callbacks.
        * "Default" applies only to enumeration and is only valid in
        * conjuction with "Add".  An enumeration callback with the "Add"
        * flag NOT set indicates a "Remove", i.e. the domain is no longer
        * valid.
        */

        NoAutoRename        = 0x8,
        /* Flag for specifying renaming behavior on name conflict when registering
        * non-shared records. By default, name conflicts are automatically handled
        * by renaming the service.  NoAutoRename overrides this behavior - with this
        * flag set, name conflicts will result in a callback.  The NoAutorename flag
        * is only valid if a name is explicitly specified when registering a service
        * (i.e. the default name is not used.)
        */

        Shared              = 0x10,
        Unique              = 0x20,
        /* Flag for registering individual records on a connected
        * DNSServiceRef.  Shared indicates that there may be multiple records
        * with this name on the network (e.g. PTR records).  Unique indicates that the
        * record's name is to be unique on the network (e.g. SRV records).
        */

        BrowseDomains       = 0x40,
        RegistrationDomains = 0x80,
        /* Flags for specifying domain enumeration type in DNSServiceEnumerateDomains.
        * BrowseDomains enumerates domains recommended for browsing, RegistrationDomains
        * enumerates domains recommended for registration.
        */

        LongLivedQuery      = 0x100,
        /* Flag for creating a long-lived unicast query for the DNSServiceQueryRecord call. */

        AllowRemoteQuery    = 0x200,
        /* Flag for creating a record for which we will answer remote queries
        * (queries from hosts more than one hop away; hosts not directly connected to the local link).
        */

        ForceMulticast      = 0x400
        /* Flag for signifying that a query or registration should be performed exclusively via multicast DNS,
        * even for a name in a domain (e.g. foo.apple.com.) that would normally imply unicast DNS.
        */
    }
}
