/*
 * Builds the user manuals: docs/manual/*.md → InventoryPOS.API/wwwroot/docs/manuals/*.pdf (+ manuals.json).
 *
 * Screenshots are taken from the running app, so the manuals show the screens as they are. Run it against an EMPTY
 * database: the script creates a demo administrator and fills the store with example data.
 *
 *   1. Start the API on an empty database, for example:
 *        dotnet run --project InventoryPOS.API --launch-profile http -- --ConnectionStrings:Sqlite="Data Source=App_Data/manuals.db" --Database:BackupDirectory=App_Data/backups
 *   2. cd build/manuals && npm install && npx playwright install chromium
 *   3. npm run manuals            (MANUALS_BASE_URL defaults to http://localhost:5080)
 *
 * A manual's text is Markdown with a front matter (title, module, icon, summary); ![caption](shot:name) inserts the
 * screenshot named in SHOTS below.
 */
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');
const { marked } = require('marked');

const ROOT = path.resolve(__dirname, '..', '..');
const SOURCE = path.join(ROOT, 'docs', 'manual');
const OUTPUT = path.join(ROOT, 'InventoryPOS.API', 'wwwroot', 'docs', 'manuals');
const WORK = path.join(__dirname, 'out');
const BASE = (process.env.MANUALS_BASE_URL || 'http://localhost:5080').replace(/\/$/, '');
const USER = process.env.MANUALS_USER || 'admin';
const PASSWORD = process.env.MANUALS_PASSWORD || 'Demo2026';
const FONTS = path.join(ROOT, 'InventoryPOS.API', 'wwwroot', 'vendor', 'fonts');

