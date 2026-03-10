(() => {
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btn = document.getElementById("btnApply");

    const debtTypeFilter = document.getElementById("debtTypeFilter");
    const incomeTypeFilter = document.getElementById("incomeTypeFilter");

    const expBody = document.getElementById("expBody");
    const incBody = document.getElementById("incBody");
    const expCount = document.getElementById("expCount");
    const incCount = document.getElementById("incCount");

    const expSelectAll = document.getElementById("expSelectAll");
    const incSelectAll = document.getElementById("incSelectAll");

    const cSelExp = document.getElementById("cSelExp");
    const cSelInc = document.getElementById("cSelInc");
    const cSelDiff = document.getElementById("cSelDiff");

    const selectedCardsRow = document.getElementById("selectedCardsRow");

    app.setDefaultMonth(from, to);

    const loadDebtTypes = async () => {
        const data = await app.get("/DebtTypes/Options");
        debtTypeFilter.innerHTML =
            `<option value="">Tümü</option>` +
            data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");
    };

    const loadIncomeTypes = async () => {
        const data = await app.get("/IncomeTypes/Options");
        incomeTypeFilter.innerHTML =
            `<option value="">Tümü</option>` +
            data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");
    };

    const updateSelectCards = () => {
        const expChecks = [...document.querySelectorAll(".exp-check:checked")];
        const incChecks = [...document.querySelectorAll(".inc-check:checked")];

        const expTotal = expChecks.reduce((sum, x) => sum + Number(x.dataset.amount || 0), 0);
        const incTotal = incChecks.reduce((sum, x) => sum + Number(x.dataset.amount || 0), 0);
        const diff = incTotal - expTotal;

        const hasAnySelection = expChecks.length > 0 || incChecks.length > 0;
        selectedCardsRow.classList.toggle("d-none", !hasAnySelection);

        cSelExp.textContent = app.money(expTotal);
        cSelInc.textContent = app.money(incTotal);
        cSelDiff.textContent = app.money(diff);

        cSelDiff.classList.remove("text-success", "text-danger", "text-primary");
        if (diff > 0) cSelDiff.classList.add("text-success");
        else if (diff < 0) cSelDiff.classList.add("text-danger");
        else cSelDiff.classList.add("text-primary");
    };

    const syncSelectAllState = () => {
        const expChecks = [...document.querySelectorAll(".exp-check")];
        const incChecks = [...document.querySelectorAll(".inc-check")];

        if (expChecks.length === 0) {
            expSelectAll.checked = false;
            expSelectAll.indeterminate = false;
        } else {
            const checkedCount = expChecks.filter(x => x.checked).length;
            expSelectAll.checked = checkedCount === expChecks.length;
            expSelectAll.indeterminate = checkedCount > 0 && checkedCount < expChecks.length;
        }

        if (incChecks.length === 0) {
            incSelectAll.checked = false;
            incSelectAll.indeterminate = false;
        } else {
            const checkedCount = incChecks.filter(x => x.checked).length;
            incSelectAll.checked = checkedCount === incChecks.length;
            incSelectAll.indeterminate = checkedCount > 0 && checkedCount < incChecks.length;
        }
    };

    const resetSelectCards = () => {
        expSelectAll.checked = false;
        expSelectAll.indeterminate = false;
        incSelectAll.checked = false;
        incSelectAll.indeterminate = false;

        selectedCardsRow.classList.add("d-none");

        cSelExp.textContent = app.money(0);
        cSelInc.textContent = app.money(0);
        cSelDiff.textContent = app.money(0);
        cSelDiff.classList.remove("text-success", "text-danger");
        cSelDiff.classList.add("text-primary");
    };

    const load = async () => {
        const data = await app.get("/Home/Summary", {
            from: from.value,
            to: to.value,
            debtTypeId: debtTypeFilter.value,
            incomeTypeId: incomeTypeFilter.value
        });

        // Üst kartlar
        document.getElementById("cExpTotal").textContent = app.money(data.cards.expenseTotal);
        document.getElementById("cExpPaid").textContent = app.money(data.cards.expensePaid);
        document.getElementById("cExpRemaining").textContent = app.money(data.cards.expenseRemaining);

        document.getElementById("cIncTotal").textContent = app.money(data.cards.incomeTotal);
        document.getElementById("cIncReceived").textContent = app.money(data.cards.incomeReceived);
        document.getElementById("cIncRemaining").textContent = app.money(data.cards.incomeRemaining);

        // Yaklaşan giderler
        expBody.innerHTML = "";
        expCount.textContent = data.upcomingExpenses.length;

        data.upcomingExpenses.forEach(x => {
            const periodBadge = x.recurringPeriodText
                ? `<span class="badge bg-light text-dark border ms-2">${x.recurringPeriodText}</span>`
                : "";

            expBody.insertAdjacentHTML("beforeend", `
        <tr>
          <td>
            <input
              type="checkbox"
              class="form-check-input exp-check"
              data-id="${x.id}"
              data-amount="${x.amount}">
          </td>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td>${x.debtType}</td>
          <td class="fw-semibold">${x.name}${periodBadge}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, false)}</td>
        </tr>
      `);
        });

        // Yaklaşan gelirler
        incBody.innerHTML = "";
        incCount.textContent = data.upcomingIncomes.length;

        data.upcomingIncomes.forEach(x => {
            const periodBadge = x.recurringPeriodText
                ? `<span class="badge bg-light text-dark border ms-2">${x.recurringPeriodText}</span>`
                : "";

            incBody.insertAdjacentHTML("beforeend", `
        <tr>
          <td>
            <input
              type="checkbox"
              class="form-check-input inc-check"
              data-id="${x.id}"
              data-amount="${x.amount}">
          </td>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td>${x.incomeType}</td>
          <td class="fw-semibold">${x.name}${periodBadge}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, false)}</td>
        </tr>
      `);
        });

        resetSelectCards();
        syncSelectAllState();
        updateSelectCards();
    };

    // Tekil seçimler
    expBody.addEventListener("change", (e) => {
        if (!e.target.classList.contains("exp-check")) return;
        syncSelectAllState();
        updateSelectCards();
    });

    incBody.addEventListener("change", (e) => {
        if (!e.target.classList.contains("inc-check")) return;
        syncSelectAllState();
        updateSelectCards();
    });

    // Tümünü seç
    expSelectAll.addEventListener("change", () => {
        document.querySelectorAll(".exp-check").forEach(x => {
            x.checked = expSelectAll.checked;
        });
        syncSelectAllState();
        updateSelectCards();
    });

    incSelectAll.addEventListener("change", () => {
        document.querySelectorAll(".inc-check").forEach(x => {
            x.checked = incSelectAll.checked;
        });
        syncSelectAllState();
        updateSelectCards();
    });

    btn.addEventListener("click", load);
    debtTypeFilter.addEventListener("change", load);
    incomeTypeFilter.addEventListener("change", load);
    from.addEventListener("change", load);
    to.addEventListener("change", load);

    (async () => {
        await loadDebtTypes();
        await loadIncomeTypes();
        await load();
    })();
})();