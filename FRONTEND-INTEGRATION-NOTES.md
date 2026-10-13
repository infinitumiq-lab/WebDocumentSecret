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
