# AllThatMatters frontend integration notes

## Implemented in this version

- Account registration and login use the existing API routes.
- Email verification page is available at `/User/Verify`. The email link should include the token as a query string, for example `/User/Verify?token=THE_URL_ENCODED_TOKEN`.
- The verification page POSTs `{ "token": "...", "purpose": "Signup" }` to `/api/Auth/validate-token` and shows a safe success/error message.
- Successful login stores access/refresh tokens in the ASP.NET Core server-side session. The access token is sent as a Bearer token for authorized API requests.
- The vault now calls `GET /api/Documents` rather than the failing `/api/FormSchemas` endpoint.
- The frontend aggregates `documentTypeId` values and `dynamicAttributes` KEY NAMES into a form preview. It deliberately does not render existing document attribute VALUES or filenames.
- Choosing another document type changes the generated fields. Input types are inferred from field names as a temporary demo because the Documents payload does not contain field-type, required, label, or dropdown-option metadata.
- Logout clears the server-side session.
- Forgot-password and token-based reset-password screens remain wired to the intended API contract. The backend reset endpoint currently requires authorization and a current password, so the reset submission will not work until the backend implements the token-based reset flow.

## Important current limitations / backend alignment

1. `GET /api/FormSchemas` currently returns HTTP 500, so this version does not call it.
2. `GET /api/Documents` is used only to demonstrate dynamic form generation. A list of existing documents is not a real form schema. The backend should eventually provide a dedicated schema endpoint that returns document type name, field key, display label, input type, required/optional flag, and options. The frontend can then stop inferring field types.
3. The frontend has not yet submitted, encrypted, or saved any document. The generated form is a preview only; no vault plaintext is sent to the backend by this preview form.
4. The email-verification API should be accessible anonymously. If a global/fallback authorization policy is enabled, backend should mark `POST /api/Auth/validate-token` as `[AllowAnonymous]`.
5. The email template must link to the deployed frontend URL plus `/User/Verify?token=...`, with the token URL-encoded. Do not put passwords or access tokens in verification links.
6. Forgot/reset password should use a short-lived, single-use reset token. The backend must not return that token in the API response body or logs.
7. Keep account authentication separate from vault encryption keys. Never log passwords, access/refresh tokens, verification tokens, reset tokens, or document contents.

## Local test checklist

1. Build and run the MVC project.
2. Register a test account and confirm the API sends the verification email.
3. Open the verification email link. Confirm it opens `/User/Verify` with the token prefilled, then submits to the backend and shows success.
4. Sign in with the verified account. Confirm the vault page opens.
5. Confirm browser network/API logs show `GET /api/Documents` with `Authorization: Bearer ...` and HTTP 200.
6. Choose Invoice, Passport / Identity Document, Contract, Tax Document, and Bank Statement. Confirm each choice changes the field list.
7. Enter sample data and press Preview. Confirm the page validates fields without saving or printing the values.
8. Sign out and confirm the session is cleared and the landing page opens.
9. Forgot/reset-password still needs backend alignment before an end-to-end success test is possible.


## UX updates in v3
- Login form asks the browser not to autocomplete and clears visible login fields on page load/pageshow. Browser password managers may still override these hints; users should remove saved credentials from the browser/password manager on shared devices.
- Success and information notifications automatically dismiss after four seconds.
- Removed the always-visible “Preview mode” paragraph. The user sees a details review only after selecting Review details, and can hide it.
- Added optional local-only supporting-file selection (PDF, common images, DOC/DOCX) for future attachment support. Files are not uploaded yet.
- Added illustrative templates for insurance, will/estate planning, contracts, passport, identity/licence, property/mortgage, education, tax, and financial documents. Contract type and identity-document issuer/ID-number fields are separate. Demo templates are clearly labelled and are not persisted as official backend schemas.
- Never treat this client-only preview as secure storage. Do not deploy a claim that the data is saved or encrypted until the secure document API and client-side encryption flow are implemented and tested.


## Follow-up frontend changes (2026-10-16)
- Verification page no longer displays a token input. When opened with a token, it shows a verifying state, removes the query string from the visible address bar, and posts the token automatically to the existing validation flow. A failed/missing token produces an error state. Resend verification is not connected yet because the backend endpoint contract has not been provided.
- Vault dynamic form now includes an optional supporting-file picker for PDF, common image formats, and Word documents. It checks extension and 10 MB per-file limit and previews filenames/sizes only. Files are NOT converted to Base64, uploaded, encrypted, or saved in this version.
- Do not implement client-side encryption from a user email/username plus SHA-256 as a substitute for a key. Before encrypted submission, agree on a reviewed design covering encryption algorithm/mode, key derivation, nonce/IV, authentication tag, key generation/recovery, payload contract, and retrieval/decryption behavior. A salt is not itself an encryption key.
- Document submission/retrieval is still not implemented. FormSchemas is used to render form definitions and preview data only.

