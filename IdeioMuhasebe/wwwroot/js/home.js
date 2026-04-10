(() => {
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btn = document.getElementById("btnApply");

    const debtTypeFilter = document.getElementById("debtTypeFilter");
    const incomeTypeFilter = document.getElementById("incomeTypeFilter");

    const expenseSummaryBody = document.getElementById("expenseSummaryBody");
    const incomeSummaryBody = document.getElementById("incomeSummaryBody");

    const expenseCategoryCount = document.getElementById("expenseCategoryCount");
    const incomeCategoryCount = document.getElementById("incomeCategoryCount");

    const cExpTotal = document.getElementById("cExpTotal");
    const cExpPaid = document.getElementById("cExpPaid");
    const cExpRemaining = document.getElementById("cExpRemaining");

    const cIncTotal = document.getElementById("cIncTotal");
    const cIncReceived = document.getElementById("cIncReceived");
    const cIncRemaining = document.getElementById("cIncRemaining");

    const tabButtons = [...document.querySelectorAll("[data-summary-tab]")];
    const expenseSummaryCard = document.getElementById("expenseSummaryCard");
    const incomeSummaryCard = document.getElementById("incomeSummaryCard");

    let activeTab = "all";

    const setCurrentMonthRange = () => {
        const now = new Date();
        const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
        const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0);

        const formatDateForInput = (date) => {
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, "0");
            const day = String(date.getDate()).padStart(2, "0");
            return `${year}-${month}-${day}`;
        };

        from.value = formatDateForInput(firstDay);
        to.value = formatDateForInput(lastDay);
    };

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

    const statusHtml = (statusText) => {
        if (statusText === "Ödendi") {
            return `<span class="summary-status-paid">Ödendi</span>`;
        }

        return `<span class="summary-status-unpaid">Ödenmedi</span>`;
    };

    const countBadgeHtml = (value, cssClass) => {
        return `<span class="summary-count-badge ${cssClass}">${value}</span>`;
    };

    const applyTabVisibility = () => {
        tabButtons.forEach(btn => {
            btn.classList.toggle("active", btn.dataset.summaryTab === activeTab);
        });

        if (activeTab === "all") {
            expenseSummaryCard.classList.remove("d-none");
            incomeSummaryCard.classList.remove("d-none");
            return;
        }

        if (activeTab === "expense") {
            expenseSummaryCard.classList.remove("d-none");
            incomeSummaryCard.classList.add("d-none");
            return;
        }

        if (activeTab === "income") {
            expenseSummaryCard.classList.add("d-none");
            incomeSummaryCard.classList.remove("d-none");
        }
    };

    const renderExpenseSummary = (rows) => {
        expenseCategoryCount.textContent = rows.length;
        expenseSummaryBody.innerHTML = "";

        if (!rows.length) {
            expenseSummaryBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-muted py-4">Kayıt bulunamadı.</td>
                </tr>
            `;
            return;
        }

        rows.forEach(x => {
            expenseSummaryBody.insertAdjacentHTML("beforeend", `
                <tr>
                    <td>
                <a href="/Debts/Detail/${x.categoryId}" class="summary-category-link">
        ${x.categoryName}
    </a>
</td>
                    <td class="text-end summary-money">${app.money(x.totalAmount)}</td>
                    <td class="text-end summary-money">${app.money(x.paidAmount)}</td>
                    <td class="text-end summary-money">${app.money(x.remainingAmount)}</td>
                    <td class="text-center">${countBadgeHtml(x.overdueCount, "summary-count-red")}</td>
                    <td class="text-center">${countBadgeHtml(x.dueTodayCount, "summary-count-yellow")}</td>
                    <td class="text-center">${countBadgeHtml(x.otherCount, "summary-count-green")}</td>
                    <td class="text-center">${statusHtml(x.statusText)}</td>
                </tr>
            `);
        });
    };

    const renderIncomeSummary = (rows) => {
        incomeCategoryCount.textContent = rows.length;
        incomeSummaryBody.innerHTML = "";

        if (!rows.length) {
            incomeSummaryBody.innerHTML = `
                <tr>
                    <td colspan="8" class="text-center text-muted py-4">Kayıt bulunamadı.</td>
                </tr>
            `;
            return;
        }

        rows.forEach(x => {
            incomeSummaryBody.insertAdjacentHTML("beforeend", `
                <tr>
                   <td>
                <a href="/Incomes/Detail/${x.categoryId}" class="summary-category-link">
        ${x.categoryName}
    </a>
</td>
                    <td class="text-end summary-money">${app.money(x.totalAmount)}</td>
                    <td class="text-end summary-money">${app.money(x.receivedAmount)}</td>
                    <td class="text-end summary-money">${app.money(x.remainingAmount)}</td>
                    <td class="text-center">${countBadgeHtml(x.overdueCount, "summary-count-red")}</td>
                    <td class="text-center">${countBadgeHtml(x.dueTodayCount, "summary-count-yellow")}</td>
                    <td class="text-center">${countBadgeHtml(x.otherCount, "summary-count-green")}</td>
                    <td class="text-center">${statusHtml(x.statusText)}</td>
                </tr>
            `);
        });
    };

    const load = async () => {
        const data = await app.get("/Home/Summary", {
            from: from.value,
            to: to.value,
            debtTypeId: debtTypeFilter.value,
            incomeTypeId: incomeTypeFilter.value
        });

        cExpTotal.textContent = app.money(data.cards.expenseTotal);
        cExpPaid.textContent = app.money(data.cards.expensePaid);
        cExpRemaining.textContent = app.money(data.cards.expenseRemaining);

        cIncTotal.textContent = app.money(data.cards.incomeTotal);
        cIncReceived.textContent = app.money(data.cards.incomeReceived);
        cIncRemaining.textContent = app.money(data.cards.incomeRemaining);

        renderExpenseSummary(data.expenseSummaryRows || []);
        renderIncomeSummary(data.incomeSummaryRows || []);
        applyTabVisibility();
    };

    document.addEventListener("click", async (e) => {
            const tabBtn = e.target.closest("[data-summary-tab]");
            if (tabBtn) {
                activeTab = tabBtn.dataset.summaryTab || "all";
                applyTabVisibility();
                return;
            }

            const link = e.target.closest(".js-summary-filter");
            if (!link) return;

            e.preventDefault();

            const id = link.dataset.id || "";
            const kind = link.dataset.kind;

            if (kind === "expense") {
                debtTypeFilter.value = id;
                activeTab = "expense";
            } else if (kind === "income") {
                incomeTypeFilter.value = id;
                activeTab = "income";
            }

            applyTabVisibility();
            await load();
        });

    btn.addEventListener("click", load);

    setCurrentMonthRange();
    applyTabVisibility();

    (async () => {
        await Promise.all([
            loadDebtTypes(),
            loadIncomeTypes()
        ]);

        await load();
    })();
})();