document.addEventListener('DOMContentLoaded', function () {
    const form = document.getElementById('budgetFilterForm');
    const month = document.getElementById('month');
    const results = document.getElementById('budgetResults');
    const loading = document.getElementById('budgetLoading');
    const errorMessage = document.getElementById('budgetError');
    const retryButton = document.getElementById('retryBudgetFilter');

    if (!form || !month || !results || !loading || !errorMessage || !retryButton) return;

    let currentRequest = null;
    let requestVersion = 0;

    async function loadBudgets() {
        // Cancel and invalidate older requests even if the month is now empty.
        if (currentRequest) currentRequest.abort();
        const thisRequest = ++requestVersion;
        loading.classList.add('d-none');
        errorMessage.classList.add('d-none');
        results.setAttribute('aria-busy', 'false');
        if (!form.reportValidity()) return;

        const controller = new AbortController();
        currentRequest = controller;
        const url = new URL(form.dataset.indexUrl, window.location.href);
        url.searchParams.set('month', month.value);
        loading.classList.remove('d-none');
        results.setAttribute('aria-busy', 'true');

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                credentials: 'same-origin',
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'Accept': 'text/html'
                },
                signal: controller.signal,
                cache: 'no-store'
            });

            // Authentication redirects must not insert the login page into the dashboard.
            if (!response.ok || response.redirected) throw new Error('Unable to retrieve budgets.');
            const html = await response.text();
            if (thisRequest !== requestVersion) return;

            results.innerHTML = html;
            window.history.replaceState(null, '', url.toString());
        } catch (error) {
            if (error.name !== 'AbortError' && thisRequest === requestVersion) {
                errorMessage.classList.remove('d-none');
            }
        } finally {
            if (thisRequest === requestVersion) {
                loading.classList.add('d-none');
                results.setAttribute('aria-busy', 'false');
                currentRequest = null;
            }
        }
    }

    month.addEventListener('change', loadBudgets);
    form.addEventListener('submit', function (event) {
        event.preventDefault();
        loadBudgets();
    });
    retryButton.addEventListener('click', loadBudgets);

    // Apply due repeat settings on the initial page visit as well as month changes.
    loadBudgets();
});
