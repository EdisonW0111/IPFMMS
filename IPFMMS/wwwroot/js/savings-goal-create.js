(() => {
    const form = document.getElementById('createSavingsGoalForm') || document.getElementById('editSavingsGoalForm');
    if (!form) return;
    const savingDay = document.getElementById('AutomaticSavingDay');
    const dayGroup = document.getElementById('automaticSavingDayGroup');
    const method = document.getElementById('SavingsMethod');
    const amount = document.getElementById('TargetAmount');
    const date = document.getElementById('TargetDate');
    const monthly = document.getElementById('MonthlyTargetAmount');
    const group = document.getElementById('monthlyTargetGroup');
    const help = document.getElementById('monthlyTargetHelp');
    const today = form.dataset.today;

    function updateMonthlyTarget() {
        const automatic = method.value === 'AutomaticMonthlyTarget';
        group.hidden = !automatic;
        dayGroup.hidden = !automatic;
        savingDay.disabled = !automatic;
        savingDay.required = automatic;
        monthly.disabled = !automatic;
        monthly.readOnly = true;
        monthly.required = false;
        monthly.value = '';
        help.textContent = 'Enter a valid target amount and date to calculate your monthly target.';
        if (!automatic) return;

        // Work in whole sen so the preview does not round below the target.
        if (!/^\d+(?:\.\d{1,2})?$/.test(amount.value) || !/^\d{4}-\d{2}-\d{2}$/.test(date.value)) return;
        const [whole, fraction = ''] = amount.value.split('.');
        const cents = Number(whole) * 100 + Number(fraction.padEnd(2, '0'));
        if (!Number.isSafeInteger(cents) || cents < 1 || cents > 999999999999) return;
        const savedCents = Number(form.dataset.savedCents || '0');
        if (!Number.isSafeInteger(savedCents) || savedCents < 0) return;
        const remainingCents = cents - savedCents;
        if (remainingCents < 0) {
            help.textContent = 'The target cannot be lower than the amount already saved.';
            return;
        }

        const [year, month, day] = date.value.split('-').map(Number);
        const parsed = new Date(Date.UTC(year, month - 1, day));
        if (year > 9998 || parsed.getUTCFullYear() !== year || parsed.getUTCMonth() + 1 !== month ||
            parsed.getUTCDate() !== day || date.value < today) return;
        const [currentYear, currentMonth, currentDay] = today.split('-').map(Number);
        const chosenDay = Number(savingDay.value);
        if (!Number.isInteger(chosenDay) || chosenDay < 1 || chosenDay > 28) return;
        const firstMonthOffset = chosenDay < currentDay ? 1 : 0;
        const months = (year - currentYear) * 12 + month - currentMonth + 1 - firstMonthOffset - (day < chosenDay ? 1 : 0);
        if (months < 1) return;

        const unchanged = form.dataset.existingMonthly && Number(amount.value) === Number(form.dataset.originalTarget)
            && date.value === form.dataset.originalDate && savingDay.value === form.dataset.originalDay;
        const monthlyAmount = unchanged ? Number(form.dataset.existingMonthly) : Math.ceil(remainingCents / months) / 100;
        monthly.value = monthlyAmount.toFixed(2);
        if (remainingCents === 0) {
            help.textContent = 'This goal is fully funded. No further monthly saving is needed.';
            return;
        }
        const formatted = new Intl.NumberFormat('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(monthlyAmount);
        if (unchanged) {
            help.textContent = 'Existing monthly schedule. Extra contributions reduce upcoming deductions. Changing the target, date or saving day creates a new plan from the remaining amount.';
            return;
        }
        help.textContent = `Save RM ${formatted} per month over ${months} month(s), scheduled deduction(s) before your target date. Extra contributions reduce upcoming deductions. Rounded up to the nearest sen.`;
    }

    method.addEventListener('change', updateMonthlyTarget);
    for (const input of [amount, date, savingDay]) {
        input.addEventListener('input', updateMonthlyTarget);
        input.addEventListener('change', updateMonthlyTarget);
    }
    updateMonthlyTarget();
})();
