package com.couchbase.lite.support;

import org.apache.http.client.CookieStore;
import org.apache.http.client.HttpClient;
import org.apache.http.conn.ClientConnectionManager;
import org.apache.http.conn.scheme.PlainSocketFactory;
import org.apache.http.conn.scheme.Scheme;
import org.apache.http.conn.scheme.SchemeRegistry;
import org.apache.http.conn.ssl.SSLSocketFactory;
import org.apache.http.cookie.Cookie;
import org.apache.http.impl.client.trunk.BasicCookieStore;
import org.apache.http.impl.client.DefaultHttpClient;
import org.apache.http.impl.conn.tsccm.ThreadSafeClientConnManager;
import org.apache.http.params.BasicHttpParams;

import java.util.List;

public class CouchbaseLiteHttpClientFactory implements HttpClientFactory {

	public static CouchbaseLiteHttpClientFactory INSTANCE;

    private CookieStore cookieStore;

    private SSLSocketFactory sslSocketFactory;

    /**
     * @param sslSocketFactoryFromUser This is to open up the system for end user to inject the sslSocket factories with their
     *                                 custom KeyStore
     */
    public void setSSLSocketFactory(SSLSocketFactory sslSocketFactoryFromUser) {
        if (sslSocketFactory != null) {
            throw new RuntimeException("SSLSocketFactory already set");
        }
        sslSocketFactory = sslSocketFactoryFromUser;
    }

    @Override
    public HttpClient getHttpClient() {

        // workaround attempt for issue #81
        // it does not seem like _not_ using the ThreadSafeClientConnManager actually
        // caused any problems, but it seems wise to use it "just in case", since it provides
        // extra safety and there are no observed side effects.
        BasicHttpParams params = new BasicHttpParams();
        SchemeRegistry schemeRegistry = new SchemeRegistry();
        schemeRegistry.register(new Scheme("http", PlainSocketFactory.getSocketFactory(), 80));
        final SSLSocketFactory sslSocketFactory = SSLSocketFactory.getSocketFactory();
        schemeRegistry.register(new Scheme("https", this.sslSocketFactory == null ? sslSocketFactory : this.sslSocketFactory, 443));
        ClientConnectionManager cm = new ThreadSafeClientConnManager(params, schemeRegistry);

        DefaultHttpClient client = new DefaultHttpClient(cm, params);

        // synchronize access to the cookieStore in case there is another
        // thread in the middle of updating it.  wait until they are done so we get their changes.
        synchronized (this) {
            client.setCookieStore(cookieStore);
        }
        return client;

    }

    public void addCookies(List<Cookie> cookies) {
        synchronized (this) {
            if (cookieStore == null) {
                cookieStore = new BasicCookieStore();
            }
            for (Cookie cookie : cookies) {
                cookieStore.addCookie(cookie);
            }
        }
    }


}
