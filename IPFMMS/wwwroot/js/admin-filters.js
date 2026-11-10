(() => {
    'use strict';
    if (!window.fetch || !window.AbortController) return;
    document.querySelectorAll('form[data-admin-filters]').forEach(form => {
        const resultId = form.dataset.results;
        const error = form.querySelector('[data-admin-error]');
        if (!resultId || !document.getElementById(resultId) || !error) return;
        let sequence = 0, active, timer;
        const defaults = new Map(Array.from(form.elements).filter(e => e.name).map(e => [e.name, e.dataset.adminDefault ?? (e.type === 'hidden' ? e.value : e.tagName === 'SELECT' ? e.options[0]?.value ?? '' : '')]));
        function sync(url) {
            const query = new URL(url, location.href).searchParams;
            for (const element of form.elements) {
                if (element.name && element.type !== 'submit') element.value = query.get(element.name) ?? defaults.get(element.name) ?? '';
            }
        }
        async function load(url, push = true) {
            clearTimeout(timer);
            active?.abort();
            active = new AbortController();
            const request = ++sequence;
            const previous = document.getElementById(resultId);
            previous.setAttribute('aria-busy', 'true');
            error.hidden = true;
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
                previous.replaceWith(next);
                sync(url);
                if (push) history.pushState(null, '', url);
            } catch (exception) {
                if (exception.name !== 'AbortError' && request === sequence) error.hidden = false;
            } finally {
                if (request === sequence) document.getElementById(resultId)?.removeAttribute('aria-busy');
            }
        }
        function submit() {
            if (!form.reportValidity()) return;
            const url = new URL(form.action, location.href);
            url.search = new URLSearchParams(new FormData(form)).toString();
            load(url.href);
        }
        form.addEventListener('submit', event => { event.preventDefault(); submit(); });
        form.querySelectorAll('input[type="search"]').forEach(input => input.addEventListener('input', () => {
            clearTimeout(timer); ++sequence; active?.abort();
            document.getElementById(resultId)?.removeAttribute('aria-busy');
            timer = setTimeout(submit, 350);
        }));
        form.querySelectorAll('select').forEach(select => select.addEventListener('change', submit));
        document.addEventListener('click', event => {
            const anchor = event.target.closest('a[data-admin-page], a[data-admin-reset]');
            if (!anchor || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
            if (!form.contains(anchor) && !document.getElementById(resultId)?.contains(anchor)) return;
            event.preventDefault(); load(anchor.href);
        });
        window.addEventListener('popstate', () => load(location.href, false));
        form.querySelector('[data-admin-submit]')?.setAttribute('hidden', '');
    });
})();
