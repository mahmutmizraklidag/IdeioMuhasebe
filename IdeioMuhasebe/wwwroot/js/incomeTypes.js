(() => {
    const body = document.getElementById("typeBody");
    const btnNew = document.getElementById("btnNewType");

    const modalEl = document.getElementById("typeModal");
    const modal = new bootstrap.Modal(modalEl);

    const idEl = document.getElementById("typeId");
    const nameEl = document.getElementById("typeName");
    const errEl = document.getElementById("typeErr");
    const btnSave = document.getElementById("btnSaveType");
    const titleEl = document.getElementById("typeModalTitle");

    const openNew = () => {
        titleEl.textContent = "Yeni Gelir Kategorisi";
        idEl.value = "0";
        nameEl.value = "";
        errEl.classList.add("d-none");
        modal.show();
    };

    const openEdit = (x) => {
        titleEl.textContent = "Gelir Kategorisi Düzenle";
        idEl.value = x.id;
        nameEl.value = x.name;
        errEl.classList.add("d-none");
        modal.show();
    };

    const load = async () => {
        const data = await app.get("/IncomeTypes/List");
        body.innerHTML = "";

        data.list.forEach(x => {
            const encoded = encodeURIComponent(JSON.stringify(x));
            const badge = x.lastPeriodWarning
                ? `<span class="badge bg-warning text-dark ms-2">Son 1 dönem</span>`
                : ``;

            body.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${x.id}</td>
          <td class="fw-semibold">${x.name}${badge}</td>
          <td class="text-end text-nowrap">
            <div class="d-inline-flex flex-nowrap gap-1">
              <button class="btn btn-sm btn-light py-1 px-2" data-act="detail" data-id="${x.id}">Detay</button>
              <button class="btn btn-sm btn-primary py-1 px-2" data-act="edit" data-json="${encoded}">Düzenle</button>
              <button class="btn btn-sm btn-danger  py-1 px-2" data-act="del" data-id="${x.id}">Sil</button>
            </div>
          </td>
        </tr>
      `);
        });
    };

    body.addEventListener("click", async (e) => {
        const el = e.target.closest("[data-act]");
        if (!el) return;

        const act = el.dataset.act;

        if (act === "detail") {
            const id = Number(el.dataset.id);
            window.location.href = `/IncomeTypes/Details/${id}`;
            return;
        }

        if (act === "edit") {
            const x = JSON.parse(decodeURIComponent(el.dataset.json));
            openEdit(x);
            return;
        }

        if (act === "del") {
            const id = Number(el.dataset.id);
            if (!confirm("Silmek istediğine emin misin?")) return;

            try {
                await app.postJson("/IncomeTypes/Delete", id);
                await load();
            } catch (err) {
                alert(err.message || "Silinemedi");
            }
        }
    });

    btnSave.addEventListener("click", async () => {
        const payload = { id: Number(idEl.value), name: nameEl.value.trim() };

        errEl.classList.add("d-none");
        errEl.textContent = "";

        if (!payload.name) {
            errEl.textContent = "Kategori adı zorunludur.";
            errEl.classList.remove("d-none");
            return;
        }

        try {
            await app.postJson("/IncomeTypes/Upsert", payload);
            modal.hide();
            await load();
        } catch (err) {
            errEl.textContent = err.message || "Kaydedilemedi";
            errEl.classList.remove("d-none");
        }
    });

    btnNew.addEventListener("click", openNew);

    load();
})();