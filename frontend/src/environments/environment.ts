// The API runs on the same machine as "ng serve": localhost on this PC, its network address
// (e.g. 192.168.1.11) when the dev site is opened from a phone on the same Wi-Fi.
const apiHost = typeof location !== 'undefined' && location.hostname ? location.hostname : 'localhost';

export const environment = {
  production: false,
  apiUrl: `http://${apiHost}:5255/api`,
  apiOrigin: `http://${apiHost}:5255`,
  // Public site address used in canonical links and share previews — must match Seo:SiteUrl on the API.
  siteUrl: 'https://ghumoodisha.com',
  // Google OAuth web client id (public). Must match GoogleAuth:ClientId on the API, and this
  // page's origin must be listed under "Authorized JavaScript origins" in Google Cloud Console.
  googleClientId: '947854606639-dhfpnn50ssed3526863uog4spug6u12f.apps.googleusercontent.com',
};
