// Google Identity Services OAuth module for TurdTracker
(function () {
    const CLIENT_ID = '101133685796-p43f4uqejgt9lgce4lvibrnoam8oegb1.apps.googleusercontent.com';
    const SCOPE = 'https://www.googleapis.com/auth/drive.appdata';

    let tokenClient = null;
    let accessToken = null;

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
            return new Promise((resolve, reject) => {
                if (!tokenClient) {
                    reject(new Error('Google Auth not initialized. Call initialize() first.'));
                    return;
                }
                tokenClient.callback = (response) => {
                    if (response.error) {
                        reject(new Error(response.error));
                        return;
                    }
                    accessToken = response.access_token;
                    resolve(accessToken);
                };
                tokenClient.error_callback = (error) => {
                    reject(new Error(error.message || 'Sign-in failed'));
                };
                tokenClient.requestAccessToken();
            });
        },

        signOut: function () {
            if (accessToken) {
                google.accounts.oauth2.revoke(accessToken);
                accessToken = null;
            }
        },

        getAccessToken: function () {
            return accessToken;
        },

        isSignedIn: function () {
            return accessToken !== null;
        }
    };
})();
