(() => {
    const root = document.documentElement;
    const saved = root.dataset.bsTheme === 'dark' ? 'dark' : 'light';
    // The server resolves account preferences and the cookie. Never reuse another account's localStorage.
    function apply(value) {
        const theme = value === 'Dark' || value === 'dark' ? 'dark' : 'light';
        const changed = root.dataset.bsTheme !== theme;
        root.dataset.bsTheme = theme;
        root.style.colorScheme = theme;
        if (changed) window.dispatchEvent(new CustomEvent('ipfmms:themechange', { detail: { theme } }));
    }
    apply(saved);
    function bindPreview() {
        const choices = document.querySelectorAll('[data-theme-choice]');
        const status = document.getElementById('themePreviewStatus');
        for (const choice of choices) choice.addEventListener('change', () => {
            if (!choice.checked) return;
            apply(choice.value);
            if (status) status.textContent = (choice.value.toLowerCase() === saved) ? 'Your saved theme is selected.' : 'Previewing this theme. Save preferences to keep it.';
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', bindPreview, { once: true });
    else bindPreview();
})();
