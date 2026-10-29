/* Variant picker: type a name, shade or code and choose a sellable presentation from the dropdown. */
(function () {
  const { api, esc, fmt, shade } = POS;

  function variantPicker(input, onPick) {
    const box = document.createElement('div');
    box.className = 'panel';
    box.style.cssText = 'position:absolute;z-index:30;left:0;right:0;top:100%;margin-top:4px;max-height:280px;overflow-y:auto;box-shadow:var(--shadow)';
    box.hidden = true;
    input.parentElement.style.position = 'relative';
    input.parentElement.appendChild(box);
    input.setAttribute('autocomplete', 'off');
    let list = [], timer;

    async function search() {
      const q = input.value.trim();
      if (!q) { box.hidden = true; return; }
      try { list = await api.get('/variants/search?q=' + encodeURIComponent(q)); } catch { list = []; }
      box.innerHTML = list.length ? list.map((v, i) => `
        <button type="button" data-i="${i}" style="display:flex;gap:10px;align-items:center;width:100%;padding:8px 12px;border:0;border-bottom:1px solid var(--line);background:none;color:var(--ink);text-align:left;cursor:pointer;font:inherit">
          ${shade.swatch(v.shadeCode, 'sm')}<span style="flex:1;min-width:0">${esc(v.displayName)}<br><span class="muted mono" style="font-size:.78rem">${esc(v.sku)}</span></span>
          <span class="muted num">${fmt.qty(v.stock)} pzas</span></button>`).join('')
        : '<div class="empty">Sin coincidencias.</div>';
      box.hidden = false;
    }
    input.addEventListener('input', () => { clearTimeout(timer); timer = setTimeout(search, 200); });
    input.addEventListener('keydown', e => { if (e.key === 'Escape') box.hidden = true; });
    box.addEventListener('click', e => {
      const b = e.target.closest('[data-i]');
      if (!b) return;
      const v = list[Number(b.dataset.i)];
      box.hidden = true;
      input.value = v.displayName;
      onPick(v);
    });
    document.addEventListener('click', e => { if (!box.contains(e.target) && e.target !== input) box.hidden = true; });
  }

  window.variantPicker = variantPicker;
})();
