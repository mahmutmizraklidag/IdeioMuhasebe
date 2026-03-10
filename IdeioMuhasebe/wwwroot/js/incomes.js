(() => {
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btnApply = document.getElementById("btnApply");
    const typeFilter = document.getElementById("typeFilter");
    const receivedFilter = document.getElementById("receivedFilter");

    const body = document.getElementById("incomeBody");

    // side form
    const sideTitle = document.getElementById("sideTitle");
    const btnNew = document.getElementById("btnNewIncome");
    const btnReset = document.getElementById("btnResetIncome");
    const btnSave = document.getElementById("btnSaveIncome");

    const incomeId = document.getElementById("incomeId");
    const incomeTypeId = document.getElementById("incomeTypeId");
    const incomeName = document.getElementById("incomeName");
    const incomeNet = document.getElementById("incomeNetAmount");
    const incomeTax = document.getElementById("incomeTaxAmount");
    const incomeAmount = document.getElementById("incomeAmount");
    const incomeDueDate = document.getElementById("incomeDueDate");
    const incomePayer = document.getElementById("incomePayer");
    const incomeIsReceived = document.getElementById("incomeIsReceived");
    const errEl = document.getElementById("incomeErr");

    // recurring
    const incomeIsRecurring = document.getElementById("incomeIsRecurring");
    let wrapPeriod = document.getElementById("wrapIncomePeriod");
    let periodCountEl = document.getElementById("incomePeriodCount");

    app.setDefaultMonth(from, to);

    const setDefaultDueDate = () => {
        if (incomeDueDate) incomeDueDate.value = new Date().toISOString().slice(0, 10);
    };

    const calcTotal = () => {
        if (!incomeNet || !incomeTax) return;
        const net = Number(incomeNet.value || 0);
        const tax = Number(incomeTax.value || 0);
        const total = net + tax;
        if (incomeAmount) incomeAmount.value = total.toFixed(2);
    };

    // ✅ Eğer cshtml’de yoksa, “kaç dönem” alanını otomatik ekle
    const ensurePeriodDom = () => {
        if (!incomeIsRecurring) return;

        wrapPeriod = document.getElementById("wrapIncomePeriod");
        periodCountEl = document.getElementById("incomePeriodCount");

        if (wrapPeriod && periodCountEl) return;

        // checkbox’ın bulunduğu yere yakın bir yere ekleyelim
        const host =
            incomeIsRecurring.closest(".form-check")?.parentElement ||
            incomeIsRecurring.parentElement ||
            document.body;

        // zaten eklenmiş olabilir (id yoksa) — tekrar eklemeyelim
        if (document.getElementById("wrapIncomePeriod")) {
            wrapPeriod = document.getElementById("wrapIncomePeriod");
            periodCountEl = document.getElementById("incomePeriodCount");
            return;
        }

        const div = document.createElement("div");
        div.className = "mt-2 d-none";
        div.id = "wrapIncomePeriod";
        div.innerHTML = `
      <label class="form-label">Kaç dönem yenilensin?</label>
      <input type="number" min="1" class="form-control" id="incomePeriodCount" placeholder="Boş bırakılırsa sınırsız" />
      <div class="small text-muted mt-1">Örn: 12 = 12 ay boyunca her ay oluşur.</div>
    `;

        // checkbox bloğunun hemen altına ekle
        const formCheck = incomeIsRecurring.closest(".form-check");
        if (formCheck && formCheck.nextSibling) {
            formCheck.insertAdjacentElement("afterend", div);
        } else {
            host.appendChild(div);
        }

        wrapPeriod = document.getElementById("wrapIncomePeriod");
        periodCountEl = document.getElementById("incomePeriodCount");
    };

    const togglePeriod = () => {
        ensurePeriodDom();
        const on = !!incomeIsRecurring?.checked;

        if (wrapPeriod) wrapPeriod.classList.toggle("d-none", !on);
        if (!on && periodCountEl) periodCountEl.value = "";
    };

    const resetForm = () => {
        if (sideTitle) sideTitle.textContent = "Gelir Ekle";
        if (incomeId) incomeId.value = "0";
        if (incomeName) incomeName.value = "";
        if (incomeNet) incomeNet.value = "";
        if (incomeTax) incomeTax.value = "";
        if (incomePayer) incomePayer.value = "";
        if (incomeIsReceived) incomeIsReceived.checked = false;

        if (incomeIsRecurring) incomeIsRecurring.checked = false;
        ensurePeriodDom();
        if (periodCountEl) periodCountEl.value = "";

        if (errEl) errEl.classList.add("d-none");
        setDefaultDueDate();
        calcTotal();
        togglePeriod();
    };

    const openEdit = (x) => {
        if (sideTitle) sideTitle.textContent = "Gelir Düzenle";
        if (incomeId) incomeId.value = x.id;
        if (incomeTypeId) incomeTypeId.value = x.incomeTypeId;
        if (incomeName) incomeName.value = x.name;

        if (incomeNet) incomeNet.value = x.netAmount ?? 0;
        if (incomeTax) incomeTax.value = x.taxAmount ?? 0;
        if (incomeDueDate) incomeDueDate.value = x.dueDate;
        if (incomePayer) incomePayer.value = x.payer ?? "";
        if (incomeIsReceived) incomeIsReceived.checked = !!x.isReceived;

        if (incomeIsRecurring) incomeIsRecurring.checked = !!x.recurringIncomeId;

        ensurePeriodDom();
        if (periodCountEl) periodCountEl.value = (x.recurringPeriodCount ?? "") || "";

        if (errEl) errEl.classList.add("d-none");
        calcTotal();
        togglePeriod();

        document.querySelector(".sticky-side")?.scrollIntoView({ behavior: "smooth", block: "start" });
    };

    const loadTypes = async () => {
        const data = await app.get("/IncomeTypes/Options");
        if (typeFilter) {
            typeFilter.innerHTML =
                `<option value="">Tümü</option>` +
                data.list.map((x) => `<option value="${x.id}">${x.name}</option>`).join("");
        }
        if (incomeTypeId) {
            incomeTypeId.innerHTML = data.list.map((x) => `<option value="${x.id}">${x.name}</option>`).join("");
        }
    };

    const load = async () => {
        const params = {
            from: from?.value,
            to: to?.value,
            incomeTypeId: typeFilter?.value || ""
        };

        if (receivedFilter) {
            params.isReceived =
                receivedFilter.value === ""
                    ? ""
                    : (receivedFilter.value === "1" ? "true" : "false");
        }

        const data = await app.get("/Incomes/List", params);

        body.innerHTML = "";
        data.list.forEach((x) => {
            const encoded = encodeURIComponent(JSON.stringify(x));

            
            const periodBadge = x.recurringPeriodText
                ? `<span class="badge bg-light text-dark border ms-2">${x.recurringPeriodText}</span>`
                : "";
            body.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td><a href="/IncomeTypes/Details/${x.incomeTypeId}" class="link-light">${x.incomeType}</a></td>
          
            <td class="fw-semibold">${x.name}${periodBadge}</td>
          
          <td class="text-muted">${x.payer ?? ""}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, x.isReceived)}</td>
          <td class="text-end text-nowrap">
            <div class="d-inline-flex flex-nowrap gap-1">
              <button class="btn btn-sm btn-primary py-1 px-2" data-act="edit" data-json="${encoded}">Düzenle</button>
              <button class="btn btn-sm btn-danger  py-1 px-2" data-act="del" data-id="${x.id}">Sil</button>
            </div>
          </td>
        </tr>
      `);
        });
    };

    body.addEventListener("click", async (e) => {
        const btn = e.target.closest("button");
        if (!btn) return;

        const act = btn.dataset.act;

        if (act === "edit") {
            const x = JSON.parse(decodeURIComponent(btn.dataset.json));
            openEdit(x);
            return;
        }

        if (act === "del") {
            const id = Number(btn.dataset.id);
            if (!confirm("Silmek istediğine emin misin?")) return;

            await app.postJson("/Incomes/Delete", id);
            if (incomeId && Number(incomeId.value) === id) resetForm();
            await load();
        }
    });

    btnSave?.addEventListener("click", async () => {
        const net = Number(incomeNet?.value || 0);
        const tax = Number(incomeTax?.value || 0);

        ensurePeriodDom();

        const payload = {
            id: Number(incomeId?.value || 0),
            incomeTypeId: Number(incomeTypeId?.value || 0),
            name: (incomeName?.value || "").trim(),
            netAmount: net,
            taxAmount: tax,
            dueDate: incomeDueDate?.value,
            payer: (incomePayer?.value || "").trim(),
            isReceived: !!incomeIsReceived?.checked,

            isRecurring: !!incomeIsRecurring?.checked,
            periodCount: incomeIsRecurring?.checked ? Number(periodCountEl?.value || 0) : null
        };

        if (!payload.incomeTypeId || !payload.name || !payload.dueDate || (payload.netAmount + payload.taxAmount) <= 0) {
            if (errEl) errEl.classList.remove("d-none");
            return;
        }

        await app.postJson("/Incomes/Upsert", payload);
        resetForm();
        await load();
    });

    btnNew?.addEventListener("click", resetForm);
    btnReset?.addEventListener("click", resetForm);

    btnApply?.addEventListener("click", load);
    typeFilter?.addEventListener("change", load);
    receivedFilter?.addEventListener("change", load);
    from?.addEventListener("change", load);
    to?.addEventListener("change", load);

    incomeNet?.addEventListener("input", calcTotal);
    incomeTax?.addEventListener("input", calcTotal);
    incomeIsRecurring?.addEventListener("change", togglePeriod);

    (async () => {
        await loadTypes();
        resetForm();
        await load();
    })();
})();