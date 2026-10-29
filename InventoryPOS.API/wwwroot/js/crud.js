/* Table + dialog for simple catalogs (customers, suppliers, brands, categories). */
(function () {
  const { api, esc, toast, fail, confirmBox, formData, fillForm } = POS;

  /**
   * config: { mount, endpoint, noun, columns: [{ label, cell(row), num }], fields: html, search, canEdit,
   *           deleteText(row), toForm(row), emptyText, extraQuery() }
   */
  function crud(config) {
    const root = config.mount;
    root.innerHTML = `
      <div class="toolbar" style="margin-bottom:12px">
        ${config.search ? '<label class="field grow"><span>Buscar</span><input type="search" data-q placeholder="' + esc(config.search) + '"></label>' : '<span class="grow"></span>'}
        <label class="check"><input type="checkbox" data-inactive> Mostrar inactivos</label>
        ${config.canEdit ? `<button class="btn primary" type="button" data-new><i class="bi bi-plus-lg"></i>Agregar ${esc(config.noun)}</button>` : ''}
      </div>
      <section class="panel"><div class="table-wrap"><table>
        <thead><tr>${config.columns.map(c => `<th class="${c.num ? 'num' : ''}">${esc(c.label)}</th>`).join('')}<th></th></tr></thead>
        <tbody data-rows></tbody></table></div></section>
      <dialog aria-label="${esc(config.noun)}"><form method="dialog">
        <header><h2 data-title></h2><button class="btn ghost icon-btn" value="cancel" formnovalidate aria-label="Cerrar"><i class="bi bi-x-lg"></i></button></header>
        <div class="body"><div class="form-grid">${config.fields}
          <label class="check span-all"><input type="checkbox" name="isActive" checked> Activo</label></div>
          <div class="form-error" hidden></div></div>
        <footer><button class="btn ghost" value="cancel" formnovalidate>Cancelar</button><button class="btn primary" value="save">Guardar</button></footer>
      </form></dialog>`;

    const q = root.querySelector('[data-q]');
    const inactive = root.querySelector('[data-inactive]');
    const tbody = root.querySelector('[data-rows]');
    const dlg = root.querySelector('dialog');
    const form = dlg.querySelector('form');
    let rows = [], editing = null;

    async function load() {
      const qs = new URLSearchParams();
      if (q?.value.trim()) qs.set('q', q.value.trim());
      if (inactive.checked) qs.set('includeInactive', 'true');
      rows = await api.get(`${config.endpoint}?${qs}`);
      tbody.innerHTML = rows.length ? rows.map((r, i) => `
        <tr class="${r.isActive ? '' : 'inactive'}">
          ${config.columns.map(c => `<td class="${c.num ? 'num' : ''}">${c.cell(r)}</td>`).join('')}
          <td class="num">${config.canEdit ? `<button class="btn ghost sm" type="button" data-edit="${i}">Editar</button>
            ${r.isActive ? `<button class="btn ghost sm danger" type="button" data-del="${i}">Desactivar</button>` : '<span class="pill">Inactivo</span>'}` : ''}</td>
        </tr>`).join('')
        : `<tr><td colspan="${config.columns.length + 1}" class="empty">${esc(q?.value ? 'Sin coincidencias.' : config.emptyText)}</td></tr>`;
    }

    function open(row) {
      editing = row;
      form.reset();
      dlg.querySelector('.form-error').hidden = true;
      dlg.querySelector('[data-title]').textContent = row ? `Editar ${config.noun}` : `Agregar ${config.noun}`;
      fillForm(form, row ? (config.toForm ? config.toForm(row) : row) : { isActive: true });
      dlg.showModal();
      form.querySelector('input,select')?.focus();
    }

    form.addEventListener('submit', async e => {
      e.preventDefault();
      if (e.submitter?.value === 'cancel') { dlg.close(); return; }
      const err = dlg.querySelector('.form-error');
      try {
        const data = formData(form);
        if (editing) await api.put(`${config.endpoint}/${editing.id}`, data);
        else await api.post(config.endpoint, data);
        toast(editing ? 'Cambios guardados.' : `${config.noun[0].toUpperCase()}${config.noun.slice(1)} agregado.`);
        dlg.close();
        await load();
      } catch (ex) { err.textContent = ex.message; err.hidden = false; }
    });

    root.addEventListener('click', async e => {
      if (e.target.closest('[data-new]')) open(null);
      const edit = e.target.closest('[data-edit]');
      if (edit) open(rows[Number(edit.dataset.edit)]);
      const del = e.target.closest('[data-del]');
      if (del) {
        const row = rows[Number(del.dataset.del)];
        if (!await confirmBox(config.deleteText(row), { okText: 'Desactivar', danger: true })) return;
        try { await api.del(`${config.endpoint}/${row.id}`); toast('Listo.'); await load(); } catch (ex) { fail(ex); }
      }
    });

    let timer;
    q?.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(load, 250); });
    inactive.addEventListener('change', load);
    return { load };
  }

  window.crud = crud;
})();
