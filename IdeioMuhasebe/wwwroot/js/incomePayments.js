(() => {
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btn = document.getElementById("btnApply");
    const typeFilter = document.getElementById("typeFilter");

    const colUnpaid = document.getElementById("unpaid");
    const colPaid = document.getElementById("paid");

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

    const loadTypes = async () => {
        const data = await app.get("/IncomeTypes/Options");
        typeFilter.innerHTML =
            `<option value="">Tümü</option>` +
            data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");
    };

    const cardHtml = (x, isPaidList) => {
        const dateText = app.formatDateTr(x.dueDate);

        const badgeHtml = isPaidList
   ? `<span class="badge bg-success">Tahsil Edildi</span>`
   : app.dueBadgeHtml(x.dueDate, false);

        const periodBadge = x.recurringPeriodText
   ? `<span class="badge bg-light text-dark border ms-2">${x.recurringPeriodText}</span>`
   : "";

        const remaining = Number(x.remainingAmount ?? x.amount ?? 0);
        const total = Number(x.totalAmount ?? x.amount ?? 0);
        const received = Number(x.receivedAmount ?? 0);
        const overreceived = Number(x.overreceivedAmount ?? 0);

        const partialBtn = `<button type="button"
                 class="btn btn-sm btn-light partial-receive-btn"
                 data-id="${x.id}"
                 data-remaining="${remaining}"
                 data-total="${total}"
                 data-received="${received}"
                 title="Tahsilat ekle">
            <i class="bi bi-pencil"></i>
         </button>`;

        const carryInfo = overreceived > 0
   ? `<div class="small text-success mt-1">Devreden fazla tahsilat: ${app.money(overreceived)}</div>`
   : "";

        return `
      <div class="kanban-card" data-id="${x.id}" data-due="${x.dueDate}">
        <div class="d-flex justify-content-between align-items-start gap-2">
          <div class="fw-semibold">${x.name}${periodBadge}</div>
          <div class="d-flex align-items-center gap-2">
            ${partialBtn}
            <div class="text-muted small">${dateText}</div>
          </div>
        </div>

        <div class="small text-muted">${x.incomeType}</div>

        <div class="mt-1 fw-semibold">
          ${app.money(isPaidList ? total : remaining)}
        </div>

        <div class="small text-muted">
          Toplam: ${app.money(total)} · Tahsil Edilen: ${app.money(received)}
        </div>

        ${carryInfo}

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
        await app.postJson("/IncomePayments/SetReceived", {
       id: Number(id),
       isReceived: !!isReceived
   });
    };

    const addPartialReceive = async (id, amount) => {
        await app.postJson("/IncomePayments/AddPartialReceive", {
       id: Number(id),
       amount: Number(amount)
   });
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
       group: "incomepay",
       animation: 160,
       onAdd: async (evt) => {
           const item = evt.item;
           const id = item.dataset.id;

           try {
               await setReceived(id, isPaidTarget);
               updateBadge(item, isPaidTarget);
               await load();
           } catch (e) {
               alert(e?.message || "Güncellenemedi");
               await load();
           }
       }
   });

    const onPartialReceiveClick = async (e) => {
        const btn = e.target.closest(".partial-receive-btn");
        if (!btn) return;

        const id = Number(btn.dataset.id);
        const remaining = Number(btn.dataset.remaining || 0);
        const total = Number(btn.dataset.total || 0);
        const received = Number(btn.dataset.received || 0);

        const raw = prompt(
       `Eklenecek tahsilat tutarı giriniz.\nToplam: ${app.money(total)}\nŞu an tahsil edilen: ${app.money(received)}\nKalan: ${app.money(remaining)}`
   );

        if (raw === null) return;

        const amount = Number(String(raw).replace(",", "."));

        if (!amount || amount <= 0) {
            alert("Geçerli bir tutar girin.");
            return;
        }

        try {
            await addPartialReceive(id, amount);
            await load();
        } catch (err) {
            alert(err?.message || "Tahsilat kaydedilemedi.");
        }
    };

    colUnpaid.addEventListener("click", onPartialReceiveClick);
    colPaid.addEventListener("click", onPartialReceiveClick);

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