document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('transactionMonthForm');
    const month = document.getElementById('transactionMonth');
    const results = document.getElementById('transactionMonthResults');
    const loading = document.getElementById('transactionMonthLoading');
    const errorMessage = document.getElementById('transactionMonthError');
    const retry = document.getElementById('retryTransactionMonth');
    if (!form || !month || !results || !loading || !errorMessage || !retry) return;

    let currentRequest = null;
    let requestVersion = 0;

    async function loadMonth() {
        if (currentRequest) currentRequest.abort();
        const version = ++requestVersion;
        loading.classList.add('d-none');
        errorMessage.classList.add('d-none');
        results.setAttribute('aria-busy', 'false');
        if (!form.reportValidity()) return;

        const controller = new AbortController();
        currentRequest = controller;
        const url = new URL(form.action, window.location.href);
        url.searchParams.set('month', month.value);
        loading.classList.remove('d-none');
        results.setAttribute('aria-busy', 'true');
        try {
            const response = await fetch(url.toString(), {
                method: 'GET', credentials: 'same-origin', cache: 'no-store',
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'text/html' },
                signal: controller.signal
            });
            if (!response.ok || response.redirected) throw new Error('Unable to load transactions.');
            const html = await response.text();
            if (version !== requestVersion) return;
            results.innerHTML = html;
            window.history.replaceState(null, '', url.toString());
        } catch (error) {
            if (error.name !== 'AbortError' && version === requestVersion)
                errorMessage.classList.remove('d-none');
        } finally {
            if (version === requestVersion) {
                loading.classList.add('d-none');
                results.setAttribute('aria-busy', 'false');
                currentRequest = null;
            }
        }
    }

    month.addEventListener('change', loadMonth);
    retry.addEventListener('click', loadMonth);
    form.addEventListener('submit', function (event) {
        event.preventDefault();
        loadMonth();
    });
});
