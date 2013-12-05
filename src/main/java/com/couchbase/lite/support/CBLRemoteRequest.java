package com.couchbase.lite.support;

import android.net.Uri;

import com.couchbase.lite.Database;
import com.couchbase.lite.Manager;
import com.couchbase.lite.util.Log;

import org.apache.http.HttpEntity;
import org.apache.http.HttpException;
import org.apache.http.HttpHost;
import org.apache.http.HttpRequest;
import org.apache.http.HttpRequestInterceptor;
import org.apache.http.HttpResponse;
import org.apache.http.StatusLine;
import org.apache.http.auth.AuthScope;
import org.apache.http.auth.AuthState;
import org.apache.http.auth.Credentials;
import org.apache.http.auth.UsernamePasswordCredentials;
import org.apache.http.client.ClientProtocolException;
import org.apache.http.client.CredentialsProvider;
import org.apache.http.client.HttpClient;
import org.apache.http.client.HttpResponseException;
import org.apache.http.client.methods.HttpEntityEnclosingRequestBase;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.client.methods.HttpPut;
import org.apache.http.client.methods.HttpUriRequest;
import org.apache.http.client.protocol.ClientContext;
import org.apache.http.conn.ClientConnectionManager;
import org.apache.http.entity.ByteArrayEntity;
import org.apache.http.impl.auth.BasicScheme;
import org.apache.http.impl.client.DefaultHttpClient;
import org.apache.http.protocol.ExecutionContext;
import org.apache.http.protocol.HttpContext;

import java.io.IOException;
import java.io.InputStream;
import java.net.URL;
import java.util.concurrent.ScheduledExecutorService;


public class CBLRemoteRequest implements Runnable {

    protected ScheduledExecutorService workExecutor;
    protected final HttpClientFactory clientFactory;
    protected String method;
    protected URL url;
    protected Object body;
    protected RemoteRequestCompletionBlock onCompletion;

    public CBLRemoteRequest(ScheduledExecutorService workExecutor,
                            HttpClientFactory clientFactory, String method, URL url,
                            Object body, RemoteRequestCompletionBlock onCompletion) {
        this.clientFactory = clientFactory;
        this.method = method;
        this.url = url;
        this.body = body;
        this.onCompletion = onCompletion;
        this.workExecutor = workExecutor;
    }

    @Override
    public void run() {

        HttpClient httpClient = clientFactory.getHttpClient();

        ClientConnectionManager manager = httpClient.getConnectionManager();

        HttpUriRequest request = createConcreteRequest();

        preemptivelySetAuthCredentials(httpClient);

        request.addHeader("Accept", "multipart/related, application/json");

        setBody(request);

        executeRequest(httpClient, request);

    }

    protected HttpUriRequest createConcreteRequest() {
        HttpUriRequest request = null;
        if (method.equalsIgnoreCase("GET")) {
            request = new HttpGet(url.toExternalForm());
        } else if (method.equalsIgnoreCase("PUT")) {
            request = new HttpPut(url.toExternalForm());
        } else if (method.equalsIgnoreCase("POST")) {
            request = new HttpPost(url.toExternalForm());
        }
        return request;
    }

    private void setBody(HttpUriRequest request) {
        // set body if appropriate
        if (body != null && request instanceof HttpEntityEnclosingRequestBase) {
            byte[] bodyBytes = null;
            try {
                bodyBytes = Manager.getObjectMapper().writeValueAsBytes(body);
            } catch (Exception e) {
                Log.e(Database.TAG, "Error serializing body of request", e);
            }
            ByteArrayEntity entity = new ByteArrayEntity(bodyBytes);
            entity.setContentType("application/json");
            ((HttpEntityEnclosingRequestBase) request).setEntity(entity);
        }
    }

    protected void executeRequest(HttpClient httpClient, HttpUriRequest request) {
        Object fullBody = null;
        Throwable error = null;

        try {

            HttpResponse response = httpClient.execute(request);

            // add in cookies to global store
            DefaultHttpClient defaultHttpClient = (DefaultHttpClient)httpClient;
            CBLHttpClientFactory.INSTANCE.addCookies(defaultHttpClient.getCookieStore().getCookies());

            StatusLine status = response.getStatusLine();
            if (status.getStatusCode() >= 300) {
                Log.e(Database.TAG,
                        "Got error " + Integer.toString(status.getStatusCode()));
                Log.e(Database.TAG, "Request was for: " + request.toString());
                Log.e(Database.TAG,
                        "Status reason: " + status.getReasonPhrase());
                error = new HttpResponseException(status.getStatusCode(),
                        status.getReasonPhrase());
            } else {
                HttpEntity temp = response.getEntity();
                if (temp != null) {
                    InputStream stream = null;
                    try {
                        stream = temp.getContent();
                        fullBody = Manager.getObjectMapper().readValue(stream,
                                Object.class);
                    } finally {
                        try {
                            stream.close();
                        } catch (IOException e) {
                        }
                    }
                }
            }
        } catch (ClientProtocolException e) {
            Log.e(Database.TAG, "client protocol exception", e);
            error = e;
        } catch (IOException e) {
            Log.e(Database.TAG, "io exception", e);
            error = e;
        }
        respondWithResult(fullBody, error);
    }

    protected void preemptivelySetAuthCredentials(HttpClient httpClient) {
        // if the URL contains user info AND if this a DefaultHttpClient
        // then preemptively set the auth credentials
        if (url.getUserInfo() != null) {
            if (url.getUserInfo().contains(":") && !url.getUserInfo().trim().equals(":")) {
                String[] userInfoSplit = url.getUserInfo().split(":");
                final Credentials creds = new UsernamePasswordCredentials(
                        Uri.decode(userInfoSplit[0]), Uri.decode(userInfoSplit[1]));
                if (httpClient instanceof DefaultHttpClient) {
                    DefaultHttpClient dhc = (DefaultHttpClient) httpClient;

                    HttpRequestInterceptor preemptiveAuth = new HttpRequestInterceptor() {

                        @Override
                        public void process(HttpRequest request,
                                HttpContext context) throws HttpException,
                                IOException {
                            AuthState authState = (AuthState) context
                                    .getAttribute(ClientContext.TARGET_AUTH_STATE);
                            CredentialsProvider credsProvider = (CredentialsProvider) context
                                    .getAttribute(ClientContext.CREDS_PROVIDER);
                            HttpHost targetHost = (HttpHost) context
                                    .getAttribute(ExecutionContext.HTTP_TARGET_HOST);

                            if (authState.getAuthScheme() == null) {
                                AuthScope authScope = new AuthScope(
                                        targetHost.getHostName(),
                                        targetHost.getPort());
                                authState.setAuthScheme(new BasicScheme());
                                authState.setCredentials(creds);
                            }
                        }
                    };

                    dhc.addRequestInterceptor(preemptiveAuth, 0);
                }
            } else {
                Log.w(Database.TAG,
                        "CBLRemoteRequest Unable to parse user info, not setting credentials");
            }
        }
    }

    public void respondWithResult(final Object result, final Throwable error) {
        if (workExecutor != null) {
            workExecutor.submit(new Runnable() {

                @Override
                public void run() {
                    try {
                        onCompletion.onCompletion(result, error);
                    } catch (Exception e) {
                        // don't let this crash the thread
                        Log.e(Database.TAG,
                                "RemoteRequestCompletionBlock throw Exception",
                                e);
                    }
                }
            });
        } else {
            Log.e(Database.TAG, "work executor was null!!!");
        }
    }

}
