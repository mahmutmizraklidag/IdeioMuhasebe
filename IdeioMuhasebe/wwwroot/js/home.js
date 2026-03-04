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

    const load = async () => {
        const data = await app.get("/Home/Summary", {
            from: from.value,
            to: to.value,
            debtTypeId: debtTypeFilter.value,
            incomeTypeId: incomeTypeFilter.value
        });

        // Kartlar
        document.getElementById("cExpTotal").textContent = app.money(data.cards.expenseTotal);
        document.getElementById("cExpPaid").textContent = app.money(data.cards.expensePaid);
        document.getElementById("cExpRemaining").textContent = app.money(data.cards.expenseRemaining);

        document.getElementById("cIncTotal").textContent = app.money(data.cards.incomeTotal);
        document.getElementById("cIncReceived").textContent = app.money(data.cards.incomeReceived);
        document.getElementById("cIncRemaining").textContent = app.money(data.cards.incomeRemaining);

        // Yaklaşan giderler (sadece ödenmemiş geliyor)
        expBody.innerHTML = "";
        expCount.textContent = data.upcomingExpenses.length;

        data.upcomingExpenses.forEach(x => {
            expBody.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td>${x.debtType}</td>
          <td class="fw-semibold">${x.name}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, false)}</td>
        </tr>
      `);
        });

        // Yaklaşan gelirler (sadece tahsil edilmemiş geliyor)
        incBody.innerHTML = "";
        incCount.textContent = data.upcomingIncomes.length;

        data.upcomingIncomes.forEach(x => {
            incBody.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td>${x.incomeType}</td>
          <td class="fw-semibold">${x.name}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, false)}</td>
        </tr>
      `);
        });
    };

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