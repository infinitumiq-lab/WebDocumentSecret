document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll("form.js-loading-form").forEach(form => {
        form.addEventListener("submit", event => {
            // Keep the button active when the browser blocks submission due to native validation.
            if (!form.checkValidity()) return;
            const button = form.querySelector("button.js-loading-submit[type=submit]");
            if (!button || button.dataset.processing === "true") { event.preventDefault(); return; }
            button.dataset.processing = "true";
            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            const label = button.querySelector(".button-label");
            if (label) label.textContent = form.dataset.loadingLabel || "Please wait…";
            form.setAttribute("aria-busy", "true");
        });
    });
});
