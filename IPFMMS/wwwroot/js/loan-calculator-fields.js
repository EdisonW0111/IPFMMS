(() => {
    const form = document.getElementById('loanCalculatorForm');
    if (!form) return;
    const rateType = form.elements.RateType, method = form.elements.Method;
    const fixed = document.getElementById('calculatorFixedFields');
    const floating = document.getElementById('calculatorFloatingFields');
    const flatOption = method.querySelector('option[value="FlatRate"]');
    let fixedMethod = method.value;
    function update() {
        const variable = rateType.value === 'Floating';
        fixed.hidden = variable; fixed.disabled = variable;
        floating.hidden = !variable; floating.disabled = !variable;
        flatOption.disabled = variable; flatOption.hidden = variable;
        if (variable) method.value = 'ReducingBalance';
        document.getElementById('calculatorRateHint').textContent = variable
            ? 'Enter the reference rate and spread from your lender. Variable-rate estimates use reducing balance and assume this rate stays unchanged.'
            : 'Enter the fixed annual rate from your lender. Choose the interest method stated in your agreement.';
        const reference = form.elements.ReferenceRate.value.trim();
        const spread = form.elements.Spread.value.trim();
        const total = Number(reference) + Number(spread);
        document.getElementById('calculatorCombinedRate').textContent = variable && reference !== '' && spread !== '' && Number.isFinite(total)
            ? `Combined annual lending rate: ${Number(total.toFixed(4))}% p.a.` : '';
    }
    rateType.addEventListener('change', () => {
        if (rateType.value === 'Floating') fixedMethod = method.value;
        else method.value = fixedMethod;
        update();
    });
    method.addEventListener('change', () => { if (rateType.value === 'Fixed') fixedMethod = method.value; });
    form.elements.ReferenceRate.addEventListener('input', update);
    form.elements.Spread.addEventListener('input', update);
    update();
})();