// ── API helpers ──
let token = null;
async function call(method, url, body) {
  const res = await fetch(BASE + '/api' + url, {
    method,
    headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: 'Bearer ' + token } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  const text = await res.text();
  const data = text ? JSON.parse(text) : null;
  if (!res.ok) throw new Error(`${method} ${url} → ${res.status} ${data?.detail || data?.title || text}`);
  return data;
}

/** Creates the demo administrator on an empty database and fills the store with believable data. */
async function seed() {
  const { needsSetup } = await call('GET', '/auth/setup');
  if (!needsSetup) {
    token = (await call('POST', '/auth/login', { userName: USER, password: PASSWORD })).token;
    if ((await call('GET', '/products')).items?.length || (await call('GET', '/products')).length) return;
  } else {
    token = (await call('POST', '/auth/setup', { userName: USER, fullName: 'Andrea Morales', password: PASSWORD, businessName: 'Matiz Belleza' })).token;
  }

  await call('PUT', '/settings', {
    BusinessRfc: 'EKU9003173C9', BusinessLegalName: 'ESCUELA KEMPER URGATE', BusinessTaxRegime: '601', BusinessZipCode: '42501',
    BusinessAddress: 'Av. Juárez 120, Centro, Pachuca, Hgo.', BusinessPhone: '771 555 0142',
  });
  const brand = async name => (await call('POST', '/brands', { name })).id;
  const category = async (name, description) => (await call('POST', '/categories', { name, description })).id;
  const supplier = async (name, contactName, phone) => (await call('POST', '/suppliers', { name, contactName, phone })).id;
  const brands = { igora: await brand('Igora Royal'), majirel: await brand('Majirel'), koleston: await brand('Koleston Perfect'), olaplex: await brand('Olaplex') };
  // The categories every store starts with (Cabello › Tintes…), so labels show category and subcategory.
  const existing = await call('GET', '/categories');
  const cat = name => existing.find(c => c.name === name)?.id ?? category(name, null);
  const cats = { tintes: await cat('Tintes'), oxidantes: await cat('Decolorantes y oxidantes'), cuidado: await cat('Tratamientos') };
  const sups = { bella: await supplier('Distribuidora Bella', 'Laura Pérez', '55 4120 3311'), salon: await supplier('Salón Pro Mayoreo', 'Jorge Ruiz', '771 220 9080') };

  const product = async (name, brandId, categoryId, supplierId, tracksBatches, variants) =>
    (await call('POST', '/products', { name, brandId, categoryId, supplierId, tracksBatches, variants })).variants;
  const dye = (sku, shadeCode, shadeName, price) => ({ sku, shadeCode, shadeName, size: 60, unit: 'ml', price, cost: Math.round(price * 0.48), minStock: 3 });
  const igora = await product('Igora Royal', brands.igora, cats.tintes, sups.bella, true,
    [dye('IGR-61', '6.1', 'Rubio oscuro ceniza', 189.9), dye('IGR-71', '7.1', 'Rubio medio ceniza', 189.9), dye('IGR-81', '8.1', 'Rubio claro ceniza', 189.9), dye('IGR-50', '5.0', 'Castaño claro natural', 189.9)]);
  const majirel = await product('Majirel', brands.majirel, cats.tintes, sups.bella, true,
    [dye('MAJ-71', '7.1', 'Rubio ceniza', 215), dye('MAJ-43', '4.3', 'Castaño dorado', 215)]);
  const koleston = await product('Koleston Perfect', brands.koleston, cats.tintes, sups.salon, true, [dye('KOL-73', '7.3', 'Rubio medio dorado', 175)]);
  const oxid = await product('Oxidante en crema 20 vol', brands.igora, cats.oxidantes, sups.bella, true,
    [{ sku: 'OX-20-1L', size: 1, unit: 'l', price: 245, cost: 120, minStock: 2 }]);
  const olaplex = await product('Olaplex N.º 3', brands.olaplex, cats.cuidado, sups.salon, false,
    [{ sku: 'OLA-3', barcode: '896364002428', size: 100, unit: 'ml', price: 690, cost: 410, minStock: 2 }]);

  const inDays = d => new Date(Date.now() + d * 864e5).toISOString().slice(0, 10);
  const line = (v, quantity, unitCost, lotNumber, expires) => ({ variantId: v.id, quantity, unitCost, lotNumber, expirationDate: expires ? inDays(expires) : null });
  await call('POST', '/purchases', { supplierId: sups.bella, invoiceNumber: 'FB-20931', items: [
    line(igora[0], 6, 91, 'L2407A', 420), line(igora[1], 8, 91, 'L2407B', 420), line(igora[1], 2, 91, 'L2311C', 40),
    line(igora[2], 2, 91, 'L2408A', 380), line(igora[3], 5, 91, 'L2408B', 390), line(majirel[0], 4, 103, 'MJ5521', 300),
    line(majirel[1], 3, 103, 'MJ5530', 25), line(oxid[0], 6, 120, 'OX0912', 500)] });
  await call('POST', '/purchases', { supplierId: sups.salon, invoiceNumber: 'R-1187', items: [line(koleston[0], 1, 84, 'KP7731', 365), line(olaplex[0], 5, 410, null, null)] });

  const customer = async c => (await call('POST', '/customers', c)).id;
  const ana = await customer({ name: 'Ana López', phone: '771 104 2290', email: 'ana.lopez@example.com', notes: '7.1 con oxidante 20 vol; sensible al amoniaco',
    taxId: 'XOJI740919U48', legalName: 'INGRID XODAR JIMENEZ', taxRegime: '612', taxZipCode: '88965', cfdiUse: 'G03' });
  await customer({ name: 'Sofía Hernández', phone: '771 332 8810', notes: 'Rubio claro, matizar cada 6 semanas', acceptsPromotions: true });
  await customer({ name: 'Estética Luna', phone: '771 222 4500', email: 'compras@esteticaluna.example.com', type: 'Mayorista', discountPercent: 15, acceptsPromotions: true });
  await customer({ name: 'Salón Brillo', phone: '771 410 7788', type: 'Mayorista', discountPercent: 10, acceptsPromotions: true });
  await customer({ name: 'Carmen Ruiz', phone: '771 290 1133' });

  const session = await call('POST', '/cash-sessions/open', { openingAmount: 800, notes: 'Fondo de la mañana' });
  const sell = (items, payments, customerId = null, discount = 0) => call('POST', '/sales', {
    customerId, discount, notes: null, items: items.map(([v, quantity]) => ({ variantId: v.id, quantity, unitPrice: null, discount: 0 })), payments });
  await sell([[igora[1], 2], [oxid[0], 1]], [{ method: 'Efectivo', amount: 700 }], ana);
  await sell([[olaplex[0], 1]], [{ method: 'Tarjeta', amount: 690, reference: '482913' }]);
  await sell([[majirel[0], 1], [igora[0], 1]], [{ method: 'Transferencia', amount: 404.9, reference: 'SPEI 7781' }]);
  await sell([[igora[3], 1]], [{ method: 'Efectivo', amount: 200 }]);
  await sell([[igora[1], 1], [koleston[0], 1]], [{ method: 'Tarjeta', amount: 200 }, { method: 'Efectivo', amount: 170 }], null, 10);
  await call('POST', `/cash-sessions/${session.id}/movements`, { type: 'Retiro', amount: 150, reason: 'Pago de garrafón y limpieza' });

  // Test accounts (no real credentials): Facturama saved, Facturapi in use, so the invoice form can be shown.
  await call('PUT', '/invoicing/account', { provider: 'Facturama', sandbox: true, userName: 'demo', password: 'demo-no-real', defaultProductCode: '53131600', serie: 'F' });
  await call('PUT', '/invoicing/account', { provider: 'Facturapi', sandbox: true, userName: '', password: 'sk_test_demo_no_real', defaultProductCode: '53131600', serie: 'F' });
}

// ── Screenshots: what each manual shows. url is opened signed in; prepare runs before the capture. ──
const SHOTS = [
  { name: 'login', url: '/login.html', signedOut: true },
  { name: 'menu', url: '/dashboard.html', full: false },
  { name: 'pos', url: '/pos.html', prepare: async p => {
      await p.fill('#search', '7.1'); await p.waitForTimeout(900); await p.click('#results .shade-card'); await p.waitForTimeout(300);
      // A scanned lot label: the ticket line shows which lot it comes from.
      const code = await p.evaluate(async () => (await POS.api.get('/labels/product/' + (await POS.api.get('/products?q=Majirel'))[0].id))[0].code);
      await p.evaluate(() => document.activeElement?.blur()); await p.keyboard.type(code, { delay: 5 }); await p.keyboard.press('Enter');
      await p.waitForTimeout(700); } },
  { name: 'pos-pay', url: '/pos.html', prepare: async p => {
      await p.fill('#search', '7.1'); await p.waitForTimeout(900); await p.click('#results .shade-card'); await p.waitForTimeout(300);
      await p.click('#pay-btn'); await p.waitForTimeout(400); await p.fill('#pay-cash', '400'); await p.dispatchEvent('#pay-cash', 'input'); await p.waitForTimeout(300); } },
  { name: 'cash', url: '/cash.html' },
  { name: 'sales', url: '/sales.html' },
  { name: 'sale-detail', url: '/sales.html', prepare: async p => { await p.click('#list [data-id]'); await p.waitForTimeout(700); } },
  { name: 'invoice-form', url: '/sales.html', prepare: async p => {
      await p.locator('#list [data-id]').last().click(); await p.waitForTimeout(700); await p.click('#invoice-btn'); await p.waitForTimeout(900); } },
  { name: 'products', url: '/products.html' },
  { name: 'product-edit', url: '/products.html', prepare: async p => {
      await p.click('#new-product'); await p.waitForTimeout(500);
      await p.fill('#product-dialog [name=name]', 'Delineador líquido Precision');
      await p.selectOption('#p-category', { label: 'Maquillaje' }).catch(() => {});
      const row = p.locator('.vrow').first();
      await row.locator('[name=shadeName]').fill('Negro intenso');
      await row.locator('[name=size]').fill('2.5'); await row.locator('[name=unit]').selectOption('ml');
      await row.locator('[name=cost]').fill('68'); await row.locator('[name=price]').fill('149');
      await row.locator('[name=initialStock]').fill('12'); await p.waitForTimeout(300); } },
  { name: 'labels-dialog', url: '/products.html', prepare: async p => {
      await p.locator('.prod', { hasText: 'Igora Royal' }).first().locator('[data-labels]').click(); await p.waitForTimeout(900); } },
  { name: 'inventory', url: '/inventory.html' },
  { name: 'orders-suggested', url: '/orders.html', full: true },
  { name: 'orders-detail', url: '/orders.html#orders', prepare: async p => {
      // An order made from the suggestion, sent by WhatsApp, with its tracking number and a movement written by hand.
      await p.evaluate(async () => {
        if ((await POS.api.get('/orders')).length) return;
        const group = (await POS.api.get('/orders/suggestions')).suppliers.find(g => g.hasWhatsApp);
        const order = await POS.api.post('/orders', { supplierId: group.supplierId, notes: 'Entregar antes del viernes, por favor.',
          items: group.lines.map(l => ({ variantId: l.variantId, quantity: l.suggestedQuantity, unitCost: l.unitCost })) });
        await POS.api.post(`/orders/${order.id}/sent`);
        await POS.api.put(`/orders/${order.id}/tracking`, { carrier: 'estafeta', trackingNumber: '6015 8890 2231 4471 0098' });
        await POS.api.post(`/orders/${order.id}/events`, { text: 'Llegó al centro de distribución de Pachuca' });
      });
      await p.reload(); await p.waitForTimeout(1200); await p.click('#orders [data-id]'); await p.waitForTimeout(800); } },
  { name: 'purchases', url: '/purchases.html' },
  { name: 'catalogs', url: '/catalogs.html' },
  { name: 'customers', url: '/customers.html' },
  { name: 'customer-fiscal', url: '/customers.html', prepare: async p => { await p.click('[data-edit="0"]'); await p.waitForTimeout(600); await p.evaluate(() => document.querySelector('dialog[open] fieldset.fiscal')?.scrollIntoView()); } },
  { name: 'customer-wholesale', url: '/customers.html', prepare: async p => {
      await p.locator('#crud tbody tr', { hasText: 'Estética Luna' }).locator('[data-edit]').click(); await p.waitForTimeout(600); } },
  { name: 'suppliers', url: '/suppliers.html' },
  { name: 'pos-wholesale', url: '/pos.html', prepare: async p => {
      await p.fill('#search', '7.1'); await p.waitForTimeout(900); await p.click('#results .shade-card'); await p.click('#results .shade-card');
      await p.click('[data-ctype=Mayorista]'); await p.fill('#ws-search', 'luna'); await p.waitForTimeout(200); await p.click('[data-ws]'); await p.waitForTimeout(400); } },
  { name: 'promotions', url: '/promotions.html', prepare: async p => {
      // A promotion with a picture made on the spot, sent to the customers who agreed.
      const exists = await p.evaluate(async () => (await POS.api.get('/promotions')).length);
      if (!exists) {
        await p.click('#new-promo');
        await p.fill('#promo-form [name=title]', '2x1 en tintes este fin de semana');
        await p.fill('#promo-form [name=message]', 'Lleva dos tintes Igora Royal y paga uno. Válido sábado y domingo, hasta agotar existencias.');
        const png = await p.evaluate(() => { const c = Object.assign(document.createElement('canvas'), { width: 1200, height: 900 }); const g = c.getContext('2d');
          const gr = g.createLinearGradient(0, 0, 1200, 900); gr.addColorStop(0, '#9c1f5c'); gr.addColorStop(1, '#f2a65a'); g.fillStyle = gr; g.fillRect(0, 0, 1200, 900);
          g.fillStyle = '#fff'; g.font = 'bold 220px sans-serif'; g.textAlign = 'center'; g.fillText('2x1', 600, 470); g.font = '600 70px sans-serif'; g.fillText('en tintes', 600, 590);
          return c.toDataURL('image/png').split(',')[1]; });
        await p.setInputFiles('#promo-file', { name: 'promo.png', mimeType: 'image/png', buffer: Buffer.from(png, 'base64') });
        await p.waitForTimeout(500); await p.click('#promo-form button[type=submit]'); await p.waitForTimeout(1200);
        await p.evaluate(async () => { const promo = (await POS.api.get('/promotions'))[0]; const aud = await POS.api.get('/promotions/audience');
          const full = await POS.api.post(`/promotions/${promo.id}/recipients`, { customerIds: aud.customers.map(c => c.id) });
          await POS.api.put(`/promotions/${promo.id}/recipients/${full.recipients[0].id}`, { status: 'Enviado' }); });
        await p.reload(); await p.waitForTimeout(1500);
      } } },
  { name: 'promotions-send', url: '/promotions.html', prepare: async p => {
      await p.evaluate(() => document.querySelector('.send-modes')?.scrollIntoView({ block: 'start' })); await p.waitForTimeout(300); } },
  { name: 'dashboard', url: '/dashboard.html' },
  { name: 'reports', url: '/reports.html' },
  { name: 'settings-business', url: '/settings.html#business' },
  { name: 'settings-invoicing', url: '/settings.html#invoicing', full: true },
  { name: 'settings-printer', url: '/settings.html#printer' },
  { name: 'settings-users', url: '/settings.html#users' },
  { name: 'settings-sync', url: '/settings.html#sync' },
  { name: 'docs', url: '/documentation.html', afterRender: true },
];

async function screenshots(browser, afterRender = false) {
  fs.mkdirSync(path.join(WORK, 'shots'), { recursive: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 820 }, deviceScaleFactor: 1.5 });
  const page = await context.newPage();
  await page.goto(BASE + '/login.html');
  await page.evaluate(([t]) => { try { sessionStorage.clear(); } catch {} }, [token]);
  const wanted = SHOTS.filter(s => !!s.afterRender === afterRender);
  for (const shot of wanted.filter(s => s.signedOut)) await capture(page, shot);
  await page.fill('#userName', USER); await page.fill('#password', PASSWORD); await page.click('#login-btn');
  await page.waitForTimeout(1500);
  for (const shot of wanted.filter(s => !s.signedOut)) await capture(page, shot);
  await context.close();
}

