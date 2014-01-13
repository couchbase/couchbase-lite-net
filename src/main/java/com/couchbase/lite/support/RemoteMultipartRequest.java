package com.couchbase.lite.support;

import org.apache.http.client.HttpClient;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.client.methods.HttpPut;
import org.apache.http.client.methods.HttpUriRequest;
import org.apache.http.entity.mime.MultipartEntity;

import java.net.URL;
import java.util.Map;
import java.util.concurrent.ScheduledExecutorService;

public class RemoteMultipartRequest extends RemoteRequest {

    private MultipartEntity multiPart;

    public RemoteMultipartRequest(ScheduledExecutorService workExecutor,
                                  HttpClientFactory clientFactory, String method, URL url,
                                  MultipartEntity multiPart, Map<String, Object> requestHeaders, RemoteRequestCompletionBlock onCompletion) {
        super(workExecutor, clientFactory, method, url, null, requestHeaders, onCompletion);
        this.multiPart = multiPart;
    }

    @Override
    public void run() {

        HttpClient httpClient = clientFactory.getHttpClient();

        preemptivelySetAuthCredentials(httpClient);

        HttpUriRequest request = null;
        if (method.equalsIgnoreCase("PUT")) {
            HttpPut putRequest = new HttpPut(url.toExternalForm());
            putRequest.setEntity(multiPart);
            request = putRequest;

        } else if (method.equalsIgnoreCase("POST")) {
            HttpPost postRequest = new HttpPost(url.toExternalForm());
            postRequest.setEntity(multiPart);
            request = postRequest;
        } else {
            throw new IllegalArgumentException("Invalid request method: " + method);
        }

        request.addHeader("Accept", "*/*");

        executeRequest(httpClient, request);

    }


}
