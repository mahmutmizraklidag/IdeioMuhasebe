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

    const cExpTotal = document.getElementById("cExpTotal");
    const cExpPaid = document.getElementById("cExpPaid");
    const cExpRemaining = document.getElementById("cExpRemaining");

    const cIncTotal = document.getElementById("cIncTotal");
    const cIncReceived = document.getElementById("cIncReceived");
    const cIncRemaining = document.getElementById("cIncRemaining");

    const expCatToggle = document.getElementById("expCatToggle");
    const incCatToggle = document.getElementById("incCatToggle");

    const expCatPanel = document.getElementById("expCatPanel");
    const incCatPanel = document.getElementById("incCatPanel");

    const expCatList = document.getElementById("expCatList");
    const incCatList = document.getElementById("incCatList");

    const expCatClear = document.getElementById("expCatClear");
    const incCatClear = document.getElementById("incCatClear");

    const expCatSelectAll = document.getElementById("expCatSelectAll");
    const incCatSelectAll = document.getElementById("incCatSelectAll");

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

    setCurrentMonthRange();

    let allUpcomingExpenses = [];
    let allUpcomingIncomes = [];

    let expenseCategories = [];
    let incomeCategories = [];

    let selectedExpenseCategoryIds = new Set();
    let selectedIncomeCategoryIds = new Set();

    let baseExpenseRemaining = 0;
    let baseIncomeRemaining = 0;

    expCatPanel.addEventListener("click", (e) => e.stopPropagation());
    incCatPanel.addEventListener("click", (e) => e.stopPropagation());

    const positionPanelUnderButton = (panel, button) => {
        const rect = button.getBoundingClientRect();
        panel.style.left = `${rect.left}px`;
        panel.style.top = `${rect.bottom + 8}px`;
    };

    const repositionOpenPanels = () => {
        if (!expCatPanel.classList.contains("d-none")) {
            positionPanelUnderButton(expCatPanel, expCatToggle);
        }

        if (!incCatPanel.classList.contains("d-none")) {
            positionPanelUnderButton(incCatPanel, incCatToggle);
        }
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

    const buildExpenseCategories = () => {
        const map = new Map();
        allUpcomingExpenses.forEach(x => {
            if (!map.has(x.debtTypeId)) {
                map.set(x.debtTypeId, { id: x.debtTypeId, name: x.debtType });
            }
        });

        expenseCategories = [...map.values()].sort((a, b) => a.name.localeCompare(b.name, "tr"));
        selectedExpenseCategoryIds = new Set(expenseCategories.map(x => x.id));
        renderExpenseCategoryPanel();
    };

    const buildIncomeCategories = () => {
        const map = new Map();
        allUpcomingIncomes.forEach(x => {
            if (!map.has(x.incomeTypeId)) {
                map.set(x.incomeTypeId, { id: x.incomeTypeId, name: x.incomeType });
            }
        });

        incomeCategories = [...map.values()].sort((a, b) => a.name.localeCompare(b.name, "tr"));
        selectedIncomeCategoryIds = new Set(incomeCategories.map(x => x.id));
        renderIncomeCategoryPanel();
    };

    const renderExpenseCategoryPanel = () => {
        expCatList.innerHTML = expenseCategories.map(x => `
      <label class="form-check m-0">
        <input
          class="form-check-input exp-cat-check"
          type="checkbox"
          value="${x.id}"
          ${selectedExpenseCategoryIds.has(x.id) ? "checked" : ""}>
        <span class="form-check-label">${x.name}</span>
      </label>
    `).join("");

        const total = expenseCategories.length;
        const selected = selectedExpenseCategoryIds.size;

        expCatSelectAll.checked = total > 0 && selected === total;
        expCatSelectAll.indeterminate = selected > 0 && selected < total;
    };

    const renderIncomeCategoryPanel = () => {
        incCatList.innerHTML = incomeCategories.map(x => `
      <label class="form-check m-0">
        <input
          class="form-check-input inc-cat-check"
          type="checkbox"
          value="${x.id}"
          ${selectedIncomeCategoryIds.has(x.id) ? "checked" : ""}>
        <span class="form-check-label">${x.name}</span>
      </label>
    `).join("");

        const total = incomeCategories.length;
        const selected = selectedIncomeCategoryIds.size;

        incCatSelectAll.checked = total > 0 && selected === total;
        incCatSelectAll.indeterminate = selected > 0 && selected < total;
    };

    const getFilteredExpenses = () => {
        return allUpcomingExpenses.filter(x => selectedExpenseCategoryIds.has(x.debtTypeId));
    };

    const getFilteredIncomes = () => {
        return allUpcomingIncomes.filter(x => selectedIncomeCategoryIds.has(x.incomeTypeId));
    };

    const updateRemainingCardsByCategoryFilter = () => {
        const allExpenseSelected = selectedExpenseCategoryIds.size === expenseCategories.length;
        const allIncomeSelected = selectedIncomeCategoryIds.size === incomeCategories.length;

        const expenseRemaining = allExpenseSelected
   ? baseExpenseRemaining
   : getFilteredExpenses().reduce((sum, x) => sum + Number(x.remainingAmount || 0), 0);

        const incomeRemaining = allIncomeSelected
   ? baseIncomeRemaining
   : getFilteredIncomes().reduce((sum, x) => sum + Number(x.remainingAmount || 0), 0);

        cExpRemaining.textContent = app.money(expenseRemaining);
        cIncRemaining.textContent = app.money(incomeRemaining);
    };

    const renderExpenses = () => {
        const filtered = getFilteredExpenses();

        expBody.innerHTML = "";
        expCount.textContent = filtered.length;

        if (filtered.length === 0) {
            expBody.innerHTML = `
        <tr>
          <td colspan="6" class="text-center text-muted py-4">Kayıt bulunamadı.</td>
        </tr>
      `;
            resetSelectCards();
            syncSelectAllState();
            updateRemainingCardsByCategoryFilter();
            return;
        }

        filtered.forEach(x => {
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
              data-amount="${x.remainingAmount}">
          </td>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td>${x.debtType}</td>
          <td class="fw-semibold">${x.name}${periodBadge}</td>
          <td class="text-end">
            <div class="fw-semibold">${app.money(x.remainingAmount)}</div>
            <div class="small text-muted">Toplam: ${app.money(x.totalAmount)} · Ödenen: ${app.money(x.paidAmount)}</div>
          </td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, false)}</td>
        </tr>
      `);
   });

        resetSelectCards();
        syncSelectAllState();
        updateRemainingCardsByCategoryFilter();
    };

    const renderIncomes = () => {
        const filtered = getFilteredIncomes();

        incBody.innerHTML = "";
        incCount.textContent = filtered.length;

        if (filtered.length === 0) {
            incBody.innerHTML = `
        <tr>
          <td colspan="6" class="text-center text-muted py-4">Kayıt bulunamadı.</td>
        </tr>
      `;
            resetSelectCards();
            syncSelectAllState();
            updateRemainingCardsByCategoryFilter();
            return;
        }

        filtered.forEach(x => {
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
              data-amount="${x.remainingAmount}">
          </td>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td>${x.incomeType}</td>
          <td class="fw-semibold">${x.name}${periodBadge}</td>
          <td class="text-end">
            <div class="fw-semibold">${app.money(x.remainingAmount)}</div>
            <div class="small text-muted">Toplam: ${app.money(x.totalAmount)} · Tahsil Edilen: ${app.money(x.receivedAmount)}</div>
          </td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, false)}</td>
        </tr>
      `);
   });

        resetSelectCards();
        syncSelectAllState();
        updateRemainingCardsByCategoryFilter();
    };

    const closeCategoryPanels = () => {
        expCatPanel.classList.add("d-none");
        incCatPanel.classList.add("d-none");
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

        baseExpenseRemaining = Number(data.cards.expenseRemaining || 0);
        baseIncomeRemaining = Number(data.cards.incomeRemaining || 0);

        allUpcomingExpenses = data.upcomingExpenses || [];
        allUpcomingIncomes = data.upcomingIncomes || [];

        buildExpenseCategories();
        buildIncomeCategories();

        renderExpenses();
        renderIncomes();

        closeCategoryPanels();
    };

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

    expCatToggle.addEventListener("click", () => {
       incCatPanel.classList.add("d-none");

       const willOpen = expCatPanel.classList.contains("d-none");
       expCatPanel.classList.toggle("d-none");

       if (willOpen) {
           positionPanelUnderButton(expCatPanel, expCatToggle);
       }
   });

    incCatToggle.addEventListener("click", () => {
       expCatPanel.classList.add("d-none");

       const willOpen = incCatPanel.classList.contains("d-none");
       incCatPanel.classList.toggle("d-none");

       if (willOpen) {
           positionPanelUnderButton(incCatPanel, incCatToggle);
       }
   });

    document.addEventListener("click", (e) => {
       const expInside = expCatPanel.contains(e.target) || expCatToggle.contains(e.target);
       const incInside = incCatPanel.contains(e.target) || incCatToggle.contains(e.target);

       if (!expInside) expCatPanel.classList.add("d-none");
       if (!incInside) incCatPanel.classList.add("d-none");
   });

    document.addEventListener("keydown", (e) => {
       if (e.key === "Escape") closeCategoryPanels();
   });

    window.addEventListener("resize", repositionOpenPanels);
    window.addEventListener("scroll", repositionOpenPanels, true);

    expCatPanel.addEventListener("change", (e) => {
       if (e.target.id === "expCatSelectAll") {
           if (e.target.checked) {
               selectedExpenseCategoryIds = new Set(expenseCategories.map(x => x.id));
           } else {
               selectedExpenseCategoryIds = new Set();
           }
           renderExpenseCategoryPanel();
           renderExpenses();
           return;
       }

       if (e.target.classList.contains("exp-cat-check")) {
           const id = Number(e.target.value);
           if (e.target.checked) selectedExpenseCategoryIds.add(id);
           else selectedExpenseCategoryIds.delete(id);

           renderExpenseCategoryPanel();
           renderExpenses();
       }
   });

    incCatPanel.addEventListener("change", (e) => {
       if (e.target.id === "incCatSelectAll") {
           if (e.target.checked) {
               selectedIncomeCategoryIds = new Set(incomeCategories.map(x => x.id));
           } else {
               selectedIncomeCategoryIds = new Set();
           }
           renderIncomeCategoryPanel();
           renderIncomes();
           return;
       }

       if (e.target.classList.contains("inc-cat-check")) {
           const id = Number(e.target.value);
           if (e.target.checked) selectedIncomeCategoryIds.add(id);
           else selectedIncomeCategoryIds.delete(id);

           renderIncomeCategoryPanel();
           renderIncomes();
       }
   });

    expCatClear.addEventListener("click", () => {
       selectedExpenseCategoryIds = new Set(expenseCategories.map(x => x.id));
       renderExpenseCategoryPanel();
       renderExpenses();
   });

    incCatClear.addEventListener("click", () => {
       selectedIncomeCategoryIds = new Set(incomeCategories.map(x => x.id));
       renderIncomeCategoryPanel();
       renderIncomes();
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