async function capture(page, shot) {
  // A blank page first: going from settings.html#a to settings.html#b would only change the hash, not reload.
  await page.goto('about:blank');
  await page.goto(BASE + shot.url);
  await page.waitForTimeout(1300);
  try { if (shot.prepare) await shot.prepare(page); }
  catch (err) { console.warn(`  ! ${shot.name}: ${err.message.split('\n')[0]}`); }
  // JPEG keeps each manual light enough to ship inside the program.
  await page.screenshot({ path: path.join(WORK, 'shots', shot.name + '.jpg'), fullPage: !!shot.full, type: 'jpeg', quality: 80 });
  console.log(`  captura ${shot.name}`);
}

// ── Manual rendering ──
function parse(file) {
  const raw = fs.readFileSync(path.join(SOURCE, file), 'utf8');
  const match = raw.match(/^---\n([\s\S]*?)\n---\n([\s\S]*)$/);
  const meta = Object.fromEntries(match[1].split('\n').map(l => l.split(/:\s(.*)/s).slice(0, 2)).map(([k, v]) => [k.trim(), v.trim()]));
  return { slug: file.replace(/\.md$/, '').replace(/^\d+-/, ''), order: parseInt(file, 10), file, ...meta, body: match[2] };
}

const dataUri = (file, type) => `data:${type};base64,${fs.readFileSync(file).toString('base64')}`;
const shotUri = name => {
  const file = path.join(WORK, 'shots', name + '.jpg');
  // The documentation screen is captured after a first render (it lists the PDFs); until then a blank placeholder is used.
  if (!fs.existsSync(file)) return 'data:image/gif;base64,R0lGODlhAQABAAAAACw=';
  return dataUri(file, 'image/jpeg');
};

