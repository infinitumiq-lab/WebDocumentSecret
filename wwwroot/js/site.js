// Automatically dismiss transient status messages; validation errors remain visible.
document.addEventListener("DOMContentLoaded", () => {
    document.querySelectorAll(".success-message, .info-message").forEach(message => {
        if (message.dataset.keepVisible === "true") return;
        window.setTimeout(() => {
            message.classList.add("message-dismissed");
            window.setTimeout(() => message.remove(), 250);
        }, 4000);
    });
});
