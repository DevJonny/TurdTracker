// Google Identity Services OAuth module for TurdTracker
(function () {
    const CLIENT_ID = '101133685796-p43f4uqejgt9lgce4lvibrnoam8oegb1.apps.googleusercontent.com';
    const SCOPE = 'https://www.googleapis.com/auth/drive.appdata';

    let tokenClient = null;
    let accessToken = null;
    let pendingTokenRequest = null;

    function loadGisScript() {
        return new Promise((resolve, reject) => {
            if (window.google && window.google.accounts) {
                resolve();
                return;
            }
            const script = document.createElement('script');
            script.src = 'https://accounts.google.com/gsi/client';
            script.async = true;
            script.defer = true;
            script.onload = resolve;
            script.onerror = () => reject(new Error('Failed to load Google Identity Services'));
            document.head.appendChild(script);
        });
    }

    window.googleAuth = {
        initialize: async function (clientId) {
            await loadGisScript();
            tokenClient = google.accounts.oauth2.initTokenClient({
                client_id: clientId || CLIENT_ID,
                scope: SCOPE,
                callback: () => { } // overridden in signIn
            });
        },

        signIn: function () {
            if (pendingTokenRequest) {
                return pendingTokenRequest;
            }
            pendingTokenRequest = new Promise((resolve, reject) => {
                if (!tokenClient) {
                    reject(new Error('Google Auth not initialized. Call initialize() first.'));
                    return;
                }
                tokenClient.callback = (response) => {
                    pendingTokenRequest = null;
                    if (response.error) {
                        reject(new Error(response.error));
                        return;
                    }
                    accessToken = response.access_token;
                    localStorage.setItem('google-auth-connected', 'true');
                    resolve(accessToken);
                };
                tokenClient.error_callback = (error) => {
                    pendingTokenRequest = null;
                    reject(new Error(error.message || 'Sign-in failed'));
                };
                tokenClient.requestAccessToken();
            });
            return pendingTokenRequest;
        },

        signOut: function () {
            if (accessToken) {
                google.accounts.oauth2.revoke(accessToken);
                accessToken = null;
            }
            localStorage.removeItem('google-auth-connected');
        },

        trySilentSignIn: function () {
            if (pendingTokenRequest) {
                return pendingTokenRequest;
            }
            pendingTokenRequest = new Promise((resolve) => {
                if (!tokenClient) {
                    resolve(null);
                    return;
                }
                tokenClient.callback = (response) => {
                    pendingTokenRequest = null;
                    if (response.error) {
                        resolve(null);
                        return;
                    }
                    accessToken = response.access_token;
                    localStorage.setItem('google-auth-connected', 'true');
                    resolve(accessToken);
                };
                tokenClient.error_callback = () => {
                    pendingTokenRequest = null;
                    resolve(null);
                };
                tokenClient.requestAccessToken({ prompt: '' });
            });
            return pendingTokenRequest;
        },

        hasPreviousSession: function () {
            return localStorage.getItem('google-auth-connected') === 'true';
        },

        getAccessToken: function () {
            return accessToken;
        },

        isSignedIn: function () {
            return accessToken !== null;
        }
    };
})();
