// wwwroot/js/menu.js  (FINAL - custom sidebar toggle + outside click close)
(() => {
    const btn = document.getElementById("btnToggleSidebar");
    const sidebar = document.querySelector(".sidebar");
    if (!btn || !sidebar) return;

    // Backdrop (overlay) oluştur
    let backdrop = document.querySelector(".sidebar-backdrop");
    if (!backdrop) {
        backdrop = document.createElement("div");
        backdrop.className = "sidebar-backdrop";
        document.body.appendChild(backdrop);
    }

    const isOpen = () => document.body.classList.contains("sidebar-open");

    const open = () => {
        document.body.classList.add("sidebar-open");
    };

    const close = () => {
        document.body.classList.remove("sidebar-open");
    };

    const toggle = () => (isOpen() ? close() : open());

    // Toggle butonu
    btn.addEventListener("click", (e) => {
        e.stopPropagation();
        toggle();
    });

    // Backdrop'a tıklayınca kapat
    backdrop.addEventListener("click", () => close());

    // Sidebar içindeki linke tıklayınca kapat (mobilde)
    sidebar.querySelectorAll("a").forEach(a => {
        a.addEventListener("click", () => {
            close();
        });
    });

    // Sidebar açıkken dışarı tıklanınca kapat (backdrop yoksa bile çalışır)
    document.addEventListener("click", (e) => {
        if (!isOpen()) return;

        const clickedInsideSidebar = sidebar.contains(e.target);
        const clickedToggleBtn = btn.contains(e.target);

        if (!clickedInsideSidebar && !clickedToggleBtn) close();
    });

    // ESC ile kapat
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && isOpen()) close();
    });
})();