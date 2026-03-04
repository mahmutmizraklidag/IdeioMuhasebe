(() => {
    const body = document.getElementById("userBody");
    const btnNew = document.getElementById("btnNewUser");

    const modalEl = document.getElementById("userModal");
    const modal = new bootstrap.Modal(modalEl);

    const titleEl = document.getElementById("userModalTitle");
    const idEl = document.getElementById("userId");
    const usernameEl = document.getElementById("username");
    const pwdEl = document.getElementById("password");
    const pwdHint = document.getElementById("pwdHint");
    const errEl = document.getElementById("userErr");

    const btnSave = document.getElementById("btnSaveUser");

    const openNew = () => {
        titleEl.textContent = "Yeni Kullanıcı";
        idEl.value = "0";
        usernameEl.value = "";
        pwdEl.value = "";
        pwdHint.classList.add("d-none");
        errEl.classList.add("d-none");
        modal.show();
    };

    const openEdit = (x) => {
        titleEl.textContent = "Kullanıcı Düzenle";
        idEl.value = x.id;
        usernameEl.value = x.username;
        pwdEl.value = "";
        pwdHint.classList.remove("d-none");
        errEl.classList.add("d-none");
        modal.show();
    };

    const load = async () => {
        const data = await app.get("/Users/List");
        body.innerHTML = "";

        data.list.forEach(x => {
            body.insertAdjacentHTML("beforeend", `
        <tr>
          <td class="text-muted">${x.id}</td>
          <td class="fw-semibold">${x.username}</td>
          <td class="text-end">
            <button class="btn btn-sm btn-primary" data-act="edit" data-json='${JSON.stringify(x)}'>Düzenle</button>
            <button class="btn btn-sm btn-danger ms-1" data-act="del" data-id="${x.id}">Sil</button>
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
            openEdit(JSON.parse(btn.dataset.json));
        }

        if (act === "del") {
            const id = Number(btn.dataset.id);
            if (!confirm("Silmek istediğine emin misin?")) return;

            try {
                await app.postJson("/Users/Delete", id);
                await load();
            } catch (err) {
                alert(err.message || "Silinemedi");
            }
        }
    });

    btnSave.addEventListener("click", async () => {
        const payload = {
            id: Number(idEl.value),
            username: usernameEl.value.trim(),
            password: pwdEl.value.trim()
        };

        errEl.classList.add("d-none");
        errEl.textContent = "";

        if (!payload.username) {
            errEl.textContent = "Kullanıcı adı zorunludur.";
            errEl.classList.remove("d-none");
            return;
        }

        // yeni kullanıcıda şifre zorunlu
        if (payload.id === 0 && !payload.password) {
            errEl.textContent = "Şifre zorunludur.";
            errEl.classList.remove("d-none");
            return;
        }

        try {
            await app.postJson("/Users/Upsert", payload);
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