/** Keeps each heading on the same page as the block that follows it (Chromium ignores break-after on headings). */
const keepHeadings = body => body.replace(
  /(<h[23][^>]*>[\s\S]*?<\/h[23]>)\s*(<(p|figure|ul|ol|table|blockquote)[\s>][\s\S]*?<\/\3>)/g,
  '<div class="keep">$1$2</div>');

function html(manuals, { title, subtitle }) {
  const font = (file, family, weight) => `@font-face{font-family:'${family}';src:url(${dataUri(path.join(FONTS, file), 'font/woff2')}) format('woff2');font-weight:${weight};}`;
  const fonts = font('instrument-sans-400-700-latin.woff2', 'Instrument Sans', '400 700') + font('instrument-sans-400-700-latin-ext.woff2', 'Instrument Sans', '400 700')
    + font('bricolage-grotesque-500-750-latin.woff2', 'Bricolage Grotesque', '500 750') + font('bricolage-grotesque-500-750-latin-ext.woff2', 'Bricolage Grotesque', '500 750')
    + font('ibm-plex-mono-400-latin.woff2', 'IBM Plex Mono', 400);
  const renderer = new marked.Renderer();
  renderer.image = ({ href, text }) => href.startsWith('shot:')
    ? `<figure><img src="${shotUri(href.slice(5))}" alt="${text}"><figcaption>${text}</figcaption></figure>`
    : `<img src="${href}" alt="${text}">`;
  const sections = manuals.map((m, i) => `
    <section class="manual ${i ? 'break' : ''}">
      <header class="cover"><div class="eyebrow">Manual de usuario · Matiz POS</div><h1>${m.title}</h1><p>${m.summary}</p></header>
      ${keepHeadings(marked.parse(m.body, { renderer }))}
    </section>`).join('');
  const toc = manuals.length > 1 ? `<section class="toc"><div class="eyebrow">Matiz POS</div><h1>${title}</h1><p class="lead">${subtitle}</p>
      <h2>Contenido</h2><ol>${manuals.map(m => `<li><b>${m.title}</b><span>${m.summary}</span></li>`).join('')}</ol></section>` : '';
  return `<!doctype html><html lang="es"><head><meta charset="utf-8"><style>${fonts}
    :root { --ink:#221a20; --ink-2:#4a3f47; --muted:#7a6f77; --line:#e6dfe3; --accent:#9c1f5c; --soft:#f6eef2; }
    @page { size: Letter; margin: 18mm 16mm 20mm; }
    body { font-family:'Instrument Sans',sans-serif; color:var(--ink); font-size:10.5pt; line-height:1.5; margin:0; }
    h1,h2,h3 { font-family:'Bricolage Grotesque',sans-serif; line-height:1.15; color:var(--ink); }
    h1 { font-size:26pt; margin:4px 0 8px; } h2 { font-size:15pt; margin:22px 0 6px; padding-top:6px; border-top:1px solid var(--line); }
    h3 { font-size:11.5pt; margin:14px 0 4px; } p { margin:6px 0; } ul,ol { padding-left:20px; margin:6px 0; } li { margin:3px 0; }
    code, kbd { font-family:'IBM Plex Mono',monospace; font-size:9pt; background:var(--soft); padding:1px 5px; border-radius:4px; }
    kbd { border:1px solid var(--line); }
    .eyebrow { text-transform:uppercase; letter-spacing:.12em; font-size:8pt; color:var(--accent); font-weight:600; }
    .cover { border-bottom:3px solid var(--accent); padding-bottom:10px; margin-bottom:8px; } .cover p, .lead { color:var(--ink-2); font-size:11.5pt; margin:0; }
    .break { break-before:page; } h2, h3 { break-after:avoid; }
    figure { margin:10px 0 14px; break-inside:avoid; text-align:center; }
    figure img { max-width:100%; max-height:165mm; border:1px solid var(--line); border-radius:6px; }
    .keep { break-inside:avoid; }
    figcaption { font-size:8.5pt; color:var(--muted); margin-top:4px; }
    blockquote { margin:10px 0; padding:8px 12px; background:var(--soft); border-left:3px solid var(--accent); border-radius:4px; break-inside:avoid; }
    blockquote p { margin:2px 0; } table { border-collapse:collapse; width:100%; margin:8px 0; font-size:9.5pt; break-inside:avoid; }
    th, td { border-bottom:1px solid var(--line); padding:5px 6px; text-align:left; vertical-align:top; } th { color:var(--ink-2); font-weight:600; }
    .toc ol { list-style:none; padding:0; counter-reset:m; } .toc li { counter-increment:m; display:grid; grid-template-columns:28px 1fr; padding:6px 0; border-bottom:1px solid var(--line); }
    .toc li::before { content:counter(m); color:var(--accent); font-weight:700; grid-row:span 2; } .toc li span { color:var(--muted); font-size:9.5pt; }
    .toc { break-after:page; }
  </style></head><body>${toc}${sections}</body></html>`;
}

