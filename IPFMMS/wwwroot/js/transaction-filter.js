document.addEventListener("DOMContentLoaded", function () {

    const form = document.getElementById("transactionFilterForm");
    const results = document.getElementById("transactionResults");
    const loading = document.getElementById("transactionLoading");
    const errorMessage = document.getElementById("transactionError");
    const resetButton = document.getElementById("resetTransactionFilters");

    const pageInput = document.getElementById("filterPage");
    const pageSizeInput = document.getElementById("filterPageSize");

    const searchInput = document.getElementById("filterSearch");
    const typeInput = document.getElementById("filterType");
    const categoryInput = document.getElementById("filterCategory");
    const sortInput = document.getElementById("filterSort");

    const startDateInput = document.getElementById("filterStartDate");
    const endDateInput = document.getElementById("filterEndDate");

    const minAmountInput = document.getElementById("filterMinAmount");
    const maxAmountInput = document.getElementById("filterMaxAmount");

    if (!form || !results || !pageInput || !pageSizeInput) {
        return;
    }

    const allCategoryOptions = Array.from(
        categoryInput.options
    ).map(function (option) {
        return option.cloneNode(true);
    });

    // Show categories based on the transaction type.
    function updateCategoryOptions() {

        const selectedType = typeInput.value;

        // Clear the current category options.
        categoryInput.innerHTML = "";

        allCategoryOptions.forEach(function (option) {

            const categoryType = option.dataset.type;

            if (
                selectedType === "All" ||
                categoryType === "All" ||
                categoryType === selectedType
            ) {
                categoryInput.appendChild(
                    option.cloneNode(true)
                );
            }

        });

        // Reset category when transaction type changes.
        categoryInput.value = "";
    }

    let currentRequest = null;
    let searchTimer = null;
    let requestVersion = 0;

    // Validate the current filter values.
    function validateFilters() {

        if (!form.reportValidity()) {
            return false;
        }

        if (startDateInput.value && endDateInput.value) {
            if (startDateInput.value > endDateInput.value) {
                alert("Start date cannot be later than end date.");
                return false;
            }
        }

        if (minAmountInput.value && maxAmountInput.value) {
            const minAmount = Number(minAmountInput.value);
            const maxAmount = Number(maxAmountInput.value);

            if (minAmount > maxAmount) {
                alert("Minimum amount cannot exceed maximum amount.");
                return false;
            }
        }

        return true;
    }

    // Load filtered transactions using AJAX.
    async function loadTransactions() {

        if (!validateFilters()) {
            return;
        }

        // Cancel the previous request if it is still running.
        if (currentRequest) {
            currentRequest.abort();
        }

        const controller = new AbortController();
        currentRequest = controller;

        const thisRequest = ++requestVersion;

        loading.classList.remove("d-none");
        errorMessage.classList.add("d-none");

        // Convert the filter form into query parameters.
        const parameters = new URLSearchParams(
            new FormData(form)
        );

        const requestUrl =
            form.action + "?" + parameters.toString();

        try {

            const response = await fetch(requestUrl, {
                method: "GET",
                credentials: "same-origin",
                headers: {
                    "X-Requested-With": "XMLHttpRequest",
                    "Accept": "text/html"
                },
                signal: controller.signal,
                cache: "no-store"
            });

            if (!response.ok || response.redirected) {
                throw new Error(
                    "Unable to retrieve transactions."
                );
            }

            const html = await response.text();

            // Ignore outdated responses.
            if (thisRequest !== requestVersion) {
                return;
            }

            // Insert the returned partial view.
            results.innerHTML = html;

        }
        catch (error) {

            // Aborted requests are expected when filters change.
            if (error.name === "AbortError") {
                return;
            }

            console.error(
                "Transaction filtering failed:",
                error
            );

            if (thisRequest === requestVersion) {
                errorMessage.classList.remove("d-none");
            }

        }
        finally {

            if (thisRequest === requestVersion) {
                loading.classList.add("d-none");
                currentRequest = null;
            }

        }
    }

    // Reset pagination when applying a new filter.
    function applyFilters() {
        clearTimeout(searchTimer);
        pageInput.value = "1";
        loadTransactions();
    }

    // Apply Filters button / Enter key.
    form.addEventListener("submit", function (event) {

        event.preventDefault();

        applyFilters();
    });

    typeInput.addEventListener("change", function () {

        updateCategoryOptions();

        applyFilters();
    });

    // Category and sorting changes.
    [categoryInput, sortInput].forEach(function (element) {

        element.addEventListener("change", applyFilters);

    });

    // Apply date and amount filters after changes.
    [
        startDateInput,
        endDateInput,
        minAmountInput,
        maxAmountInput
    ].forEach(function (element) {

        if (element) {
            element.addEventListener(
                "change",
                applyFilters
            );
        }
    });

    // Search after the user stops typing.
    if (searchInput) {

        searchInput.addEventListener("input", function () {

            clearTimeout(searchTimer);

            searchTimer = setTimeout(function () {

                applyFilters();

            }, 400);

        });
    }

    // Reset all filters.
    if (resetButton) {

        resetButton.addEventListener("click", function () {

            clearTimeout(searchTimer);

            form.reset();

            updateCategoryOptions();

            pageInput.value = "1";
            pageSizeInput.value = "10";

            loadTransactions();
        });
    }

    // Handle pagination using event delegation.
    results.addEventListener("click", function (event) {

        const pageButton = event.target.closest(
            ".transaction-page"
        );

        if (!pageButton || pageButton.disabled) {
            return;
        }

        const requestedPage = Number(
            pageButton.dataset.page
        );

        if (!Number.isInteger(requestedPage) ||
            requestedPage < 1) {
            return;
        }

        pageInput.value = String(requestedPage);

        loadTransactions();
    });

    // Handle page-size selection.
    results.addEventListener("change", function (event) {

        if (event.target.id !== "transactionPageSize") {
            return;
        }

        const selectedSize = Number(event.target.value);

        if (![10, 25, 50].includes(selectedSize)) {
            return;
        }

        pageSizeInput.value = String(selectedSize);
        pageInput.value = "1";

        loadTransactions();
    });

    // Initialize available categories.
    updateCategoryOptions();

    // Load transactions automatically on page opening.
    loadTransactions();

});