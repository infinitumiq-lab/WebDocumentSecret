document.addEventListener("DOMContentLoaded", () => {
    const typeSelect = document.getElementById("documentType");
    const formContainer = document.getElementById("documentFormContainer");
    const fieldsContainer = document.getElementById("documentFields");
    const form = document.getElementById("dynamicDocumentForm");
    const preview = document.getElementById("formPreviewMessage");
    const previewDetails = document.getElementById("previewDetails");
    const previewAttachments = document.getElementById("previewAttachments");
    const hidePreview = document.getElementById("hidePreview");
    const supportingFiles = document.getElementById("supportingFiles");
    const attachmentValidation = document.getElementById("attachmentValidation");
    const validationSummary = document.getElementById("formValidationSummary");
    const prepareSubmissionButton = document.getElementById("prepareSubmission");
    const submissionStatus = document.getElementById("submissionStatus");
    let preparedDocumentPayload = null;
    const vaultPasswordInput = document.getElementById("vaultPassword");
    const retrieveIdInput = document.getElementById("retrieveRecordId");
    const retrieveButton = document.getElementById("retrieveDocumentButton");
    const retrievedOutput = document.getElementById("retrievedDocumentOutput");
    if (!typeSelect || !formContainer || !fieldsContainer || !form) return;

    // Only schemas returned by the authenticated API are used; no local demo templates.
    const schemas = Array.isArray(window.allThatMattersFormSchemas) ? window.allThatMattersFormSchemas : [];
    const fieldRules = {
        // These are UI safety limits, not a substitute for backend validation.
        defaultMaxLength: 2000,
        passwordMaxLength: 4096,
        numberMaxLength: 32
    };

    typeSelect.addEventListener("change", () => {
        fieldsContainer.replaceChildren(); clearPreview(); clearValidation(); preparedDocumentPayload = null;
        const schema = schemas.find(item => item.documentTypeId === typeSelect.value);
        if (!schema) { formContainer.hidden = true; return; }
        for (const field of (schema.fields || [])) fieldsContainer.appendChild(createField(field));
        formContainer.hidden = false;
        const note = document.createElement("p");
        note.className = "form-note";
        note.textContent = "Fields are provided by the FormSchemas service. Review your entries before continuing.";
        fieldsContainer.appendChild(note);
    });

    form.addEventListener("submit", event => {
        event.preventDefault();
        clearValidation();
        const errors = validateForm();
        const fileErrors = validateFiles();
        if (fileErrors.length) errors.push(...fileErrors);
        if (errors.length) {
            showValidation(errors);
            const firstInvalid = fieldsContainer.querySelector(".field-invalid");
            firstInvalid?.focus();
            return;
        }

        previewDetails.replaceChildren();
        const selectedSchema = schemas.find(item => item.documentTypeId === typeSelect.value);
        const fieldsByName = new Map((selectedSchema?.fields || []).map(field => [field.keyName, field]));
        for (const [key, value] of new FormData(form).entries()) {
            if (key === "__RequestVerificationToken" || !String(value).trim() || value instanceof File) continue;
            const field = fieldsByName.get(key);
            const label = document.createElement("dt");
            label.textContent = humanize(key);
            const content = document.createElement("dd");
            // Never display password-type values in the review panel.
            content.textContent = String(field?.inputType).toLowerCase() === "password" ? "•••••••• (hidden)" : String(value);
            previewDetails.append(label, content);
        }
        renderAttachmentPreview();
        preview.hidden = false;
        if (submissionStatus) { submissionStatus.hidden = true; submissionStatus.textContent = ""; }
        preparedDocumentPayload = null;
        preview.scrollIntoView({ behavior: "smooth", block: "nearest" });
    });

    prepareSubmissionButton?.addEventListener("click", async () => {
        clearValidation();
        const errors = validateForm();
        const fileErrors = validateFiles();
        if (fileErrors.length) errors.push(...fileErrors);
        if (errors.length) { showValidation(errors); fieldsContainer.querySelector(".field-invalid")?.focus(); return; }
        const password = vaultPasswordInput?.value || "";
        if (password.length < 12) { showValidation(["Enter your vault password (at least 12 characters)."]); vaultPasswordInput?.focus(); return; }
        if (!window.argon2?.hash) { showStatus("The encryption library could not load. Refresh the page and try again.", true); return; }
        const schema = schemas.find(item => item.documentTypeId === typeSelect.value);
        if (!schema) { showValidation(["Choose a document type before saving."]); return; }
        const values = {};
        for (const field of (schema.fields || [])) {
            const control = [...fieldsContainer.querySelectorAll("[data-field-key]")].find(item => item.dataset.fieldKey === field.keyName);
            if (!control) continue;
            const value = String(control.value ?? "").trim();
            if (value) values[field.keyName] = value;
        }
        if (Object.keys(values).length === 0) { showValidation(["Enter at least one field before saving."]); return; }
        const button = prepareSubmissionButton;
        const oldText = button.textContent;
        button.disabled = true; button.textContent = "Encrypting and saving…";
        try {
            const envelope = await encryptDocument({ documentTypeId: schema.documentTypeId, documentTypeName: schema.typeName, fields: values, createdAt: new Date().toISOString() }, password);
            const token = document.querySelector('#dynamicDocumentForm input[name="__RequestVerificationToken"]')?.value;
            const response = await fetch("/Vault/Save", {
                method: "POST", headers: { "Content-Type": "application/json", "RequestVerificationToken": token || "" },
                body: JSON.stringify({ encryptedEnvelope: envelope, documentTypeId: schema.documentTypeId }), credentials: "same-origin"
            });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || !result.success || !result.recordId) throw new Error(result.message || "The encrypted document could not be saved.");
            if (retrieveIdInput) retrieveIdInput.value = result.recordId;
            if (submissionStatus) {
                submissionStatus.textContent = `Encrypted document saved. Record ID: ${result.recordId}. Keep this ID; you will need it to retrieve this record until the list endpoint is connected.`;
                submissionStatus.hidden = false;
            }
            if (vaultPasswordInput) vaultPasswordInput.value = "";
            showStatus("Document encrypted and saved successfully.", false);
        } catch (error) {
            showStatus(error instanceof Error ? error.message : "The document could not be saved.", true);
        } finally { button.disabled = false; button.textContent = oldText; }
    });

    retrieveButton?.addEventListener("click", async () => {
        const id = retrieveIdInput?.value.trim();
        const password = vaultPasswordInput?.value || "";
        if (!id) { showStatus("Enter a document record ID first.", true); retrieveIdInput?.focus(); return; }
        if (password.length < 12) { showStatus("Enter your vault password to decrypt this document.", true); vaultPasswordInput?.focus(); return; }
        if (!window.argon2?.hash) { showStatus("The encryption library could not load. Refresh the page and try again.", true); return; }
        retrieveButton.disabled = true; retrieveButton.textContent = "Retrieving and decrypting…";
        if (retrievedOutput) { retrievedOutput.hidden = true; retrievedOutput.textContent = ""; }
        try {
            const response = await fetch(`/Vault/Retrieve?id=${encodeURIComponent(id)}`, { credentials: "same-origin" });
            const result = await response.json().catch(() => ({}));
            if (!response.ok || !result.success || !result.envelope) throw new Error(result.message || "Could not retrieve this record.");
            const documentData = await decryptDocument(result.envelope, password);
            if (retrievedOutput) { retrievedOutput.textContent = JSON.stringify(documentData, null, 2); retrievedOutput.hidden = false; }
            showStatus("Document retrieved and decrypted in this browser.", false);
        } catch (error) {
            showStatus(error instanceof Error ? error.message : "Decryption failed. Check your vault password and record ID.", true);
        } finally { retrieveButton.disabled = false; retrieveButton.textContent = "Retrieve and decrypt"; if (vaultPasswordInput) vaultPasswordInput.value = ""; }
    });

    async function encryptDocument(documentData, password) {
        const salt = crypto.getRandomValues(new Uint8Array(16));
        const nonce = crypto.getRandomValues(new Uint8Array(12));
        const derived = await window.argon2.hash({ pass: password, salt, time: 3, mem: 65536, hashLen: 32, parallelism: 1, type: window.argon2.ArgonType.Argon2id });
        const key = await crypto.subtle.importKey("raw", derived.hash, { name: "AES-GCM" }, false, ["encrypt"]);
        const plaintext = new TextEncoder().encode(JSON.stringify(documentData));
        const ciphertext = await crypto.subtle.encrypt({ name: "AES-GCM", iv: nonce, tagLength: 128 }, key, plaintext);
        const envelope = {
            version: 1,
            kdf: { algorithm: "Argon2id", salt: toBase64(salt), parameters: { time: 3, memoryKiB: 65536, parallelism: 1, outputLength: 32 } },
            encryption: { algorithm: "AES-256-GCM", nonce: toBase64(nonce), ciphertext: toBase64(new Uint8Array(ciphertext)), tag: "appended-to-ciphertext" }
        };
        return toBase64(new TextEncoder().encode(JSON.stringify(envelope)));
    }

    async function decryptDocument(encodedEnvelope, password) {
        let envelope;
        try { envelope = JSON.parse(new TextDecoder().decode(fromBase64(encodedEnvelope))); }
        catch { throw new Error("The stored encryption envelope is invalid or unsupported."); }
        if (envelope.version !== 1 || envelope.kdf?.algorithm !== "Argon2id" || envelope.encryption?.algorithm !== "AES-256-GCM") throw new Error("This encryption format is not supported by this version of the app.");
        const salt = fromBase64(envelope.kdf.salt), nonce = fromBase64(envelope.encryption.nonce), ciphertext = fromBase64(envelope.encryption.ciphertext);
        const p = envelope.kdf.parameters || {};
        if (salt.length !== 16 || nonce.length !== 12 || p.time !== 3 || p.memoryKiB !== 65536 || p.parallelism !== 1 || p.outputLength !== 32) throw new Error("The encryption parameters are invalid or unsupported.");
        const derived = await window.argon2.hash({ pass: password, salt, time: p.time, mem: p.memoryKiB, hashLen: p.outputLength, parallelism: p.parallelism, type: window.argon2.ArgonType.Argon2id });
        const key = await crypto.subtle.importKey("raw", derived.hash, { name: "AES-GCM" }, false, ["decrypt"]);
        try {
            const plain = await crypto.subtle.decrypt({ name: "AES-GCM", iv: nonce, tagLength: 128 }, key, ciphertext);
            return JSON.parse(new TextDecoder().decode(plain));
        } catch { throw new Error("Decryption failed. The vault password may be incorrect or the stored data may have been altered."); }
    }
    function toBase64(bytes) { let binary = ""; for (const byte of bytes) binary += String.fromCharCode(byte); return btoa(binary); }
    function fromBase64(value) { const binary = atob(value); return Uint8Array.from(binary, c => c.charCodeAt(0)); }
    function showStatus(message, isError) {
        if (!submissionStatus) return;
        submissionStatus.textContent = message; submissionStatus.className = isError ? "error-message" : "success-message"; submissionStatus.hidden = false;
        submissionStatus.scrollIntoView({ behavior: "smooth", block: "nearest" });
    }

    hidePreview?.addEventListener("click", clearPreview);
    supportingFiles?.addEventListener("change", () => {
        clearPreview();
        const errors = validateFiles();
        if (attachmentValidation) {
            attachmentValidation.textContent = errors.join(" ");
            attachmentValidation.hidden = errors.length === 0;
        }
    });
    form.addEventListener("input", event => {
        if (event.target instanceof HTMLElement) event.target.classList.remove("field-invalid");
        clearValidation();
    });
    form.addEventListener("change", event => {
        if (event.target instanceof HTMLElement) event.target.classList.remove("field-invalid");
        clearValidation();
    });
    form.addEventListener("reset", () => window.setTimeout(() => { clearPreview(); clearValidation(); }, 0));

    function createField(field) {
        const wrapper = document.createElement("div");
        wrapper.className = "form-group dynamic-field";
        const key = typeof field.keyName === "string" ? field.keyName : "field";
        const labelText = humanize(key);
        const id = `field-${key.replace(/[^a-zA-Z0-9_-]/g, "-")}`;
        const label = document.createElement("label");
        label.htmlFor = id;
        label.textContent = labelText;
        wrapper.appendChild(label);

        const type = String(field.inputType || "text").toLowerCase();
        let control;
        if (type === "dropdown") {
            control = document.createElement("select");
            control.className = "form-control";
            const placeholder = document.createElement("option");
            placeholder.value = "";
            placeholder.textContent = `Select ${labelText.toLowerCase()}`;
            control.appendChild(placeholder);
            for (const value of (Array.isArray(field.options) ? field.options : [])) {
                const option = document.createElement("option");
                option.value = String(value);
                option.textContent = String(value);
                control.appendChild(option);
            }
        } else if (type === "textarea") {
            control = document.createElement("textarea");
            control.rows = 3;
            control.className = "form-control";
            control.placeholder = placeholderFor(key);
            control.maxLength = fieldRules.defaultMaxLength;
        } else {
            control = document.createElement("input");
            control.className = "form-control";
            const allowed = new Set(["text", "password", "date", "number", "email", "tel", "url"]);
            control.type = allowed.has(type) ? type : "text";
            control.placeholder = placeholderFor(key);
            if (control.type === "password") {
                control.autocomplete = "new-password";
                control.maxLength = fieldRules.passwordMaxLength;
            } else if (control.type === "number") {
                control.maxLength = fieldRules.numberMaxLength;
                control.inputMode = "decimal";
            } else {
                control.maxLength = fieldRules.defaultMaxLength;
                control.autocomplete = "off";
            }
            if (control.type === "date") control.max = "9999-12-31";
            if (control.type === "email") control.maxLength = 254;
        }
        control.id = id;
        control.name = key;
        control.dataset.fieldKey = key;
        control.dataset.inputType = type;
        control.autocomplete = control.autocomplete || "off";
        control.setAttribute("aria-describedby", `${id}-error`);
        wrapper.appendChild(control);

        const hint = document.createElement("small");
        hint.className = "field-hint";
        hint.id = `${id}-error`;
        hint.hidden = true;
        wrapper.appendChild(hint);
        return wrapper;
    }

    function validateFiles() {
        const errors = [];
        if (!supportingFiles) return errors;
        const allowedExtensions = new Set(["pdf", "png", "jpg", "jpeg", "webp", "doc", "docx"]);
        const maxBytes = 10 * 1024 * 1024;
        for (const file of supportingFiles.files || []) {
            const extension = file.name.includes(".") ? file.name.split(".").pop().toLowerCase() : "";
            if (!allowedExtensions.has(extension)) errors.push(`${file.name}: this file type is not supported.`);
            if (file.size > maxBytes) errors.push(`${file.name}: files must be 10 MB or smaller.`);
        }
        return errors;
    }

    function renderAttachmentPreview() {
        if (!previewAttachments || !supportingFiles || !supportingFiles.files.length) return;
        previewAttachments.replaceChildren();
        const heading = document.createElement("h3");
        heading.textContent = "Supporting files (not uploaded)";
        const list = document.createElement("ul");
        for (const file of supportingFiles.files) {
            const item = document.createElement("li");
            item.textContent = `${file.name} — ${formatBytes(file.size)}`;
            list.appendChild(item);
        }
        previewAttachments.append(heading, list);
    }

    function formatBytes(bytes) {
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }

    function validateForm() {
        const errors = [];
        const schema = schemas.find(item => item.documentTypeId === typeSelect.value);
        const schemaFields = schema?.fields || [];
        for (const field of schemaFields) {
            const control = [...fieldsContainer.querySelectorAll("[data-field-key]")].find(item => item.dataset.fieldKey === field.keyName);
            if (!control) continue;
            const value = String(control.value ?? "").trim();
            const type = String(field.inputType || "text").toLowerCase();
            let message = "";
            if (!value) continue; // Schema currently has no required flag, so don't invent required fields.
            if (type === "dropdown" && !(field.options || []).some(option => String(option) === value)) {
                message = "Choose one of the available options.";
            } else if (type === "date" && !isValidDate(value)) {
                message = "Enter a valid date.";
            } else if (type === "number" && !Number.isFinite(Number(value))) {
                message = "Enter a valid number.";
            } else if (type === "email" && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(value)) {
                message = "Enter a valid email address.";
            } else if (type === "url" && !isValidUrl(value)) {
                message = "Enter a valid URL, including https:// when applicable.";
            } else if (type === "tel" && !/^[+\d()\s.-]{5,32}$/.test(value)) {
                message = "Check the phone number format.";
            } else if (type === "password" && value.length > fieldRules.passwordMaxLength) {
                message = `Use no more than ${fieldRules.passwordMaxLength} characters.`;
            } else if (type !== "password" && value.length > fieldRules.defaultMaxLength) {
                message = `Use no more than ${fieldRules.defaultMaxLength} characters.`;
            }
            if (message) {
                control.classList.add("field-invalid");
                const hint = document.getElementById(`${control.id}-error`);
                if (hint) { hint.textContent = message; hint.hidden = false; }
                errors.push(`${humanize(field.keyName)}: ${message}`);
            }
        }
        return errors;
    }

    function isValidDate(value) {
        if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false;
        const date = new Date(`${value}T00:00:00Z`);
        return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value;
    }
    function isValidUrl(value) { try { const url = new URL(value); return url.protocol === "http:" || url.protocol === "https:"; } catch { return false; } }
    function placeholderFor(key) {
        const k = key.toLowerCase();
        if (k.includes("email")) return "Enter email address";
        if (k.includes("phone")) return "Enter phone number";
        if (k.includes("name")) return `Enter ${humanize(key).toLowerCase()}`;
        if (k.includes("number")) return `Enter ${humanize(key).toLowerCase()}`;
        if (k.includes("password") || k.includes("passphrase") || k.includes("secret")) return "Enter value";
        if (k.includes("notes") || k === "note") return "Add any helpful details (optional)";
        if (k.includes("address")) return "Enter address";
        return `Enter ${humanize(key).toLowerCase()}`;
    }
    function showValidation(errors) {
        validationSummary.replaceChildren();
        const title = document.createElement("p");
        title.textContent = "Please check the following fields:";
        validationSummary.appendChild(title);
        const list = document.createElement("ul");
        for (const error of errors) { const li = document.createElement("li"); li.textContent = error; list.appendChild(li); }
        validationSummary.appendChild(list);
        validationSummary.hidden = false;
    }
    function clearValidation() {
        if (attachmentValidation) { attachmentValidation.hidden = true; attachmentValidation.textContent = ""; }
        if (validationSummary) { validationSummary.hidden = true; validationSummary.replaceChildren(); }
        fieldsContainer.querySelectorAll(".field-invalid").forEach(element => element.classList.remove("field-invalid"));
        fieldsContainer.querySelectorAll(".field-hint").forEach(element => { element.hidden = true; element.textContent = ""; });
    }
    function clearPreview() {
        if (preview) preview.hidden = true;
        if (previewDetails) previewDetails.replaceChildren();
        if (previewAttachments) previewAttachments.replaceChildren();
        preparedDocumentPayload = null;
        if (submissionStatus) { submissionStatus.hidden = true; submissionStatus.textContent = ""; }
    }
    function humanize(value) { return String(value || "Field").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").replace(/^./, c => c.toUpperCase()); }
});
