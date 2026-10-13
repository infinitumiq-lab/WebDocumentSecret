(() => {
    const clearCredentials = () => {
        const form = document.querySelector('form[asp-action="Login"], form[action*="Login"]');
        if (!form) return;
        const username = form.querySelector('input[name="Username"]');
        const password = form.querySelector('input[name="Password"]');
        if (username) username.value = "";
        if (password) password.value = "";
    };
    document.addEventListener("DOMContentLoaded", clearCredentials);
    window.addEventListener("pageshow", clearCredentials);
})();