async function pdf(browser, manuals, file, meta) {
  const page = await browser.newPage();
  await page.setContent(html(manuals, meta), { waitUntil: 'load' });
  await page.pdf({
    path: file, format: 'Letter', printBackground: true, displayHeaderFooter: true,
    headerTemplate: '<span></span>',
    footerTemplate: `<div style="font-family:sans-serif;font-size:7pt;color:#7a6f77;width:100%;padding:0 16mm;display:flex;justify-content:space-between">
      <span>Matiz POS · ${meta.footer}</span><span>Página <span class="pageNumber"></span> de <span class="totalPages"></span></span></div>`,
    margin: { top: '18mm', bottom: '20mm', left: '16mm', right: '16mm' },
  });
  await page.close();
}

(async () => {
  console.log(`Datos de ejemplo en ${BASE}…`);
  await seed();
  const browser = await chromium.launch();
  console.log('Capturas…');
  await screenshots(browser);

  const manuals = fs.readdirSync(SOURCE).filter(f => f.endsWith('.md')).sort().map(parse);
  fs.mkdirSync(OUTPUT, { recursive: true });
  for (const f of fs.readdirSync(path.join(WORK, 'shots'))) if (SHOTS.some(s => s.afterRender && f.startsWith(s.name + '.'))) fs.unlinkSync(path.join(WORK, 'shots', f));
  await render(browser, manuals);
  await screenshots(browser, true);
  await render(browser, manuals);
  await browser.close();
  console.log(`Listo: ${manuals.length + 1} PDF en ${path.relative(ROOT, OUTPUT)}`);
})().catch(err => { console.error(err); process.exit(1); });

