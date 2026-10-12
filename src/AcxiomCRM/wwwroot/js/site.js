// AcxiomCRM - shared UI behaviour
(function () {
    "use strict";

    // Off-canvas sidebar on tablet/mobile
    var sidebar = document.getElementById("sidebar");
    var toggle = document.getElementById("sidebarToggle");
    var backdrop = document.getElementById("sidebarBackdrop");

    function setSidebar(open) {
        if (!sidebar) return;
        sidebar.classList.toggle("open", open);
        if (backdrop) backdrop.hidden = !open;
        if (toggle) toggle.setAttribute("aria-expanded", open ? "true" : "false");
    }

    if (toggle) {
        toggle.addEventListener("click", function () {
            setSidebar(!sidebar.classList.contains("open"));
        });
    }
    if (backdrop) {
        backdrop.addEventListener("click", function () { setSidebar(false); });
    }
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") setSidebar(false);
    });

    // Confirmation dialog for destructive / state-changing forms: <form data-confirm="message">
    var modalEl = document.getElementById("confirmModal");
    var pendingForm = null;
    var pendingSubmitter = null;
    if (modalEl && window.bootstrap) {
        var modal = new bootstrap.Modal(modalEl);
        var body = document.getElementById("confirmModalBody");
        var ok = document.getElementById("confirmModalOk");

        document.addEventListener("submit", function (e) {
            var form = e.target;
            if (!(form instanceof HTMLFormElement) || !form.dataset.confirm || form.dataset.confirmed === "true") return;
            // Let jQuery validation block invalid forms first.
            if (window.jQuery && jQuery(form).data("validator") && !jQuery(form).valid()) return;
            e.preventDefault();
            pendingForm = form;
            pendingSubmitter = e.submitter || null;
            body.textContent = form.dataset.confirm;
            ok.textContent = form.dataset.confirmButton || "Confirm";
            ok.className = "btn " + (form.dataset.confirmStyle || "btn-danger");
            modal.show();
        }, true);

        ok.addEventListener("click", function () {
            if (!pendingForm) return;
            pendingForm.dataset.confirmed = "true";
            modal.hide();
            if (pendingForm.requestSubmit) {
                // Keep the clicked button so its name/value (e.g. outcome=Won) is posted.
                if (pendingSubmitter) pendingForm.requestSubmit(pendingSubmitter); else pendingForm.requestSubmit();
            } else {
                if (pendingSubmitter && pendingSubmitter.name) {
                    var hidden = document.createElement("input");
                    hidden.type = "hidden"; hidden.name = pendingSubmitter.name; hidden.value = pendingSubmitter.value;
                    pendingForm.appendChild(hidden);
                }
                pendingForm.submit();
            }
            pendingForm = null;
            pendingSubmitter = null;
        });
    }

    // Show/hide groups driven by a radio/select: <div data-show-when="RelatedType=Lead">
    function refreshConditional() {
        document.querySelectorAll("[data-show-when]").forEach(function (el) {
            var parts = el.getAttribute("data-show-when").split("=");
            var name = parts[0], values = parts[1].split("|");
            var checked = document.querySelector('input[name="' + name + '"]:checked');
            var input = checked || document.querySelector('[name="' + name + '"]:not([type=radio]):not([type=hidden])');
            var current = input ? (input.type === "checkbox" ? String(input.checked) : input.value) : "";
            var visible = values.indexOf(current) >= 0;
            el.hidden = !visible;
            el.querySelectorAll("input, select, textarea").forEach(function (field) {
                field.disabled = !visible;
            });
        });
    }
    document.addEventListener("change", function (e) {
        if (e.target && e.target.name) refreshConditional();
    });
    refreshConditional();

    // Auto-submit filter selects marked with data-autosubmit
    document.querySelectorAll("select[data-autosubmit]").forEach(function (s) {
        s.addEventListener("change", function () { s.form.submit(); });
    });

    // Dashboard period selector: show custom range inputs only for "custom"
    var period = document.getElementById("periodSelect");
    if (period) {
        var custom = document.getElementById("customRange");
        var sync = function () { if (custom) custom.hidden = period.value !== "custom"; };
        period.addEventListener("change", function () {
            sync();
            if (period.value !== "custom") period.form.submit();
        });
        sync();
    }
})();
