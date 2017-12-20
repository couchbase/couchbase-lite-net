using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace Couchbase.Lite.Tests.Shared.Util.CouchDb.Auth
{
    public class BasicAuth : IAuth
    {
        private readonly string couchDbUser;
        private readonly string couchdbUserPassword;
        public BasicAuth(string couchDbUser, string couchdbUserPassword)
        {
            this.couchDbUser = couchDbUser;
            this.couchdbUserPassword = couchdbUserPassword;
        }

        public AuthenticationHeaderValue GetAuthenticationHeaderValue()
        {
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{couchDbUser}:{couchdbUserPassword}")));
        }
    }
}
