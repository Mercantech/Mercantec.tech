window.mercantecAuth = {
  setTokens(access, refresh, expiresAt) {
    sessionStorage.setItem('mt_access_token', access);
    sessionStorage.setItem('mt_refresh_token', refresh);
    sessionStorage.setItem('mt_expires_at', String(expiresAt));
  },
  getTokens() {
    return {
      access: sessionStorage.getItem('mt_access_token'),
      refresh: sessionStorage.getItem('mt_refresh_token'),
      expiresAt: Number(sessionStorage.getItem('mt_expires_at') || '0')
    };
  },
  clearTokens() {
    sessionStorage.removeItem('mt_access_token');
    sessionStorage.removeItem('mt_refresh_token');
    sessionStorage.removeItem('mt_expires_at');
  },
  setPkce(verifier, state) {
    sessionStorage.setItem('mt_pkce_verifier', verifier);
    sessionStorage.setItem('mt_oauth_state', state);
  },
  getPkce() {
    return {
      verifier: sessionStorage.getItem('mt_pkce_verifier'),
      state: sessionStorage.getItem('mt_oauth_state')
    };
  },
  clearPkce() {
    sessionStorage.removeItem('mt_pkce_verifier');
    sessionStorage.removeItem('mt_oauth_state');
  }
};

window.mercantecDownload = {
  fromBase64(filename, contentType, base64) {
    const link = document.createElement('a');
    link.href = `data:${contentType};base64,${base64}`;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    link.remove();
  }
};
