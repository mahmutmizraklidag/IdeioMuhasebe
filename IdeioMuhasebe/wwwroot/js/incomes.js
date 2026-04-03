(() => {
    const tokenEl = document.querySelector('input[name="__RequestVerificationToken"]');
    const token = tokenEl ? tokenEl.value : "";

    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btnApply = document.getElementById("btnApply");

    const typeFilter = document.getElementById("typeFilter");
    const receivedFilter = document.getElementById("receivedFilter");
    const body = document.getElementById("incomeBody");

    const sideTitle = document.getElementById("sideTitle");
    const btnNew = document.getElementById("btnNewIncome");
    const btnAddFab = document.getElementById("btnAddIncome");
    const btnSave = document.getElementById("btnSaveIncome");
    const btnReset = document.getElementById("btnResetIncome");
    const err = document.getElementById("incomeErr");

    const incomeId = document.getElementById("incomeId");
    const incomeTypeId = document.getElementById("incomeTypeId");
    const incomeName = document.getElementById("incomeName");
    const incomeNetAmount = document.getElementById("incomeNetAmount");
    const incomeTaxAmount = document.getElementById("incomeTaxAmount");
    const incomeAmount = document.getElementById("incomeAmount");
    const incomeDueDate = document.getElementById("incomeDueDate");
    const incomePayer = document.getElementById("incomePayer");
    const incomeIsReceived = document.getElementById("incomeIsReceived");
    const incomeIsRecurring = document.getElementById("incomeIsRecurring");
    const incomePeriodCount = document.getElementById("incomePeriodCount");
    const wrapIncomePeriod = document.getElementById("wrapIncomePeriod");

    const setCurrentMonthRange = () => {
        const now = new Date();
        const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
        const lastDay = new Date(now.getFullYear(), now.getMonth() + 1, 0);

        const format = (date) => {
            const y = date.getFullYear();
            const m = String(date.getMonth() + 1).padStart(2, "0");
            const d = String(date.getDate()).padStart(2, "0");
            return `${y}-${m}-${d}`;
        };

        from.value = format(firstDay);
        to.value = format(lastDay);
        incomeDueDate.value = format(new Date());
    };

    const calcTotal = () => {
        const net = Number(incomeNetAmount.value || 0);
        const tax = Number(incomeTaxAmount.value || 0);
        incomeAmount.value = (net + tax).toFixed(2);
    };

    const toggleRecurringWrap = () => {
        if (incomeIsRecurring.checked) {
            wrapIncomePeriod.classList.remove("d-none");
        } else {
            wrapIncomePeriod.classList.add("d-none");
            incomePeriodCount.value = "";
        }
    };

    const resetForm = () => {
        sideTitle.textContent = "Gelir Ekle";
        incomeId.value = "0";
        incomeTypeId.value = "";
        incomeName.value = "";
        incomeNetAmount.value = "";
        incomeTaxAmount.value = "";
        incomeAmount.value = "";
        incomeDueDate.value = "";
        incomePayer.value = "";
        incomeIsReceived.checked = false;
        incomeIsRecurring.checked = false;
        incomePeriodCount.value = "";
        wrapIncomePeriod.classList.add("d-none");
        err.classList.add("d-none");

        const now = new Date();
        const y = now.getFullYear();
        const m = String(now.getMonth() + 1).padStart(2, "0");
        const d = String(now.getDate()).padStart(2, "0");
        incomeDueDate.value = `${y}-${m}-${d}`;
    };

    const loadTypes = async () => {
        const data = await app.get("/IncomeTypes/Options");

        typeFilter.innerHTML =
            `<option value="">Tümü</option>` +
            data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");

        incomeTypeId.innerHTML =
            `<option value="">Seçiniz</option>` +
            data.list.map(x => `<option value="${x.id}">${x.name}</option>`).join("");
    };

    const rowHtml = (x) => {
        const status = x.isReceived
   ? `<span class="badge bg-success">Tahsil Edildi</span>`
   : `<span class="badge bg-warning text-dark">Tahsil Edilmedi</span>`;

        const recurringBadge = x.recurringPeriodText
   ? `<span class="badge bg-light text-dark border ms-1">${x.recurringPeriodText}</span>`
   : "";

        return `
            <tr>
                <td>${app.formatDateTr(x.dueDate)}</td>
                <td>${x.incomeType}</td>
                <td>${x.name} ${recurringBadge}</td>
                <td>${x.payer ?? "-"}</td>
                <td class="text-end">${app.money(x.amount)}</td>
                <td class="text-end">${status}</td>
                <td class="text-end">
                    <button class="btn btn-sm btn-light me-1 btn-edit" data-id="${x.id}">Düzenle</button>
                    <button class="btn btn-sm btn-danger btn-del" data-id="${x.id}">Sil</button>
                </td>
            </tr>
        `;
    };

    let currentList = [];

    const load = async () => {
        const isReceived =
            receivedFilter.value === ""
   ? null
   : receivedFilter.value === "1";

        const data = await app.get("/Incomes/List", {
       from: from.value,
       to: to.value,
       incomeTypeId: typeFilter.value,
       isReceived
   });

        currentList = data.list || [];
        body.innerHTML = currentList.map(rowHtml).join("");

        if (!currentList.length) {
            body.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center text-muted py-4">Kayıt bulunamadı.</td>
                </tr>
            `;
        }
    };

    const fillForm = (x) => {
        sideTitle.textContent = "Gelir Düzenle";
        incomeId.value = x.id;
        incomeTypeId.value = x.incomeTypeId;
        incomeName.value = x.name ?? "";
        incomeNetAmount.value = x.netAmount ?? 0;
        incomeTaxAmount.value = x.taxAmount ?? 0;
        incomeAmount.value = x.amount ?? 0;
        incomeDueDate.value = x.dueDate;
        incomePayer.value = x.payer ?? "";
        incomeIsReceived.checked = !!x.isReceived;
        incomeIsRecurring.checked = !!x.recurringIncomeId;
        incomePeriodCount.value = x.recurringPeriodCount ?? "";
        toggleRecurringWrap();
        err.classList.add("d-none");
    };

    const save = async () => {
        err.classList.add("d-none");

        const payload = {
            id: Number(incomeId.value || 0),
            incomeTypeId: Number(incomeTypeId.value || 0),
            name: incomeName.value.trim(),
            netAmount: Number(incomeNetAmount.value || 0),
            taxAmount: Number(incomeTaxAmount.value || 0),
            amount: Number(incomeAmount.value || 0),
            dueDate: incomeDueDate.value,
            payer: incomePayer.value.trim(),
            isReceived: incomeIsReceived.checked,
            isRecurring: incomeIsRecurring.checked,
            periodCount: incomePeriodCount.value ? Number(incomePeriodCount.value) : null
        };

        if (!payload.incomeTypeId || !payload.name || !payload.dueDate || payload.amount <= 0) {
            err.classList.remove("d-none");
            return;
        }

        await fetch("/Incomes/Upsert", {
       method: "POST",
       headers: {
           "Content-Type": "application/json",
           "RequestVerificationToken": token
       },
       body: JSON.stringify(payload)
   }).then(async r => {
       const data = await r.json();
       if (!r.ok || data.ok === false) {
           throw new Error(data.message || "Kayıt başarısız.");
       }
   });

        resetForm();
        await load();
    };

    const del = async (id) => {
        if (!confirm("Bu kaydı silmek istediğinize emin misiniz?")) return;

        await fetch("/Incomes/Delete", {
       method: "POST",
       headers: {
           "Content-Type": "application/json",
           "RequestVerificationToken": token
       },
       body: JSON.stringify(id)
   }).then(async r => {
       const data = await r.json();
       if (!r.ok || data.ok === false) {
           throw new Error(data.message || "Silme işlemi başarısız.");
       }
   });

        await load();
        if (Number(incomeId.value) === Number(id)) {
            resetForm();
        }
    };

    body.addEventListener("click", async (e) => {
       const editBtn = e.target.closest(".btn-edit");
       const delBtn = e.target.closest(".btn-del");

       if (editBtn) {
           const id = Number(editBtn.dataset.id);
           const item = currentList.find(x => x.id === id);
           if (item) fillForm(item);
           return;
       }

       if (delBtn) {
           const id = Number(delBtn.dataset.id);
           try {
               await del(id);
           } catch (ex) {
               alert(ex.message || "Silinemedi.");
           }
       }
   });

    btnApply.addEventListener("click", load);
    btnNew.addEventListener("click", resetForm);
    btnReset.addEventListener("click", resetForm);
    btnSave.addEventListener("click", async () => {
       try {
           await save();
       } catch (ex) {
           alert(ex.message || "Kaydedilemedi.");
       }
   });

    if (btnAddFab) {
        btnAddFab.addEventListener("click", resetForm);
    }

    incomeNetAmount.addEventListener("input", calcTotal);
    incomeTaxAmount.addEventListener("input", calcTotal);
    incomeIsRecurring.addEventListener("change", toggleRecurringWrap);

    from.addEventListener("change", load);
    to.addEventListener("change", load);
    typeFilter.addEventListener("change", load);
    receivedFilter.addEventListener("change", load);

    (async () => {
        setCurrentMonthRange();
        await loadTypes();
        resetForm();
        await load();
    })();
})();