async function render(browser, manuals) {
  for (const f of fs.readdirSync(OUTPUT)) if (f.endsWith('.pdf')) fs.unlinkSync(path.join(OUTPUT, f));
  const index = [];
  for (const m of manuals) {
    const file = `${String(m.order).padStart(2, '0')}-${m.slug}.pdf`;
    await pdf(browser, [m], path.join(OUTPUT, file), { title: m.title, footer: m.title });
    index.push({ slug: m.slug, title: m.title, module: m.module === '-' ? null : m.module, icon: m.icon, summary: m.summary, file,
      size: fs.statSync(path.join(OUTPUT, file)).size });
    console.log(`  manual ${file}`);
  }
  const complete = 'manual-completo.pdf';
  await pdf(browser, manuals, path.join(OUTPUT, complete), { title: 'Manual de usuario', subtitle: 'Todos los módulos en un solo documento.', footer: 'Manual completo' });
  index.push({ slug: 'completo', title: 'Manual completo', module: null, icon: 'bi-book', summary: 'Todos los manuales en un solo documento, con índice.',
    file: complete, size: fs.statSync(path.join(OUTPUT, complete)).size, complete: true });
  fs.writeFileSync(path.join(OUTPUT, 'manuals.json'), JSON.stringify({ generatedAt: new Date().toISOString(), manuals: index }, null, 2) + '\n');
}
