// wwwroot/js/menu.js
(() => {
    const btn = document.getElementById("btnToggleSidebar");
    const sidebar = document.querySelector(".sidebar");
    if (!btn || !sidebar) return;

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

    const toggle = () => {
        if (isOpen()) close();
        else open();
    };

    // default kapalı
    close();

    btn.addEventListener("click", (e) => {
        e.stopPropagation();
        toggle();
    });

    backdrop.addEventListener("click", close);

    sidebar.querySelectorAll("a").forEach(a => {
        a.addEventListener("click", () => close());
    });

    document.addEventListener("click", (e) => {
        if (!isOpen()) return;

        const clickedInsideSidebar = sidebar.contains(e.target);
        const clickedToggleBtn = btn.contains(e.target);

        if (!clickedInsideSidebar && !clickedToggleBtn) {
            close();
        }
    });

    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && isOpen()) {
            close();
        }
    });
})();