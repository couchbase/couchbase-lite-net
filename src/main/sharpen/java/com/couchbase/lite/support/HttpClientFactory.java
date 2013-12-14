package com.couchbase.lite.support;

import org.apache.http.client.HttpClient;

public interface HttpClientFactory {
	HttpClient getHttpClient();
}
