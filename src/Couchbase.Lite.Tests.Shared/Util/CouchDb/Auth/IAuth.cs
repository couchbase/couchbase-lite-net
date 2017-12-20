using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;

namespace Couchbase.Lite.Tests.Shared.Util.CouchDb.Auth
{
    public interface IAuth
    {
        AuthenticationHeaderValue GetAuthenticationHeaderValue();
    }
}
