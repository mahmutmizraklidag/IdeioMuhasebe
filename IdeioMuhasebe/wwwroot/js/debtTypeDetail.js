(() => {
    const typeId = Number(document.getElementById("debtTypeId").value);
    const from = document.getElementById("fromDate");
    const to = document.getElementById("toDate");
    const btn = document.getElementById("btnApply");
    const body = document.getElementById("body");

    app.setDefaultMonth(from, to);

    const load = async () => {
        const data = await app.get("/DebtTypeDetailApi/Data", { debtTypeId: typeId, from: from.value, to: to.value });

        document.getElementById("cTotal").textContent = app.money(data.cards.total);
        document.getElementById("cPaid").textContent = app.money(data.cards.paid);
        document.getElementById("cRemaining").textContent = app.money(data.cards.remaining);

        body.innerHTML = "";
        data.list.forEach(x => {
            const status = x.isPaid
                ? `<span class="badge bg-success">Ödendi</span>`
                : `<span class="badge bg-warning text-dark">Bekliyor</span>`;

            body.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${x.dueDate}</td>
          <td class="fw-semibold">${x.name}</td>
          <td class="text-muted">${x.payee ?? ""}</td>
          <td class="text-end">${app.money(x.amount)}</td>
          <td class="text-end">${status}</td>
        </tr>
      `);
        });
    };

    btn.addEventListener("click", load);
    load();
})();