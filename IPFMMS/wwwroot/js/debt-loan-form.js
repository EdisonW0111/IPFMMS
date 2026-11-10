(() => {
    const form = document.getElementById('debtLoanForm');
    if (!form) return;
    const locked = form.dataset.loanLocked === 'true';
    const hasRepayments = form.dataset.hasRepayments === 'true';
    // Locked inputs have hidden counterparts for POST. Never regenerate their historical values.
    if (locked) return;
    const field = name => document.getElementById(name);
    const number = name => {
        const text = field(name).value.trim();
        return text === '' ? NaN : Number(text);
    };
    const status = document.getElementById('loanQuoteStatus');
    function refresh() {
        const calculated = field('CalculationMode').value === 'Calculated';
        const floating = calculated && field('RateType').value === 'Floating';
        const existing = field('IsExistingLoan').checked;
        const config = document.getElementById('loanConfiguration');
        config.hidden = !calculated;
        config.disabled = !calculated;
        const reference = document.getElementById('floatingRateFields');
        reference.hidden = !floating;
        reference.disabled = !floating;
        field('OutstandingAmount').readOnly = hasRepayments || (calculated && !existing);
        field('MonthlyRepaymentAmount').readOnly = calculated;
        field('InterestRate').readOnly = floating;
        document.querySelector('label[for="MonthlyRepaymentAmount"]').textContent = calculated ? 'Estimated monthly repayment (RM)' : 'Monthly repayment amount (RM)';
        document.getElementById('debtModeHint').textContent = calculated ? 'Calculate a monthly reducing-balance estimate from your principal, rate and term.' : 'Enter your balance and planned repayment. No interest is automatically added.';
        document.getElementById('outstandingHint').textContent = hasRepayments ? 'Managed by repayment history; this principal balance is read-only.' : calculated && !existing ? 'Starts automatically at the original principal for a new loan.' : 'Enter the principal still owed on your lender’s latest statement.';
        document.getElementById('monthlyPaymentHint').textContent = calculated ? 'Calculated by the system; the final payment can differ because of rounding.' : 'Your planned amount. Repayments are recorded manually.';
        document.getElementById('interestRateHint').textContent = floating ? 'Calculated as reference rate plus spread.' : calculated ? 'The annual rate used for the monthly reducing-balance estimate.' : 'For information only in manual tracking mode.';
        document.getElementById('loanTermHint').textContent = existing ? 'Enter the months remaining on your current loan.' : 'Enter the full repayment term, for example 60 months for five years.';
        document.getElementById('calculatedLoanNote').hidden = !calculated;
        document.getElementById('manualDebtNote').hidden = calculated;
        status.textContent = '';
        if (!calculated) return;
        if (!existing && !hasRepayments) field('OutstandingAmount').value = Number.isFinite(number('OriginalAmount')) ? field('OriginalAmount').value : '';
        let rate = number('InterestRate');
        if (floating) {
            const referenceRate = number('ReferenceRate'), spread = number('RateSpread');
            rate = referenceRate + spread;
            const validReference = Number.isFinite(referenceRate) && referenceRate >= 0 && referenceRate <= 100 && Number.isFinite(spread) && spread >= -100 && spread <= 100;
            field('InterestRate').value = validReference && rate >= 0 && rate <= 100 ? Number(rate.toFixed(4)).toString() : '';
            if (!validReference) rate = NaN;
        }
        const principal = number('OutstandingAmount'), months = number('LoanTermMonths');
        if (!Number.isFinite(principal) || principal <= 0 || principal > 999999999.99 || !Number.isFinite(rate) || rate < 0 || rate > 100 || !Number.isInteger(months) || months < 1 || months > 480) {
            field('MonthlyRepaymentAmount').value = '';
            status.textContent = 'Enter a valid principal, annual rate and term to see an estimate.';
            return;
        }
        const monthlyRate = rate / 1200;
        // expm1/log1p keep the browser preview stable for very small rates.
        const payment = monthlyRate === 0 ? principal / months : principal * monthlyRate / -Math.expm1(-months * Math.log1p(monthlyRate));
        const rounded = Math.max(.01, Math.round((payment + Number.EPSILON) * 100) / 100);
        if (!Number.isFinite(rounded) || rounded > 999999999.99) {
            field('MonthlyRepaymentAmount').value = '';
            status.textContent = 'The estimate is outside the supported amount range. Adjust the principal or term.';
            return;
        }
        field('MonthlyRepaymentAmount').value = rounded.toFixed(2);
        status.textContent = 'Preview only. The server recalculates the saved instalment from your loan terms.';
    }
    for (const name of ['CalculationMode', 'RateType', 'IsExistingLoan', 'OriginalAmount', 'OutstandingAmount', 'LoanTermMonths', 'InterestRate', 'ReferenceRate', 'RateSpread']) {
        const input = field(name);
        input.addEventListener(input.tagName === 'SELECT' || input.type === 'checkbox' ? 'change' : 'input', refresh);
    }
    refresh();
})();
