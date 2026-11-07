(() => {
    const form = document.getElementById('billHistoryFilters');
    if (!form || !window.fetch || !window.AbortController) return;
    const error = document.getElementById('billHistoryError');
    let active, sequence = 0, timer;
    function sync(url) {
        const q = new URL(url, location.href).searchParams;
        for (const name of ['search', 'month', 'method']) form.elements[name].value = q.get(name) || '';
        let exact = form.elements.expenseId;
        if (q.has('expenseId')) {
            if (!exact) { exact = document.createElement('input'); exact.type = 'hidden'; exact.name = 'expenseId'; form.append(exact); }
            exact.value = q.get('expenseId');
        } else exact?.remove();
    }
    async function load(url, push = true) {
        clearTimeout(timer);
        const current = ++sequence;
        active?.abort(); active = new AbortController();
        const old = document.getElementById('billHistoryResults');
        old.setAttribute('aria-busy', 'true'); error.hidden = true;
        try {
            const response = await fetch(url, { signal: active.signal, credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok) throw new Error();
            const html = await response.text();
            if (current !== sequence) return;
            const next = new DOMParser().parseFromString(html, 'text/html').getElementById('billHistoryResults');
            if (!next) { if(response.redirected) { location.assign(response.url); return; } throw new Error(); }
            old.replaceWith(next); sync(url);
            if (push) history.pushState(null, '', url);
        } catch (e) { if (e.name !== 'AbortError' && current === sequence) error.hidden = false; }
        finally { if(current === sequence) document.getElementById('billHistoryResults').removeAttribute('aria-busy'); }
    }
    function submit() {
        if (!form.reportValidity()) return;
        const url = new URL(form.action); url.search = new URLSearchParams(new FormData(form)).toString();
        load(url.href);
    }
    form.addEventListener('submit', e => { e.preventDefault(); submit(); });
    form.elements.search.addEventListener('input', () => {
        clearTimeout(timer); ++sequence; active?.abort();
        timer = setTimeout(submit, 350);
    });
    for (const name of ['month', 'method']) form.elements[name].addEventListener('change', submit);
    document.addEventListener('click', e => {
        const a = e.target.closest('#billHistoryResults nav a, #historyReset');
        if (!a || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
        e.preventDefault(); load(a.href);
    });
    window.addEventListener('popstate', () => { sync(location.href); load(location.href, false); });
    document.getElementById('historyFilterSubmit').hidden = true;
})();
