(() => {
    const typeId = Number(document.getElementById("incomeTypeId").value);

    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const status = document.getElementById("statusFilter");
    const btn = document.getElementById("btnApply");

    const cTotal = document.getElementById("cTotal");
    const cReceived = document.getElementById("cReceived");
    const cRemaining = document.getElementById("cRemaining");

    const rows = document.getElementById("rows");

    // aynı davranış: sayfa açılınca bu ay
    app.setDefaultMonth(from, to);

    const load = async () => {
        const data = await app.get("/IncomeTypes/DetailsData", {
            id: typeId,
            from: from.value,
            to: to.value,
            status: status.value
        });

        cTotal.textContent = app.money(data.cards.total);
        cReceived.textContent = app.money(data.cards.received);
        cRemaining.textContent = app.money(data.cards.remaining);

        rows.innerHTML = "";

        data.list.forEach(x => {
            const badge = x.isReceived
                ? `<span class="badge bg-success">Tahsil Edildi</span>`
                : app.dueBadgeHtml(x.dueDate, false); // kaç gün kaldı vb.

            rows.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${app.formatDateTr(x.dueDate)}</td>
          <td class="fw-semibold">${x.name}</td>
          <td class="text-muted">${x.payer ?? ""}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${badge}</td>
        </tr>
      `);
        });
    };

    btn.addEventListener("click", load);
    status.addEventListener("change", load);
    from.addEventListener("change", load);
    to.addEventListener("change", load);

    load();
})();