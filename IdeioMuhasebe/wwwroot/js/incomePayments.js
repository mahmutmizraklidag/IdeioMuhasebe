(() => {
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btn = document.getElementById("btnApply");
    const typeFilter = document.getElementById("typeFilter");

    const colUnpaid = document.getElementById("unpaid");
    const colPaid = document.getElementById("paid");

    app.setDefaultMonth(from, to);

    const loadTypes = async () => {
        const data = await app.get("/IncomeTypes/Options");
        typeFilter.innerHTML = `<option value="">Tümü</option>` + data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");
    };

    const cardHtml = (x, isPaidList) => {
        const dateText = app.formatDateTr(x.dueDate);
        const badgeHtml = isPaidList ? `<span class="badge bg-success">Tahsil Edildi</span>` : app.dueBadgeHtml(x.dueDate, false);

        const periodBadge = x.recurringPeriodText
            ? `<span class="badge bg-light text-dark border ms-2">${x.recurringPeriodText}</span>`
            : "";

        return `
      <div class="kanban-card" data-id="${x.id}" data-due="${x.dueDate}">
        <div class="d-flex justify-content-between">
          <div class="fw-semibold">${x.name}${periodBadge}</div>
          <div class="text-muted small">${dateText}</div>
        </div>
        <div class="small text-muted">${x.incomeType}</div>
        
        <div class="mt-1 fw-semibold">${app.money(x.amount)}</div>
        <div class="due-badge mt-1">${badgeHtml}</div>
      </div>
    `;
    };

    const load = async () => {
        const data = await app.get("/IncomePayments/List", {
            from: from.value,
            to: to.value,
            incomeTypeId: typeFilter.value
        });

        colUnpaid.innerHTML = data.unpaid.map(x => cardHtml(x, false)).join("");
        colPaid.innerHTML = data.paid.map(x => cardHtml(x, true)).join("");
    };

    const setReceived = async (id, isReceived) => {
        await app.postJson("/IncomePayments/SetReceived", { id: Number(id), isReceived: !!isReceived });
    };

    const updateBadge = (item, isPaidTarget) => {
        const due = item.dataset.due;
        const box = item.querySelector(".due-badge");
        if (!box) return;

        box.innerHTML = isPaidTarget
            ? `<span class="badge bg-success">Tahsil Edildi</span>`
            : app.dueBadgeHtml(due, false);
    };

    const mkSortable = (el, isPaidTarget) => new Sortable(el, {
        group: "inc",
        animation: 160,
        onAdd: async (evt) => {
            const item = evt.item;
            const id = item.dataset.id;

            try {
                await setReceived(id, isPaidTarget);
                updateBadge(item, isPaidTarget);
            } catch (e) {
                alert("Güncellenemedi");
                await load();
            }
        }
    });

    btn.addEventListener("click", load);
    typeFilter.addEventListener("change", load);
    from.addEventListener("change", load);
    to.addEventListener("change", load);

    (async () => {
        await loadTypes();
        await load();

        mkSortable(colUnpaid, false);
        mkSortable(colPaid, true);
    })();
})();