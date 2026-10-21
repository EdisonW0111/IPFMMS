document.addEventListener("DOMContentLoaded", function () {

    const wrapper = document.getElementById("dashboardWrapper");
    const toggle = document.getElementById("sidebarToggle");
    const mobileButton = document.getElementById("mobileMenuButton");
    const overlay = document.getElementById("dashboardOverlay");

    const storageKey = "ipfmms-sidebar-collapsed";

    // Restore user's sidebar preference.
    try {
        if (localStorage.getItem(storageKey) === "true") {
            wrapper.classList.add("sidebar-collapsed");
        }
    } catch (error) {
        // Continue with the default layout if storage is unavailable.
    }

    function updateToggle() {
        const collapsed =
            wrapper.classList.contains("sidebar-collapsed");

        toggle.setAttribute("aria-expanded", String(!collapsed));
        toggle.setAttribute(
            "aria-label",
            collapsed ? "Expand sidebar" : "Collapse sidebar"
        );

        const icon = toggle.querySelector("i");
        const label = toggle.querySelector(".nav-label");

        icon.className = collapsed
            ? "bi bi-layout-sidebar-inset-reverse"
            : "bi bi-layout-sidebar-inset";

        label.textContent = collapsed
            ? "Expand Sidebar"
            : "Collapse Sidebar";
    }

    updateToggle();

    // Enable normal sidebar animations after initial rendering.
    requestAnimationFrame(function () {
        requestAnimationFrame(function () {
            wrapper.classList.remove("sidebar-initializing");
        });
    });


    // Desktop expand/collapse.
    toggle.addEventListener("click", function () {
        wrapper.classList.toggle("sidebar-collapsed");

        try {
            localStorage.setItem(
                storageKey,
                String(wrapper.classList.contains("sidebar-collapsed"))
            );
        } catch (error) {
            // Sidebar still works without persistent storage.
        }

        updateToggle();
    });

    function closeMobileSidebar() {
        wrapper.classList.remove("mobile-sidebar-open");
        mobileButton.setAttribute("aria-expanded", "false");
        mobileButton.setAttribute(
            "aria-label", "Open navigation menu"
        );
    }

    // Mobile sidebar.
    mobileButton.addEventListener("click", function () {
        const isOpen =
            wrapper.classList.toggle("mobile-sidebar-open");

        mobileButton.setAttribute("aria-expanded", String(isOpen));
        mobileButton.setAttribute(
            "aria-label",
            isOpen ? "Close navigation menu" : "Open navigation menu"
        );
    });

    // Click outside to close.
    overlay.addEventListener("click", closeMobileSidebar);

    // Close with Escape.
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape") {
            closeMobileSidebar();
        }
    });

    // Profile dropdown

    const profileButton =
        document.getElementById("profileDropdownButton");

    const profileMenu =
        document.getElementById("profileDropdownMenu");

    if (profileButton && profileMenu) {

        function closeProfileMenu() {
            profileMenu.hidden = true;
            profileButton.setAttribute("aria-expanded", "false");
        }

        profileButton.addEventListener("click", function (event) {
            event.stopPropagation();

            const isOpen = !profileMenu.hidden;

            profileMenu.hidden = isOpen;
            profileButton.setAttribute(
                "aria-expanded",
                String(!isOpen)
            );
        });

        document.addEventListener("click", function (event) {
            if (!profileButton.contains(event.target) &&
                !profileMenu.contains(event.target)) {
                closeProfileMenu();
            }
        });

        document.addEventListener("keydown", function (event) {
            if (event.key === "Escape") {
                closeProfileMenu();
                profileButton.focus();
            }
        });
    }
});