## Form submission preparation (2026-10-16)

- The dynamic form now has a **Prepare save request (demo)** action after review. It validates the selected schema's fields and assembles a draft object in browser memory containing `documentTypeId`, `documentTypeName`, and a `fields` dictionary.
- This draft object is intentionally not displayed, logged, posted to an API, written to local/session storage, or persisted. The UI explicitly tells the user that nothing has been saved.
- The draft object is an internal frontend preparation shape only. It is **not** the final backend request contract and must not be treated as an encrypted envelope.
- The real submit service should be implemented only after inspecting the backend endpoint and agreeing on the encryption envelope. It should then send ciphertext/envelope only, use the authenticated server-side session for the access token, and never trust a client-supplied OwnerId.
- Before connecting the real endpoint, confirm: route and HTTP method; request DTO; success/error response shape; document ID field; field/schema identifier expectations; max request body size; whether display metadata is encrypted or stored separately; and how the client retrieves and decrypts a saved record.
- The selected supporting files remain local-only previews and are deliberately excluded from the draft form payload.


## Backend encrypted-envelope contract checked against DocumentSecret-kosi_dev (uploaded 2026-10-16)

The uploaded backend source now contains `POST /api/EncryptedDocuments` and `GET /api/EncryptedDocuments/{id}`. Both require Bearer authentication. The POST body is `{ "encryptedEnvelope": "<base64-string>" }`. The backend decodes the string as Base64 and currently rejects decoded envelope payloads larger than 1,000,000 bytes. It obtains OwnerId from the authenticated claims; the frontend must not send OwnerId. The response is an `ApiResponse<EncryptedCreateResponseDto>` with `flag`, `message`, `statusCode`, and `data.recordId`. The GET response uses the same wrapper and returns `data.recordId`, `data.envelope` (Base64), and `data.createdAt`.

This contract is now visible in the supplied source ZIP, but it does not mean frontend submission is ready for production. The frontend still needs an agreed, tested encryption-envelope format and must Base64-encode the serialized envelope bytes exactly once before POSTing. The backend's 1 MB limit applies to decoded bytes, not the Base64 string length. The current frontend draft object is not yet the encrypted envelope.


## Save and retrieval status (2026-10-18)

- The dynamic form currently validates and previews form values only. It does **not** save them to the frontend or backend. The demo “Prepare save request” action is not a real save operation.
- The backend source includes `POST /api/EncryptedDocuments` and `GET /api/EncryptedDocuments/{id}`. The POST requires a Base64-encoded encrypted envelope and optionally accepts `documentTypeId`; the API derives owner identity from the authenticated token. The GET returns the stored envelope as Base64 and checks that the authenticated caller owns the record.
- The frontend has not yet implemented encryption/envelope creation, a real POST save, or decryption on retrieval. Do not send plaintext form values to the encrypted-document endpoint.
- The current API GET route retrieves one record by GUID; a user-facing saved-documents list needs a safe owner-scoped list endpoint or another agreed listing contract. The uploaded backend controller has an owner route, but the service implementation in the same source indicates the owner-list method was removed; confirm/fix this with the backend teammate before relying on it.
- Form submission and retrieval must only be wired after the frontend and backend agree on the envelope structure, key derivation, nonce/tag representation, and decryption behavior.


## Encryption round-trip update (2026-10-20)

- Vault forms now have a separate vault-password field and a real encrypt/save action. The password is used only in browser JavaScript and is cleared from the field after each operation.
- The browser encrypts document field JSON with AES-256-GCM and derives a key using Argon2id with per-document random salt. Only the Base64-encoded envelope is sent to `/Vault/Save`, which proxies to `POST /api/EncryptedDocuments`.
- `/Vault/Retrieve?id=...` proxies authenticated retrieval and returns the envelope to the browser for local decryption. The retrieved JSON is displayed in the UI after successful decryption.
- The user must keep the record ID to retrieve a saved document until the backend provides a current-user list endpoint or the frontend can safely obtain the user-owned record list.
- Supporting files remain local-only; this version does not upload or encrypt the selected files.
- Recovery key/email/phone recovery is not implemented yet. Do not advertise recovery as available. It requires a separate wrapped-key protocol and backend storage contract.
- The Argon2id script is currently loaded from a version-pinned CDN URL. Vendor the audited library/WASM locally before production deployment.
- The environment used to prepare this ZIP did not have the .NET SDK, so a `dotnet build` could not be run here. JavaScript syntax was checked with `node --check`; complete integration tests still need to be run against the live/local backend.
