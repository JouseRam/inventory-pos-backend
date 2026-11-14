/*
 * Product labels on letter sheets for a laser (or inkjet) printer.
 * Two sizes: small 2 × 1 cm (delineadores, small jars) and medium 3 × 2 cm with every detail.
 * The code (QR or Code 128) carries the system ID; the scanner types it at the register and the system looks up the
 * current product, lot and price. Everything else is printed as text so it can be read at a glance.
 *
 * Usage: POS.labels.open(labels, { title }) where labels come from /api/labels/...
 */
(function () {
  const { esc, fmt, toast } = POS;

  // ── Sheet geometry (mm) ──
  const PAGE = { w: 215.9, h: 279.4 }; // Carta
  const SIZES = {
    small: { w: 20, h: 10, label: 'Pequeña · 2 × 1 cm' },
    medium: { w: 30, h: 20, label: 'Mediana · 3 × 2 cm' },
  };
  const DEFAULTS = { size: 'medium', symbology: 'qr', top: 10, left: 8, gapX: 2, gapY: 2, start: 1, cutLines: true };
  const STORE_KEY = 'pos.labels.layout';
  const load = () => { try { return { ...DEFAULTS, ...JSON.parse(localStorage.getItem(STORE_KEY) || '{}'), start: 1 }; } catch { return { ...DEFAULTS }; } };
  const save = o => { try { localStorage.setItem(STORE_KEY, JSON.stringify(o)); } catch { /* storage blocked */ } };

  function grid(o) {
    const s = SIZES[o.size];
    const cols = Math.max(1, Math.floor((PAGE.w - 2 * o.left + o.gapX) / (s.w + o.gapX)));
    const rows = Math.max(1, Math.floor((PAGE.h - 2 * o.top + o.gapY) / (s.h + o.gapY)));
    return { cols, rows, perSheet: cols * rows };
  }

  // ── QR (vendor/qrcode/qrcode.js) as an SVG path in module units ──
  function qrSvg(text, sizeMm) {
    const qr = qrcode(0, 'M');
    qr.addData(text, /^[0-9A-Z $%*+\-./:]*$/.test(text) ? 'Alphanumeric' : 'Byte');
    qr.make();
    const n = qr.getModuleCount(), quiet = 1, total = n + quiet * 2;
    let d = '';
    for (let r = 0; r < n; r++) for (let c = 0; c < n; c++) if (qr.isDark(r, c)) d += `M${c + quiet} ${r + quiet}h1v1h-1z`;
    return `<svg class="qr" viewBox="0 0 ${total} ${total}" style="width:${sizeMm}mm;height:${sizeMm}mm" shape-rendering="crispEdges"><path d="${d}"/></svg>`;
  }

  // ── Code 128 (set B): widths of bar/space pairs for values 0–106 ──
  const C128 = ('212222 222122 222221 121223 121322 131222 122213 122312 132212 221213 221312 231212 112232 122132 122231 113222 ' +
    '123122 123221 223211 221132 221231 213212 223112 312131 311222 321122 321221 312212 322112 322211 212123 212321 232121 111323 ' +
    '131123 131321 112313 132113 132311 211313 231113 231311 112133 112331 132131 113123 113321 133121 313121 211331 231131 213113 ' +
    '213311 213131 311123 311321 331121 312113 312311 332111 314111 221411 431111 111224 111422 121124 121421 141122 141221 112214 ' +
    '112412 122114 122411 142112 142211 241211 221114 413111 241112 134111 111242 121142 121241 114212 124112 124211 411212 421112 ' +
    '421211 212141 214121 412121 111143 111341 131141 114113 114311 411113 411311 113141 114131 311141 411131 211412 211214 211232 2331112').split(' ');

  function code128Svg(text, widthMm, heightMm) {
    const values = [...text].map(ch => ch.charCodeAt(0) - 32);
    if (values.some(v => v < 0 || v > 94)) throw new Error('El código solo admite caracteres ASCII.');
    const check = (104 + values.reduce((sum, v, i) => sum + v * (i + 1), 0)) % 103;
    const symbols = [104, ...values, check, 106];
    const quiet = 6;
    let x = quiet, d = '';
    for (const s of symbols) {
      [...C128[s]].forEach((w, i) => { const width = Number(w); if (i % 2 === 0) d += `M${x} 0h${width}v1h-${width}z`; x += width; });
    }
    return `<svg class="bars" viewBox="0 0 ${x + quiet} 1" preserveAspectRatio="none" style="width:${widthMm}mm;height:${heightMm}mm" shape-rendering="crispEdges"><path d="${d}"/></svg>`;
  }

  // dd/mm/aa: the long date does not fit a 3 cm label.
  const shortDate = d => { const [y, m, day] = String(d).slice(0, 10).split('-'); return `${day}/${m}/${y.slice(2)}`; };

  // ── One label ──
  function label(l, o) {
    const price = fmt.money(l.price);
    const lot = l.lot ? `Lote ${esc(l.lot)}${l.expirationDate ? ' · cad. ' + shortDate(l.expirationDate) : ''}` : '';
    const name = esc(l.name);
    if (o.size === 'small') {
      return `<div class="lbl small">${qrSvg(l.code, 8.6)}
        <div class="txt"><b class="name">${name}</b><span class="price">${price}</span>
          ${l.lot ? `<span class="tiny">L ${esc(l.lot)}</span>` : ''}<span class="tiny mono">${esc(l.code)}</span></div></div>`;
    }
    const detail = [l.shade, l.size].filter(Boolean).map(esc).join(' · ');
    if (o.symbology === 'barcode') {
      return `<div class="lbl medium bar">
        <b class="name">${name}</b>
        ${l.category ? `<span class="tiny">${esc(l.category)}</span>` : ''}
        <div class="row"><span class="tiny">${lot || detail}</span><span class="price">${price}</span></div>
        ${code128Svg(l.code, 28, 5.2)}<span class="tiny mono center">${esc(l.code)}</span></div>`;
    }
    return `<div class="lbl medium">
      <div class="code">${qrSvg(l.code, 12.5)}<span class="tiny mono">${esc(l.code)}</span></div>
      <div class="txt"><b class="name">${name}</b>
        ${l.category ? `<span class="tiny">${esc(l.category)}</span>` : ''}
        ${detail ? `<span class="tiny">${detail}</span>` : ''}
        ${l.lot ? `<span class="tiny">Lote ${esc(l.lot)}</span>` : ''}
        ${l.lot && l.expirationDate ? `<span class="tiny">Caduca ${shortDate(l.expirationDate)}</span>` : ''}
        <span class="price">${price}</span></div></div>`;
  }

  /** Printable document: absolute positions in millimetres so the printer places each label where the sheet has it. */
  function sheets(labels, o) {
    const s = SIZES[o.size];
    const g = grid(o);
    const queue = [];
    for (let i = 1; i < o.start; i++) queue.push(null); // skip used positions of a partly used sheet
    labels.forEach(l => { for (let i = 0; i < l.copies; i++) queue.push(l); });
    const pages = [];
    for (let p = 0; p * g.perSheet < queue.length; p++) {
      const cells = queue.slice(p * g.perSheet, (p + 1) * g.perSheet).map((l, i) => {
        if (!l) return '';
        const c = i % g.cols, r = Math.floor(i / g.cols);
        return `<div class="cell ${o.cutLines ? 'cut' : ''}" style="left:${o.left + c * (s.w + o.gapX)}mm;top:${o.top + r * (s.h + o.gapY)}mm;width:${s.w}mm;height:${s.h}mm">${label(l, o)}</div>`;
      }).join('');
      pages.push(`<section class="sheet">${cells}</section>`);
    }
    return { html: pages.join(''), pages: pages.length, count: queue.length - (o.start - 1) };
  }

  const SHEET_CSS = `
    @page { size: letter; margin: 0; }
    * { box-sizing: border-box; }
    body { margin: 0; font-family: 'Instrument Sans', Arial, sans-serif; color: #000; -webkit-print-color-adjust: exact; print-color-adjust: exact; }
    .sheet { position: relative; width: ${PAGE.w}mm; height: ${PAGE.h}mm; overflow: hidden; break-after: page; background: #fff; }
    .sheet:last-child { break-after: auto; }
    .cell { position: absolute; overflow: hidden; }
    .cell.cut { outline: 0.1mm dashed #b8b8b8; outline-offset: 0; }
    .lbl { width: 100%; height: 100%; padding: 0.6mm; display: flex; gap: 0.7mm; line-height: 1.12; }
    .lbl .txt > *, .lbl.bar > * { flex-shrink: 0; }
    .lbl svg path { fill: #000; }
    .lbl .txt { display: flex; flex-direction: column; min-width: 0; flex: 1; justify-content: center; gap: 0.25mm; }
    .lbl .name { font-size: 5.2pt; font-weight: 700; overflow: hidden; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; word-break: break-word; }
    .lbl .tiny { font-size: 3.9pt; line-height: 1.3; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .lbl .mono { font-family: 'IBM Plex Mono', 'Courier New', monospace; letter-spacing: 0.02em; }
    .lbl .price { font-size: 7pt; font-weight: 800; }
    .lbl.small { align-items: center; padding: 0.4mm; gap: 0.5mm; line-height: 1.05; }
    .lbl.small .txt { gap: 0.1mm; }
    /* Small labels have room for one line of name: price, lot and ID matter more. */
    .lbl.small .name { font-size: 4.2pt; line-height: 1.3; -webkit-line-clamp: 1; white-space: nowrap; text-overflow: ellipsis; display: block; }
    .lbl.small .price { font-size: 5.4pt; }
    .lbl.small .tiny { font-size: 3.2pt; }
    .lbl.medium .code { display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 0.2mm; }
    .lbl.medium .name { font-size: 5.6pt; }
    .lbl.medium .price { font-size: 8pt; }
    .lbl.bar { flex-direction: column; gap: 0.25mm; justify-content: space-between; }
    .lbl.bar .row { display: flex; justify-content: space-between; align-items: baseline; gap: 1mm; }
    .lbl.bar .bars { display: block; margin: 0 auto; }
    .lbl .center { text-align: center; }`;

  // ── Dialog ──
  function dialog() {
    let dlg = document.getElementById('labels-dialog');
    if (dlg) return dlg;
    document.body.insertAdjacentHTML('beforeend', `
      <dialog id="labels-dialog" class="labels-dialog" aria-labelledby="labels-title"><form method="dialog" novalidate>
        <header><h2 id="labels-title">Imprimir etiquetas</h2><button class="btn ghost icon-btn" value="cancel" aria-label="Cerrar"><i class="bi bi-x-lg"></i></button></header>
        <div class="body labels-body">
          <div class="labels-options">
            <fieldset class="seg"><legend>Tamaño</legend>
              <label><input type="radio" name="size" value="small"> Pequeña · 2 × 1 cm</label>
              <label><input type="radio" name="size" value="medium"> Mediana · 3 × 2 cm</label></fieldset>
            <fieldset class="seg" data-symbology><legend>Código</legend>
              <label><input type="radio" name="symbology" value="qr"> QR (recomendado)</label>
              <label><input type="radio" name="symbology" value="barcode"> Código de barras</label></fieldset>
            <p class="muted small-note" data-small-note hidden>La etiqueta pequeña siempre lleva QR: un código de barras de 2 cm no se lee bien.</p>
            <details class="layout"><summary>Acomodo en la hoja carta</summary>
              <div class="form-grid" style="margin-top:8px">
                <label class="field"><span>Margen superior (mm)</span><input type="number" name="top" min="0" max="40" step="0.5"></label>
                <label class="field"><span>Margen izquierdo (mm)</span><input type="number" name="left" min="0" max="40" step="0.5"></label>
                <label class="field"><span>Separación horizontal (mm)</span><input type="number" name="gapX" min="0" max="20" step="0.5"></label>
                <label class="field"><span>Separación vertical (mm)</span><input type="number" name="gapY" min="0" max="20" step="0.5"></label>
                <label class="check span-all"><input type="checkbox" name="cutLines"> Líneas guía para recortar (hoja adhesiva completa)</label>
              </div>
              <p class="muted" style="margin:6px 0 0;font-size:.82rem">Con hojas precortadas, copia aquí las medidas que vienen en el empaque.</p>
            </details>
            <label class="field"><span>Empezar en la posición</span><input type="number" name="start" min="1" step="1"></label>
            <p class="muted" style="margin:0;font-size:.82rem" data-capacity></p>
          </div>
          <div class="labels-items">
            <div class="labels-items-head"><b>Etiquetas</b><span class="muted" data-total></span></div>
            <div class="labels-list" data-list></div>
            <div class="labels-preview" data-preview></div>
          </div>
        </div>
        <footer>
          <span class="muted" style="margin-right:auto;font-size:.82rem">Imprime a escala 100 % (tamaño real), sin «ajustar a la página».</span>
          <button class="btn ghost" value="cancel">Cerrar</button>
          <button class="btn primary" type="button" data-print><i class="bi bi-printer"></i>Imprimir</button>
        </footer>
      </form></dialog>`);
    return document.getElementById('labels-dialog');
  }

  function open(labels, { title } = {}) {
    if (!labels.length) { toast('No hay etiquetas que imprimir.', 'error'); return; }
    const dlg = dialog();
    const form = dlg.querySelector('form');
    const items = labels.map(l => ({ ...l }));
    const o = load();
    dlg.querySelector('#labels-title').textContent = title ? `Etiquetas · ${title}` : 'Imprimir etiquetas';

    const el = name => form.elements[name];
    el('size').value = o.size;
    el('symbology').value = o.symbology;
    ['top', 'left', 'gapX', 'gapY', 'start'].forEach(k => { el(k).value = o[k]; });
    el('cutLines').checked = o.cutLines;

    dlg.querySelector('[data-list]').innerHTML = items.map((l, i) => `
      <div class="labels-row">
        <div><b>${esc(l.name)}</b><span class="muted">${esc([l.shade, l.size, l.lot ? 'Lote ' + l.lot : null].filter(Boolean).join(' · '))}</span>
          <span class="mono muted">${esc(l.code)} · ${fmt.money(l.price)}</span></div>
        <label class="field"><span>Copias</span><input type="number" min="0" max="500" step="1" value="${l.copies}" data-copies="${i}"></label>
      </div>`).join('');

    function read() {
      o.size = el('size').value;
      o.symbology = o.size === 'small' ? 'qr' : el('symbology').value;
      ['top', 'left', 'gapX', 'gapY'].forEach(k => { o[k] = Math.max(0, Number(el(k).value) || 0); });
      o.start = Math.max(1, Math.floor(Number(el('start').value) || 1));
      o.cutLines = el('cutLines').checked;
      dlg.querySelectorAll('[data-copies]').forEach(inp => { items[inp.dataset.copies].copies = Math.max(0, Math.floor(Number(inp.value) || 0)); });
    }

    function refresh() {
      read();
      save(o);
      dlg.querySelector('[data-symbology]').disabled = o.size === 'small';
      dlg.querySelector('[data-small-note]').hidden = o.size !== 'small';
      const g = grid(o);
      const doc = sheets(items, o);
      dlg.querySelector('[data-capacity]').textContent = `${g.cols} × ${g.rows} = ${g.perSheet} etiquetas por hoja carta.`;
      dlg.querySelector('[data-total]').textContent = `${doc.count} etiquetas · ${doc.pages} ${doc.pages === 1 ? 'hoja' : 'hojas'}`;
      // Preview: the first labels at real proportion, enlarged.
      const sample = items.filter(l => l.copies > 0).slice(0, 4);
      const s = SIZES[o.size];
      dlg.querySelector('[data-preview]').innerHTML = sample.length
        ? `<style>${SHEET_CSS.replace(/@page[^}]*}/, '').replace(/body \{[^}]*}/, '')}</style>` + sample.map(l =>
          `<div class="preview-cell" style="width:${s.w}mm;height:${s.h}mm">${label(l, o)}</div>`).join('')
        : '<p class="muted">Indica cuántas copias quieres.</p>';
      dlg.querySelector('[data-print]').disabled = doc.count === 0;
    }

    form.oninput = refresh;
    form.onchange = refresh;
    dlg.querySelector('[data-print]').onclick = () => { read(); print(items, o); };
    refresh();
    if (!dlg.open) dlg.showModal();
  }

  /** Prints from a hidden frame: the register's window stays where it is and no pop-up is needed. */
  function print(items, o) {
    const doc = sheets(items, o);
    if (!doc.count) return;
    document.getElementById('labels-print')?.remove();
    const frame = Object.assign(document.createElement('iframe'), { id: 'labels-print', title: 'Etiquetas' });
    frame.style.cssText = 'position:fixed;width:0;height:0;border:0;right:0;bottom:0';
    document.body.append(frame);
    const fonts = [...document.querySelectorAll('link[rel=stylesheet]')].filter(l => l.href.includes('fonts')).map(l => `<link rel="stylesheet" href="${l.href}">`).join('');
    frame.contentDocument.open();
    frame.contentDocument.write(`<!doctype html><html lang="es"><head><meta charset="utf-8"><title>Etiquetas</title>${fonts}<style>${SHEET_CSS}</style></head><body>${doc.html}</body></html>`);
    frame.contentDocument.close();
    const go = () => { frame.contentWindow.focus(); frame.contentWindow.print(); };
    (frame.contentDocument.fonts?.ready || Promise.resolve()).then(() => setTimeout(go, 150));
  }

  POS.labels = { open, sheets, sizes: SIZES, defaults: DEFAULTS, css: SHEET_CSS };
})();
