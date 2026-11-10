(() => {
    const form = document.querySelector('[data-shared-finance-form]');
    if (!form) return;
    const type = form.elements.FinanceType;
    const month = form.elements.Month;
    const monthGroup = form.querySelector('[data-shared-budget-month]');
    const amountGroup = form.querySelector('[data-shared-amount-group]');
    function updateType() {
        const budget = type.value === 'MonthlyBudget';
        monthGroup.hidden = !budget;
        month.disabled = !budget;
        amountGroup.classList.toggle('col-md-6', budget);
        form.querySelector('label[for="Amount"]').textContent = budget ? 'Budget limit (RM)' : 'Savings target (RM)';
    }
    type.addEventListener('change', updateType);
    updateType();
})();
(() => {
    const form = document.querySelector('[data-shared-filters]');
    if (!form || !window.fetch || !window.AbortController) return;
    const resultId = form.dataset.results;
    const error = document.querySelector('[data-shared-error]');
    let sequence = 0, active, timer;
    function sync(url, results) {
        const query = new URL(url, location.href).searchParams;
        for (const field of form.querySelectorAll('input:not([type="hidden"]),select')) {
            const fallback = field.name === 'month' ? (results?.dataset.month || '') : ['type', 'scope'].includes(field.name) ? 'all' : '';
            field.value = query.get(field.name) || fallback;
        }
    }
    async function load(url, push = true) {
        clearTimeout(timer);
        active?.abort(); active = new AbortController();
        const request = ++sequence;
        const old = document.getElementById(resultId);
        old.setAttribute('aria-busy', 'true'); error.hidden = true;
        try {
            const response = await fetch(url, { signal: active.signal, credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'text/html' } });
            if (!response.ok) throw new Error('Request failed');
            const html = await response.text();
            if (request !== sequence) return;
            const next = new DOMParser().parseFromString(html, 'text/html').getElementById(resultId);
            if (!next) {
                if (response.redirected) { location.assign(response.url); return; }
                throw new Error('Unexpected response');
            }
            old.replaceWith(next); sync(url, next);
            if (push) history.pushState(null, '', url);
        } catch (e) {
            if (e.name !== 'AbortError' && request === sequence) error.hidden = false;
        } finally {
            if (request === sequence) document.getElementById(resultId).removeAttribute('aria-busy');
        }
    }
    function submit() {
        if (!form.reportValidity()) return;
        const url = new URL(form.action, location.href);
        url.search = new URLSearchParams(new FormData(form)).toString();
        load(url.href);
    }
    form.addEventListener('submit', event => { event.preventDefault(); submit(); });
    for (const field of form.querySelectorAll('select,input[type="month"]')) field.addEventListener('change', submit);
    form.elements.search?.addEventListener('input', () => {
        clearTimeout(timer); ++sequence; active?.abort(); timer = setTimeout(submit, 350);
    });
    document.addEventListener('click', event => {
        const link = event.target.closest(`[data-shared-reset],#${resultId} nav a`);
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        event.preventDefault(); load(link.href);
    });
    window.addEventListener('popstate', () => load(location.href, false));
    form.querySelector('[data-shared-submit]').hidden = true;
})();
