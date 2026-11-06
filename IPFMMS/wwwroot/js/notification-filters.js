(() => {
    const form = document.getElementById('notificationFilters');
    if (!form) return;
    let sequence = 0;
    let activeGet;
    const error = document.getElementById('notificationFilterError');
    function updateForm(url) {
        const query = new URL(url, location.href).searchParams;
        form.elements.type.value = query.get('type') || 'all';
        form.elements.read.value = query.get('read') || 'all';
    }
    async function load(url, options = {}, push = true) {
        const request = ++sequence;
        activeGet?.abort();
        const controller = new AbortController();
        if (!options.method || options.method === 'GET') activeGet = controller;
        const previous = document.getElementById('notificationResults');
        previous.setAttribute('aria-busy', 'true');
        error.hidden = true;
        try {
            const response = await fetch(url, { ...options, signal: controller.signal, credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest', Accept: 'text/html' } });
            if (!response.ok) throw new Error('Request failed');
            const text = await response.text();
            if (request !== sequence) return;
            const parsed = new DOMParser().parseFromString(text, 'text/html');
            const next = parsed.getElementById('notificationResults');
            if (!next) {
                if (response.redirected) { location.assign(response.url); return; }
                throw new Error('Unexpected response');
            }
            previous.replaceWith(next);
            const finalUrl = options.method ? response.url : url;
            updateForm(finalUrl);
            if (push) history.pushState(null, '', finalUrl);
            const count = Number(next.dataset.unreadCount);
            document.getElementById('notificationPageUnread').textContent = `${count} unread`;
            document.dispatchEvent(new CustomEvent('notifications:updated', { detail: { count } }));
        } catch (failure) {
            if (failure.name !== 'AbortError' && request === sequence) {
                error.textContent = 'Could not update notifications. Please try again.';
                error.hidden = false;
            }
        } finally {
            if (request === sequence) document.getElementById('notificationResults').setAttribute('aria-busy', 'false');
        }
    }
    function filter() {
        const url = new URL(form.action, location.href);
        new FormData(form).forEach((value, key) => url.searchParams.set(key, value));
        load(url.href);
    }
    form.addEventListener('submit', event => { event.preventDefault(); filter(); });
    form.addEventListener('change', filter);
    document.getElementById('notificationReset').addEventListener('click', event => { event.preventDefault(); load(event.currentTarget.href); });
    document.addEventListener('click', event => {
        const link = event.target.closest('#notificationResults [data-notification-page]');
        if (link) { event.preventDefault(); load(link.href); }
    });
    document.addEventListener('submit', event => {
        const actionForm = event.target.closest('#notificationResults form');
        if (!actionForm) return;
        event.preventDefault();
        const button = actionForm.querySelector('button');
        button.disabled = true;
        const body = new URLSearchParams(new FormData(actionForm));
        load(actionForm.action, { method: 'POST', body }, false).finally(() => { button.disabled = false; });
    });
    window.addEventListener('popstate', () => load(location.href, {}, false));
    // Keep the native submit button until the AJAX handlers are installed.
    document.getElementById('notificationFilterButton').hidden = true;
})();
