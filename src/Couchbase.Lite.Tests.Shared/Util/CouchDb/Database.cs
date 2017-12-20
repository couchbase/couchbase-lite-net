using Couchbase.Lite.Tests.Shared.Util.CouchDb.Auth;
using Couchbase.Lite.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Lite.Tests.Shared.Util.CouchDb
{
    public class Database : IDisposable
    {
        public Uri DatabaseUri { get; protected set; }

        private readonly HttpClient httpClient;
        private readonly Uri baseUri;
        private readonly string dbName;
        private readonly IAuth auth;

        public Database(Uri baseUri, string dbName, IAuth auth)
        {
            this.httpClient = new HttpClient();
            this.baseUri = baseUri;
            this.auth = auth;
            this.dbName = dbName;
            DatabaseUri = baseUri.AppendPath(dbName);
        }

        public async Task CreateDatabaseAsync()
        {
            var createDbRequest = CreateAuthentificatedRequest(HttpMethod.Put, DatabaseUri);
            var response = await httpClient.SendAsync(createDbRequest);
            AssertSuccessStatusCode(response);
        }

        public async Task<string> GetCouchDbDocAsStringAsync(string docId)
        {
            var getRequest = CreateAuthentificatedRequest(HttpMethod.Get, DatabaseUri.AppendPath($"/{docId}"));
            var response = await httpClient.SendAsync(getRequest);
            AssertSuccessStatusCode(response);
            return await response.Content.ReadAsStringAsync();
        }

        public Task<string> UpdateCouchDbDocAsync(string docId, string docAsString)
        {
            return UpdateCouchDbDoc(docId, null, docAsString);
        }

        public async Task<string> UpdateCouchDbDoc(string docId, string rev, string docAsString)
        {
            var subPath = $"/{docId}";
            if(rev != null)
            {
                subPath = String.Concat(subPath, $"?rev={rev}");
            }

            var updateDocRequest = CreateAuthentificatedRequest(HttpMethod.Put, DatabaseUri.AppendPath(subPath));
            updateDocRequest.Content = new StringContent(docAsString);
            var response = await httpClient.SendAsync(updateDocRequest);
            AssertSuccessStatusCode(response);
            var revJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IDictionary<string, object>>(revJson)["rev"].ToString();
        }


        public async Task<string> UpdateCouchDbDocAttachmentAsync(string docId, string rev, string attachmentName, Stream stream)
        {
            var updateAttachmentRequest = CreateAuthentificatedRequest(HttpMethod.Put, DatabaseUri.AppendPath($"{docId}/{attachmentName}?rev={rev}"));
            updateAttachmentRequest.Content = new StreamContent(stream);
            var response = await httpClient.SendAsync(updateAttachmentRequest);
            AssertSuccessStatusCode(response);
            var revJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<IDictionary<string, object>>(revJson)["rev"].ToString();
        }

        private HttpRequestMessage CreateAuthentificatedRequest(HttpMethod method, Uri endPoint)
        {
            var requestMessage = new HttpRequestMessage(method, endPoint);
            requestMessage.Headers.Authorization = auth.GetAuthenticationHeaderValue();
            return requestMessage;
        }
        private void AssertSuccessStatusCode(HttpResponseMessage response)
        {
            try
            { 
                response.EnsureSuccessStatusCode();
            }
            catch(HttpRequestException e)
            {
                throw new CouchDbException("CouchDb Exception", e, (int)response.StatusCode);
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
