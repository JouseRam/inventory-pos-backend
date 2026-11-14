/* Invoice dialog for a sale: issue the CFDI, download it, or cancel it. Used by the register and the sales history. */
(function () {
  const { api, esc, fmt, toast, session } = POS;
  let sat = null;
  const catalogs = async () => sat ??= await api.get('/sat/catalogs');
  const option = (o, selected) =>
    `<option value="${esc(o.code)}" data-individual="${o.individual}" data-company="${o.company}" ${o.code === selected ? 'selected' : ''}>${esc(o.code)} · ${esc(o.description)}</option>`;

  function dialog() {
    let dlg = document.getElementById('invoice-dialog');
    if (dlg) return dlg;
    document.body.insertAdjacentHTML('beforeend', `
      <dialog id="invoice-dialog" aria-labelledby="invoice-title"><form method="dialog" novalidate>
        <header><h2 id="invoice-title">Facturar venta</h2><button class="btn ghost icon-btn" value="cancel" formnovalidate aria-label="Cerrar"><i class="bi bi-x-lg"></i></button></header>
        <div class="body" data-body></div>
        <footer data-footer></footer>
      </form></dialog>`);
    return document.getElementById('invoice-dialog');
  }

  /** Opens the dialog for a sale. onChange runs after the invoice is issued or cancelled. */
  async function open(saleId, { onChange } = {}) {
    const dlg = dialog();
    const body = dlg.querySelector('[data-body]');
    const footer = dlg.querySelector('[data-footer]');
    const canEdit = session.can('Invoicing', true);
    body.innerHTML = '<p class="muted">Cargando…</p>';
    footer.innerHTML = '';
    if (!dlg.open) dlg.showModal();

    let draft;
    try { [draft] = await Promise.all([api.get(`/invoices/draft/${saleId}`), catalogs()]); }
    catch (err) { body.innerHTML = `<div class="form-error">${esc(err.message)}</div>`; footer.innerHTML = '<button class="btn" value="cancel">Cerrar</button>'; return; }

    const title = dlg.querySelector('#invoice-title');
    const sandbox = draft.sandbox ? ' <span class="pill warn" title="Facturas de prueba: no tienen validez ante el SAT">Modo de pruebas</span>' : '';
    if (draft.invoice) return showInvoice(draft.invoice);

    title.innerHTML = `Facturar venta <span class="mono">${esc(draft.saleFolio)}</span>${sandbox}`;
    if (draft.missingSetup.length) {
      body.innerHTML = `
        <div class="notice warn"><i class="bi bi-exclamation-triangle"></i><div>
          <b>Falta configurar la facturación</b>
          <ul>${draft.missingSetup.map(m => `<li>${esc(m)}</li>`).join('')}</ul>
          ${session.can('Settings', true) ? '<a href="settings.html#invoicing">Ir a Configuración → Facturación</a>' : 'Pide a un administrador que la configure.'}
        </div></div>`;
      footer.innerHTML = '<button class="btn" value="cancel">Cerrar</button>';
      return;
    }
    if (!canEdit) {
      body.innerHTML = '<p>Esta venta no está facturada. No tienes permiso para emitir facturas.</p>';
      footer.innerHTML = '<button class="btn" value="cancel">Cerrar</button>';
      return;
    }

    body.innerHTML = `
      <div class="notice warn" data-offline hidden><i class="bi bi-wifi-off"></i><div><b>Sin conexión a internet.</b> Para facturar necesitas estar conectado. La venta ya quedó registrada; puedes facturarla cuando regrese la conexión.</div></div>
      <p class="muted" style="margin:0 0 12px">Total <b class="mono">${fmt.money(draft.total)}</b>. Los datos deben coincidir con la constancia de situación fiscal del cliente.</p>
      <div class="form-grid">
        <label class="field"><span>RFC</span><input name="taxId" maxlength="13" class="mono" required autocapitalize="characters" value="${esc(draft.taxId || '')}"></label>
        <label class="field"><span>Código postal fiscal</span><input name="taxZipCode" maxlength="5" inputmode="numeric" class="mono" required value="${esc(draft.taxZipCode || '')}"></label>
        <label class="field span-all"><span>Nombre o razón social</span><input name="legalName" maxlength="300" required value="${esc(draft.legalName || draft.customerName || '')}"></label>
        <label class="field span-all"><span>Régimen fiscal</span><select name="taxRegime" required><option value="">Elige…</option>${sat.taxRegimes.map(o => option(o, draft.taxRegime)).join('')}</select></label>
        <label class="field"><span>Uso del CFDI</span><select name="cfdiUse" required><option value="">Elige…</option>${sat.cfdiUses.map(o => option(o, draft.cfdiUse || 'G03')).join('')}</select></label>
        <label class="field"><span>Forma de pago</span><select name="paymentForm">${sat.paymentForms.map(o => option(o, draft.paymentForm)).join('')}</select></label>
        <label class="field span-all"><span>Correo del cliente (opcional)</span><input name="email" type="email" maxlength="150" placeholder="Se guarda en el cliente para enviarle sus facturas"></label>
      </div>
      <div class="form-error" hidden data-error></div>`;
    footer.innerHTML = `<button class="btn ghost" value="cancel" formnovalidate>Cancelar</button>
      <button class="btn primary" type="submit" data-issue><i class="bi bi-receipt"></i>Timbrar factura</button>`;

    const form = dlg.querySelector('form');
    const el = name => form.elements[name];
    const issue = footer.querySelector('[data-issue]');
    const error = body.querySelector('[data-error]');

    // Only the regimes and uses that fit the RFC typed (12 = persona moral, 13 = persona física).
    const fit = () => {
      const rfc = el('taxId').value.trim().toUpperCase();
      const kind = rfc.length === 12 ? 'company' : rfc.length === 13 ? 'individual' : null;
      for (const sel of [el('taxRegime'), el('cfdiUse')]) {
        for (const o of sel.options) if (o.value) o.hidden = kind !== null && o.dataset[kind] !== 'true';
        if (sel.selectedOptions[0]?.hidden) sel.value = '';
      }
    };
    // A customer already saved with this RFC fills in the rest.
    const lookup = async () => {
      const rfc = el('taxId').value.trim().toUpperCase();
      if (rfc.length < 12 || (el('legalName').value && el('taxRegime').value)) return;
      const match = (await api.get('/customers?q=' + encodeURIComponent(rfc)).catch(() => [])).find(c => c.taxId === rfc);
      if (!match) return;
      el('legalName').value = match.legalName || ''; el('taxRegime').value = match.taxRegime || '';
      el('taxZipCode').value = match.taxZipCode || ''; el('cfdiUse').value = match.cfdiUse || el('cfdiUse').value;
      el('email').value = match.email || '';
    };
    el('taxId').addEventListener('input', () => { el('taxId').value = el('taxId').value.toUpperCase(); fit(); });
    el('taxId').addEventListener('change', lookup);
    fit();

    const offline = body.querySelector('[data-offline]');
    const connectivity = () => { offline.hidden = navigator.onLine; issue.disabled = !navigator.onLine; };
    connectivity();
    window.addEventListener('online', connectivity);
    window.addEventListener('offline', connectivity);
    dlg.addEventListener('close', () => {
      window.removeEventListener('online', connectivity);
      window.removeEventListener('offline', connectivity);
    }, { once: true });

    form.onsubmit = async e => {
      e.preventDefault();
      if (e.submitter?.value === 'cancel') { dlg.close(); return; }
      if (!navigator.onLine) { connectivity(); return; }
      error.hidden = true;
      issue.disabled = true;
      issue.innerHTML = '<i class="bi bi-hourglass-split"></i>Timbrando…';
      try {
        const data = {
          customerId: draft.customerId,
          taxId: el('taxId').value.trim(), legalName: el('legalName').value.trim(), taxRegime: el('taxRegime').value,
          taxZipCode: el('taxZipCode').value.trim(), cfdiUse: el('cfdiUse').value, paymentForm: el('paymentForm').value,
          email: el('email').value.trim() || null,
        };
        const invoice = await api.post(`/invoices/sale/${saleId}`, data);
        toast('Factura timbrada.');
        onChange?.(invoice);
        showInvoice(invoice);
      } catch (err) {
        error.textContent = err.status === 503 ? err.message : `No se pudo timbrar: ${err.message}`;
        error.hidden = false;
        issue.disabled = false;
        issue.innerHTML = '<i class="bi bi-receipt"></i>Timbrar factura';
      }
    };

    function showInvoice(inv) {
      const cancelled = inv.status === 'Cancelada';
      title.innerHTML = `Factura de la venta <span class="mono">${esc(inv.saleFolio || draft.saleFolio)}</span> ${cancelled ? '<span class="pill">Cancelada</span>' : '<span class="pill good">Vigente</span>'}${inv.sandbox ? ' <span class="pill warn">Prueba</span>' : ''}`;
      body.innerHTML = `
        <dl class="facts">
          <dt>Folio fiscal (UUID)</dt><dd class="mono">${esc(inv.uuid)}</dd>
          <dt>Cliente</dt><dd>${esc(inv.receiverName)} · <span class="mono">${esc(inv.receiverRfc)}</span></dd>
          <dt>Total</dt><dd class="mono">${fmt.money(inv.total)}</dd>
          <dt>Emitida</dt><dd>${fmt.dateTime(inv.createdAt)}</dd>
          ${cancelled ? `<dt>Cancelada</dt><dd>${fmt.dateTime(inv.cancelledAt)} · motivo ${esc(inv.cancelMotive || '')}</dd>` : ''}
        </dl>
        ${!cancelled && canEdit ? `
        <details class="cancel-invoice"><summary>Cancelar esta factura ante el SAT</summary>
          <div class="form-grid" style="margin-top:10px">
            <label class="field span-all"><span>Motivo</span><select data-motive>${sat.cancelMotives.map(o => option(o, '02')).join('')}</select></label>
            <label class="field span-all" data-replacement hidden><span>UUID de la factura que la sustituye</span><input class="mono" maxlength="36" data-uuid></label>
            <div><button class="btn danger" type="button" data-cancel-invoice><i class="bi bi-x-octagon"></i>Cancelar factura</button></div>
          </div>
        </details>` : ''}
        <div class="form-error" hidden data-error></div>`;
      footer.innerHTML = `
        <button class="btn" type="button" data-pdf><i class="bi bi-file-earmark-pdf"></i>PDF</button>
        <button class="btn" type="button" data-xml><i class="bi bi-filetype-xml"></i>XML</button>
        <button class="btn primary" value="cancel">Listo</button>`;
      const err = body.querySelector('[data-error]');
      const show = e => { err.textContent = e.message; err.hidden = false; };
      footer.querySelector('[data-pdf]').onclick = () => api.download(`/invoices/${inv.id}/pdf`, `${inv.uuid}.pdf`).catch(show);
      footer.querySelector('[data-xml]').onclick = () => api.download(`/invoices/${inv.id}/xml`, `${inv.uuid}.xml`).catch(show);
      const motive = body.querySelector('[data-motive]');
      if (!motive) return;
      motive.onchange = () => { body.querySelector('[data-replacement]').hidden = motive.value !== '01'; };
      body.querySelector('[data-cancel-invoice]').onclick = async () => {
        if (!navigator.onLine) { show(new Error('Sin conexión a internet: para cancelar una factura necesitas estar conectado.')); return; }
        if (!await POS.confirmBox(`Cancelar la factura ${inv.uuid} ante el SAT. Esta acción no se puede deshacer.`, { okText: 'Cancelar factura', danger: true })) return;
        try {
          const result = await api.post(`/invoices/${inv.id}/cancel`, { motive: motive.value, replacementUuid: body.querySelector('[data-uuid]').value.trim() || null });
          toast(result.message);
          onChange?.(result.invoice);
          showInvoice(result.invoice);
        } catch (e) { show(e); }
      };
    }
  }

  POS.invoice = { open };
})();
