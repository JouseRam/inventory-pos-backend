const API_BASE = '/api';

const api = {
  async get(path) {
    const r = await fetch(API_BASE + path);
    if (!r.ok) throw new Error(await r.text());
    return r.json();
  },
  async post(path, body) {
    const r = await fetch(API_BASE + path, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    if (!r.ok) {
      const msg = await r.text();
      let err;
      try { err = JSON.parse(msg).error || msg; } catch { err = msg; }
      throw new Error(err);
    }
    return r.json();
  },
  async put(path, body) {
    const r = await fetch(API_BASE + path, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body)
    });
    if (!r.ok) {
      const msg = await r.text();
      let err;
      try { err = JSON.parse(msg).error || msg; } catch { err = msg; }
      throw new Error(err);
    }
    return r.json();
  },
  async del(path) {
    const r = await fetch(API_BASE + path, { method: 'DELETE' });
    if (!r.ok) throw new Error(await r.text());
    return r.status !== 204 ? r.json() : null;
  }
};

function fmt(n, decimals = 2) {
  return Number(n || 0).toLocaleString('es-MX', { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
}
function fmtDate(d) {
  if (!d) return '';
  return new Date(d).toLocaleDateString('es-MX', { day: '2-digit', month: 'short', year: 'numeric' });
}
function fmtDateTime(d) {
  if (!d) return '';
  return new Date(d).toLocaleString('es-MX', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function toast(msg, type = 'default', duration = 3500) {
  const container = document.getElementById('toast-container') || (() => {
    const el = document.createElement('div');
    el.id = 'toast-container';
    document.body.appendChild(el);
    return el;
  })();
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  const icon = type === 'success' ? 'check-circle' : type === 'error' ? 'x-circle' : type === 'warning' ? 'exclamation-triangle' : 'info-circle';
  el.innerHTML = `<i class="bi bi-${icon}"></i> ${msg}`;
  container.appendChild(el);
  setTimeout(() => el.remove(), duration);
}

function confirm(msg) {
  return window.confirm(msg);
}
