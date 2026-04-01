(() => {
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btn = document.getElementById("btnApply");

    const mode = document.getElementById("mode");

    const debtTypeFilter = document.getElementById("debtTypeFilter");
    const incomeTypeFilter = document.getElementById("incomeTypeFilter");

    const expenseStatus = document.getElementById("expenseStatus");
    const incomeStatus = document.getElementById("incomeStatus");

    const expenseKind = document.getElementById("expenseKind");

    const wrapDebtType = document.getElementById("wrapDebtType");
    const wrapIncomeType = document.getElementById("wrapIncomeType");
    const wrapExpenseStatus = document.getElementById("wrapExpenseStatus");
    const wrapIncomeStatus = document.getElementById("wrapIncomeStatus");
    const wrapExpenseKind = document.getElementById("wrapExpenseKind");

    const singleWrap = document.getElementById("singleDonutWrap");
    const compareWrap = document.getElementById("compareDonutsWrap");
    const barWrap = document.getElementById("barWrap");
    const monthBarWrap = document.getElementById("monthBarWrap");

    // ✅ NEW compare bar
    const btnCompareBar = document.getElementById("btnCompareBar");
    const aMetric = document.getElementById("aMetric");
    const bMetric = document.getElementById("bMetric");
    const aFrom = document.getElementById("aFrom");
    const aTo = document.getElementById("aTo");
    const bFrom = document.getElementById("bFrom");
    const bTo = document.getElementById("bTo");
    const aDebtType = document.getElementById("aDebtType");
    const bDebtType = document.getElementById("bDebtType");
    const aIncomeType = document.getElementById("aIncomeType");
    const bIncomeType = document.getElementById("bIncomeType");
    const aExpenseStatus = document.getElementById("aExpenseStatus");
    const bExpenseStatus = document.getElementById("bExpenseStatus");
    const aIncomeStatus = document.getElementById("aIncomeStatus");
    const bIncomeStatus = document.getElementById("bIncomeStatus");

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

    let lineChart = null;
    let totalBarChart = null;
    let monthBarChart = null;

    let typeChart = null;
    let expTypeChart = null;
    let incTypeChart = null;

    // ✅ new chart
    let customCompareChart = null;

    const loadDebtTypes = async () => {
        const data = await app.get("/DebtTypes/Options");
        const opt = `<option value="">Tümü</option>` + data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");
        debtTypeFilter.innerHTML = opt;
        aDebtType.innerHTML = opt;
        bDebtType.innerHTML = opt;
    };

    const loadIncomeTypes = async () => {
        const data = await app.get("/IncomeTypes/Options");
        const opt = `<option value="">Tümü</option>` + data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");
        incomeTypeFilter.innerHTML = opt;
        aIncomeType.innerHTML = opt;
        bIncomeType.innerHTML = opt;
    };

    const destroyAll = () => {
        lineChart?.destroy(); lineChart = null;
        totalBarChart?.destroy(); totalBarChart = null;
        monthBarChart?.destroy(); monthBarChart = null;

        typeChart?.destroy(); typeChart = null;
        expTypeChart?.destroy(); expTypeChart = null;
        incTypeChart?.destroy(); incTypeChart = null;

        customCompareChart?.destroy(); customCompareChart = null;
    };

    const baseOpts = {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: true, position: "bottom" } }
    };

    const monthLabelTr = (ym) => {
        const [y, m] = String(ym).split("-");
        return `${m}.${y}`;
    };

    const sum = (arr) => arr.reduce((a, b) => a + (Number(b) || 0), 0);

    const expLabel = () => {
        const k = (expenseKind?.value || "total");
        if (k === "tax_debt") return "Gider Vergisi";
        if (k === "tax_income") return "Gelir Vergisi";
        if (k === "tax_total" || k === "tax") return "Toplam Vergi";
        if (k === "normal") return "Normal Gider";
        return "Toplam Gider";
    };

    const renderMonthlyLine = (labels, expVals, incVals) => {
        lineChart?.destroy();

        const datasets = incVals
            ? [
                { label: expLabel(), data: expVals, borderColor: "rgba(220,53,69,1)" },
                { label: "Gelir", data: incVals, borderColor: "rgba(25,135,84,1)" }
            ]
            : [
                {
                    label: mode.value === "income" ? "Gelir" : expLabel(),
                    data: expVals,
                    borderColor: mode.value === "income" ? "rgba(25,135,84,1)" : "rgba(220,53,69,1)"
                }
            ];

        lineChart = new Chart(document.getElementById("dailyChart"), {
            type: "line",
            data: { labels, datasets },
            options: { ...baseOpts, scales: { y: { beginAtZero: true } } }
        });
    };

    const renderTotalBar = (expenseTotal, incomeTotal) => {
        totalBarChart?.destroy();

        totalBarChart = new Chart(document.getElementById("barChart"), {
            type: "bar",
            data: {
                labels: [""],
                datasets: [
                    {
                        label: expLabel(),
                        data: [expenseTotal],
                        backgroundColor: "rgba(220,53,69,0.85)",
                        borderColor: "rgba(220,53,69,1)",
                        borderWidth: 1,
                        barThickness: 26,
                        maxBarThickness: 30
                    },
                    {
                        label: "Gelir",
                        data: [incomeTotal],
                        backgroundColor: "rgba(25,135,84,0.85)",
                        borderColor: "rgba(25,135,84,1)",
                        borderWidth: 1,
                        barThickness: 26,
                        maxBarThickness: 30
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: true, position: "bottom" } },
                scales: {
                    x: { grid: { display: false }, ticks: { display: false } },
                    y: { beginAtZero: true }
                }
            }
        });
    };

    const renderMonthCompareBar = (labels, expVals, incVals) => {
        monthBarChart?.destroy();

        monthBarChart = new Chart(document.getElementById("monthBarChart"), {
            type: "bar",
            data: {
                labels,
                datasets: [
                    {
                        label: expLabel(),
                        data: expVals,
                        backgroundColor: "rgba(220,53,69,0.75)",
                        borderColor: "rgba(220,53,69,1)",
                        borderWidth: 1,
                        barThickness: 18,
                        maxBarThickness: 22
                    },
                    {
                        label: "Gelir",
                        data: incVals,
                        backgroundColor: "rgba(25,135,84,0.75)",
                        borderColor: "rgba(25,135,84,1)",
                        borderWidth: 1,
                        barThickness: 18,
                        maxBarThickness: 22
                    }
                ]
            },
            options: { ...baseOpts, scales: { y: { beginAtZero: true } } }
        });
    };

    const renderDonut = (canvasId, labels, values) => {
        const el = document.getElementById(canvasId);
        return new Chart(el, {
            type: "doughnut",
            data: { labels, datasets: [{ data: values }] },
            options: { ...baseOpts }
        });
    };

    const handleModeUi = () => {
        const m = mode.value;

        if (m === "expense") {
            wrapDebtType.classList.remove("d-none");
            wrapExpenseStatus.classList.remove("d-none");
            wrapExpenseKind.classList.remove("d-none");

            wrapIncomeType.classList.add("d-none");
            wrapIncomeStatus.classList.add("d-none");

            incomeTypeFilter.value = "";
            incomeStatus.value = "total";
        } else if (m === "income") {
            wrapDebtType.classList.add("d-none");
            wrapExpenseStatus.classList.add("d-none");
            wrapExpenseKind.classList.add("d-none");

            wrapIncomeType.classList.remove("d-none");
            wrapIncomeStatus.classList.remove("d-none");

            debtTypeFilter.value = "";
            expenseStatus.value = "total";
            expenseKind.value = "total";
        } else {
            wrapDebtType.classList.remove("d-none");
            wrapExpenseStatus.classList.remove("d-none");
            wrapExpenseKind.classList.remove("d-none");

            wrapIncomeType.classList.remove("d-none");
            wrapIncomeStatus.classList.remove("d-none");
        }
    };

    const load = async () => {
        // eski grafikler için destroy
        lineChart?.destroy(); lineChart = null;
        totalBarChart?.destroy(); totalBarChart = null;
        monthBarChart?.destroy(); monthBarChart = null;
        typeChart?.destroy(); typeChart = null;
        expTypeChart?.destroy(); expTypeChart = null;
        incTypeChart?.destroy(); incTypeChart = null;

        const m = mode.value;

        const data = await app.get("/Statistics/DataV2", {
            from: from.value,
            to: to.value,
            mode: m,
            debtTypeId: debtTypeFilter.value,
            incomeTypeId: incomeTypeFilter.value,
            expenseStatus: expenseStatus.value || "total",
            incomeStatus: incomeStatus.value || "total",
            expenseKind: expenseKind.value || "total"
        });

        if (m === "compare") {
            singleWrap.classList.add("d-none");
            compareWrap.classList.remove("d-none");
            barWrap.classList.remove("d-none");
            monthBarWrap.classList.remove("d-none");

            const labels = data.expense.monthly.map(x => monthLabelTr(x.month));
            const expVals = data.expense.monthly.map(x => Number(x.total));
            const incVals = data.income.monthly.map(x => Number(x.total));

            renderMonthlyLine(labels, expVals, incVals);
            renderTotalBar(sum(expVals), sum(incVals));
            renderMonthCompareBar(labels, expVals, incVals);

            expTypeChart = renderDonut(
                "expTypeChart",
                data.expense.byType.map(x => x.type),
                data.expense.byType.map(x => Number(x.total))
            );

            incTypeChart = renderDonut(
                "incTypeChart",
                data.income.byType.map(x => x.type),
                data.income.byType.map(x => Number(x.total))
            );

            return;
        }

        compareWrap.classList.add("d-none");
        barWrap.classList.add("d-none");
        monthBarWrap.classList.add("d-none");
        singleWrap.classList.remove("d-none");

        const labels = data.monthly.map(x => monthLabelTr(x.month));
        const vals = data.monthly.map(x => Number(x.total));

        renderMonthlyLine(labels, vals);

        typeChart = renderDonut(
            "typeChart",
            data.byType.map(x => x.type),
            data.byType.map(x => Number(x.total))
        );
    };

    // ✅ NEW: custom compare bar fetch + render
    const colorForMetric = (m) => {
        m = String(m || "").toLowerCase();
        if (m.startsWith("income")) return "rgba(25,135,84,0.85)";      // green
        if (m.startsWith("expense")) return "rgba(220,53,69,0.85)";     // red
        if (m.startsWith("tax")) return "rgba(255,193,7,0.85)";         // amber
        return "rgba(13,110,253,0.85)";                                 // blue
    };

    const renderCustomCompareBar = (aVal, bVal) => {
        customCompareChart?.destroy();

        const aLabel = aMetric.options[aMetric.selectedIndex].text;
        const bLabel = bMetric.options[bMetric.selectedIndex].text;

        customCompareChart = new Chart(document.getElementById("customCompareChart"), {
            type: "bar",
            data: {
                labels: [aLabel, bLabel],
                datasets: [
                    {
                        label: "Tutar",
                        data: [Number(aVal || 0), Number(bVal || 0)],
                        backgroundColor: [colorForMetric(aMetric.value), colorForMetric(bMetric.value)],
                        borderWidth: 0,
                        barThickness: 24,
                        maxBarThickness: 28
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: { y: { beginAtZero: true } }
            }
        });
    };

    const loadCustomCompareBar = async () => {
        const data = await app.get("/Statistics/CompareBar", {
            aFrom: aFrom.value, aTo: aTo.value, aMetric: aMetric.value,
            aDebtTypeId: aDebtType.value, aIncomeTypeId: aIncomeType.value,
            aExpenseStatus: aExpenseStatus.value, aIncomeStatus: aIncomeStatus.value,

            bFrom: bFrom.value, bTo: bTo.value, bMetric: bMetric.value,
            bDebtTypeId: bDebtType.value, bIncomeTypeId: bIncomeType.value,
            bExpenseStatus: bExpenseStatus.value, bIncomeStatus: bIncomeStatus.value
        });

        renderCustomCompareBar(data.a, data.b);
    };

    // Events
    btn.addEventListener("click", load);
    mode.addEventListener("change", () => { handleModeUi(); load(); });

    debtTypeFilter.addEventListener("change", load);
    incomeTypeFilter.addEventListener("change", load);

    expenseStatus.addEventListener("change", load);
    incomeStatus.addEventListener("change", load);

    expenseKind.addEventListener("change", () => {
        if (expenseKind.value === "tax_income") debtTypeFilter.value = "";
        load();
    });

    from.addEventListener("change", load);
    to.addEventListener("change", load);

    // custom compare events
    btnCompareBar.addEventListener("click", loadCustomCompareBar);
    [aMetric, bMetric, aFrom, aTo, bFrom, bTo, aDebtType, bDebtType, aIncomeType, bIncomeType, aExpenseStatus, bExpenseStatus, aIncomeStatus, bIncomeStatus]
        .forEach(el => el.addEventListener("change", loadCustomCompareBar));

    (async () => {
        await loadDebtTypes();
        await loadIncomeTypes();

        // ✅ ilk açılışta karşılaştır
        mode.value = "compare";
        expenseStatus.value = "total";
        incomeStatus.value = "total";

        handleModeUi();
        await load();

        // custom compare default: global range
        aFrom.value = from.value; aTo.value = to.value;
        bFrom.value = from.value; bTo.value = to.value;

        await loadCustomCompareBar();
    })();
})();