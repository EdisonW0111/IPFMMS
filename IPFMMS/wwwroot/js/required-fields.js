(() => {
    function update() {
        document.querySelectorAll('label[for]').forEach(label => {
            const field = document.getElementById(label.htmlFor);
            const needed = field && !field.matches(':disabled') && !field.closest('[hidden]') && !field.readOnly && field.type !== 'hidden' &&
                (field.required || (field.hasAttribute('data-val-required') && !['checkbox', 'radio'].includes(field.type)));
            let marker = label.querySelector('.required-field-marker');
            if (needed && !marker) {
                marker = document.createElement('span'); marker.className = 'required-field-marker';
                marker.textContent = ' *'; marker.setAttribute('aria-hidden', 'true'); label.append(marker);
            } else if (!needed && marker) marker.remove();
            if (field) field.setAttribute('aria-required', needed ? 'true' : 'false');
        });
    }
    document.addEventListener('DOMContentLoaded', () => {
        update();
        let queued = false;
        new MutationObserver(() => {
            if (!queued) { queued = true; queueMicrotask(() => { queued = false; update(); }); }
        }).observe(document.body, {subtree:true, childList:true, attributes:true,
            attributeFilter:['required','disabled','readonly','data-val-required','hidden']});
    });
})();
