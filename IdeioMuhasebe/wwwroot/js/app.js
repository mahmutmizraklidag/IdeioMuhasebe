window.app = (() => {
    const tokenEl = () => document.getElementById("__RequestVerificationToken");
    const csrf = () => tokenEl()?.value || "";

    const money = (v) => {
        const n = Number(v || 0);
        return "₺" + n.toLocaleString("tr-TR", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    };

    const qs = (obj) => {
        const p = new URLSearchParams();
        Object.entries(obj || {}).forEach(([k, v]) => {
            if (v === null || v === undefined || v === "") return;
            p.set(k, v);
        });
        return p.toString();
    };

    const get = async (url, params) => {
        const u = params ? `${url}?${qs(params)}` : url;
        const r = await fetch(u, { headers: { "Accept": "application/json" } });
        if (!r.ok) throw new Error("GET failed");
        return await r.json();
    };

    const postJson = async (url, body) => {
        const r = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "Accept": "application/json",
                "RequestVerificationToken": csrf()
            },
            body: JSON.stringify(body ?? {})
        });
        const data = await r.json().catch(() => ({}));
        if (!r.ok) throw new Error(data?.message || "POST failed");
        return data;
    };

    const setDefaultMonth = (fromEl, toEl) => {
        const now = new Date();
        const from = new Date(now.getFullYear(), now.getMonth(), 1);
        const to = new Date(now.getFullYear(), now.getMonth() + 1, 0);

        const iso = (d) => d.toISOString().slice(0, 10);
        if (fromEl) fromEl.value = iso(from);
        if (toEl) toEl.value = iso(to);
    };

    // mobile sidebar
    document.addEventListener("click", (e) => {
        if (e.target?.id === "btnToggleSidebar") {
            document.querySelector(".sidebar")?.classList.toggle("open");
        }
    });
    const pad2 = (n) => String(n).padStart(2, "0");

    // "2026-03-06" gibi YYYY-MM-DD stringini timezone kaydırmadan parse eder
    const parseYmd = (s) => {
        if (!s) return null;
        const parts = String(s).split("-");
        if (parts.length !== 3) return null;
        const y = Number(parts[0]);
        const m = Number(parts[1]);
        const d = Number(parts[2]);
        return new Date(y, m - 1, d);
    };

    const formatDateTr = (ymd) => {
        const dt = parseYmd(ymd);
        if (!dt) return "";
        return `${pad2(dt.getDate())}.${pad2(dt.getMonth() + 1)}.${dt.getFullYear()}`;
    };

    const daysDiff = (ymd) => {
        const due = parseYmd(ymd);
        if (!due) return null;

        const today = new Date();
        const t = new Date(today.getFullYear(), today.getMonth(), today.getDate()); // midnight
        const msDay = 24 * 60 * 60 * 1000;
        return Math.round((due - t) / msDay); // due - today
    };

    // Durum badge HTML (Ödendi / Bugün / X gün kaldı / X gün geçti)
    const dueBadgeHtml = (ymd, isPaid) => {
        if (isPaid) return `<span class="badge bg-success">Ödendi</span>`;

        const diff = daysDiff(ymd);
        if (diff === null) return `<span class="badge bg-warning text-dark">Tarih yok</span>`;

        if (diff > 0) return `<span class="badge bg-warning text-dark">${diff} gün kaldı</span>`;
        if (diff === 0) return `<span class="badge bg-info text-dark">Bugün</span>`;
        return `<span class="badge bg-danger">${Math.abs(diff)} gün geçti</span>`;
    };
    const dueBadgeHtmlCustom = (ymd, isDone, doneText) => {
        if (isDone) return `<span class="badge bg-success">${doneText}</span>`;
        return dueBadgeHtml(ymd, false);
    };
    return { csrf, money, qs, get, postJson, setDefaultMonth, formatDateTr, dueBadgeHtml, dueBadgeHtmlCustom };
})();