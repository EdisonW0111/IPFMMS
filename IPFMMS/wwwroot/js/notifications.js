(() => {
    const badge = document.getElementById('notificationUnreadCount');
    if (!badge) return;
    const link = badge.closest('a');
    let busy = false;
    async function refresh() {
        if (busy || document.hidden) return;
        busy = true;
        try {
            const response = await fetch(badge.dataset.countUrl, { credentials: 'same-origin', cache: 'no-store', headers: { Accept: 'application/json' } });
            if (!response.ok || !response.headers.get('content-type')?.includes('application/json')) return;
            const data = await response.json();
            if (!Number.isInteger(data.count) || data.count < 0) return;
            badge.textContent = data.count > 99 ? '99+' : String(data.count);
            badge.hidden = data.count === 0;
            link.setAttribute('aria-label', `Notifications, ${data.count} unread`);
        } catch { /* Leave navigation usable if a refresh fails. */ }
        finally { busy = false; }
    }
    refresh();
    setInterval(refresh, 60000);
    document.addEventListener('visibilitychange', () => { if (!document.hidden) refresh(); });
})();
