(() => {
    const paidFilter = document.getElementById("paidFilter");
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btnApply = document.getElementById("btnApply");
    const typeFilter = document.getElementById("typeFilter");
    const body = document.getElementById("debtBody");

    const sideTitle = document.getElementById("sideTitle");
    const btnNewDebt = document.getElementById("btnNewDebt");
    const btnResetDebt = document.getElementById("btnResetDebt");
    const btnSaveDebt = document.getElementById("btnSaveDebt");

    const debtId = document.getElementById("debtId");
    const debtTypeId = document.getElementById("debtTypeId");
    const debtName = document.getElementById("debtName");
    const debtAmount = document.getElementById("debtAmount");
    const debtDueDate = document.getElementById("debtDueDate");
    const debtPayee = document.getElementById("debtPayee");
    const debtIsPaid = document.getElementById("debtIsPaid");
    const debtErr = document.getElementById("debtErr");

    // recurring UI
    const debtIsRecurring = document.getElementById("debtIsRecurring");
    const wrapDebtPeriod = document.getElementById("wrapDebtPeriod");
    const debtPeriodCount = document.getElementById("debtPeriodCount");

    app.setDefaultMonth(from, to);

    const setDefaultDueDate = () => {
        debtDueDate.value = new Date().toISOString().slice(0, 10);
    };

    const togglePeriod = () => {
        const on = !!debtIsRecurring?.checked;
        wrapDebtPeriod?.classList.toggle("d-none", !on);
        if (!on && debtPeriodCount) debtPeriodCount.value = "";
    };

    const resetForm = () => {
        sideTitle.textContent = "Borç Ekle";
        debtId.value = "0";
        debtName.value = "";
        debtAmount.value = "";
        debtPayee.value = "";
        debtIsPaid.checked = false;

        if (debtIsRecurring) debtIsRecurring.checked = false;
        if (debtPeriodCount) debtPeriodCount.value = "";

        debtErr.classList.add("d-none");
        setDefaultDueDate();
        togglePeriod();
    };

    const openEdit = (x) => {
        sideTitle.textContent = "Borç Düzenle";
        debtId.value = x.id;
        debtTypeId.value = x.debtTypeId;
        debtName.value = x.name;
        debtAmount.value = x.amount;
        debtDueDate.value = x.dueDate;
        debtPayee.value = x.payee ?? "";
        debtIsPaid.checked = !!x.isPaid;

        // recurring info
        if (debtIsRecurring) debtIsRecurring.checked = !!x.recurringDebtId;
        if (debtPeriodCount) debtPeriodCount.value = (x.recurringPeriodCount ?? "") || "";
        togglePeriod();

        debtErr.classList.add("d-none");
        document.querySelector(".sticky-side")?.scrollIntoView({ behavior: "smooth", block: "start" });
    };

    const loadTypes = async () => {
        const data = await app.get("/DebtTypes/Options");
        typeFilter.innerHTML =
            `<option value="">Tümü</option>` +
            data.list.map((x) => `<option value="${x.id}">${x.name}</option>`).join("");

        debtTypeId.innerHTML = data.list.map((x) => `<option value="${x.id}">${x.name}</option>`).join("");
    };

    const load = async () => {
        const isPaidParam =
            paidFilter?.value === "" ? "" : (paidFilter?.value === "1" ? "true" : "false");

        const params = {
            from: from.value,
            to: to.value,
            debtTypeId: typeFilter.value,
            isPaid: isPaidParam
        };

        const data = await app.get("/Debts/List", params);

        body.innerHTML = "";
        data.list.forEach((x) => {
            const encoded = encodeURIComponent(JSON.stringify(x));

            // ✅ SON 1 DÖNEM yazısı borç adının üstünde
            const warnHtml = x.lastPeriodWarning
                ? `<div class="small text-warning mb-1">Son 1 dönem</div>`
                : ``;

            body.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td><a href="/DebtTypes/Details/${x.debtTypeId}" class="link-light">${x.debtType}</a></td>
          <td class="fw-semibold">
            ${warnHtml}
            <div>${x.name}</div>
          </td>
          <td class="text-muted">${x.payee ?? ""}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${app.dueBadgeHtml(x.dueDate, x.isPaid)}</td>
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

            await app.postJson("/Debts/Delete", id);
            if (Number(debtId.value) === id) resetForm();
            await load();
        }
    });

    btnSaveDebt.addEventListener("click", async () => {
        const payload = {
            id: Number(debtId.value),
            debtTypeId: Number(debtTypeId.value),
            name: debtName.value.trim(),
            amount: Number(debtAmount.value || 0),
            dueDate: debtDueDate.value,
            payee: debtPayee.value.trim(),
            isPaid: debtIsPaid.checked,

            // recurring
            isRecurring: !!debtIsRecurring?.checked,
            periodCount: debtIsRecurring?.checked ? Number(debtPeriodCount?.value || 0) : null
        };

        if (!payload.debtTypeId || !payload.name || !payload.amount || !payload.dueDate) {
            debtErr.classList.remove("d-none");
            return;
        }

        await app.postJson("/Debts/Upsert", payload);
        resetForm();
        await load();
    });

    btnNewDebt?.addEventListener("click", resetForm);
    btnResetDebt?.addEventListener("click", resetForm);

    document.getElementById("btnAddDebt")?.addEventListener("click", () => {
        resetForm();
        document.querySelector(".sticky-side")?.scrollIntoView({ behavior: "smooth", block: "start" });
    });

    btnApply?.addEventListener("click", load);
    typeFilter?.addEventListener("change", load);
    paidFilter?.addEventListener("change", load);
    from?.addEventListener("change", load);
    to?.addEventListener("change", load);

    debtIsRecurring?.addEventListener("change", togglePeriod);

    (async () => {
        await loadTypes();
        resetForm();
        await load();
    })();
})();