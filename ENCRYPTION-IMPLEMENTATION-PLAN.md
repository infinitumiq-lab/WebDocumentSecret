# AllThatMatters client-side encryption implementation

## Current stage

This update adds typed frontend API client methods for `POST /api/EncryptedDocuments` and `GET /api/EncryptedDocuments/{id}`. It does **not** yet encrypt, save, or decrypt form values from the Vault UI. The existing demo review/preparation button remains a demo action.

Do not send plaintext form values to the encrypted-document API.

## Proposed envelope (version 1, pending final agreement)

The plaintext document JSON is encrypted in the browser with AES-256-GCM. The envelope JSON should include a version and the information needed to derive the same key and decrypt the content. The serialized envelope is UTF-8 encoded, then Base64 encoded into `encryptedEnvelope`.

For a per-document derived key, proposed fields:

```json
{
  "version": 1,
  "kdf": {
    "algorithm": "Argon2id",
    "salt": "base64-encoded-random-salt",
    "parameters": {
      "degreeOfParallelism": 1,
      "memorySize": 19456,
      "iterations": 4,
      "outputLength": 32
    }
  },
  "encryption": {
    "algorithm": "AES-256-GCM",
    "nonce": "base64-encoded-12-byte-random-nonce",
    "ciphertext": "base64-encoded-ciphertext-and-tag"
  }
}
```

This is a proposal, not a frozen wire contract. Before production, decide whether the salt/key is per-document or per-vault, whether a separate vault secret is required, and exactly how the authentication tag is represented. Web Crypto AES-GCM returns ciphertext with the authentication tag appended; a library-specific implementation may expose them separately. Both sides must agree on one format.

## Security rules

- Never send plaintext form values or the vault key to the backend.
- Never store the vault password/key in localStorage, sessionStorage, cookies, or logs.
- Use a cryptographically secure random salt and a fresh 12-byte nonce for every encryption operation with the same key.
- Reject unsupported envelope versions/algorithms and fail closed on authentication/decryption errors.
- The backend stores and returns the envelope as opaque Base64 data.
- Argon2id settings used for account password hashing are not automatically the approved vault KDF settings. Benchmark and agree parameters for browser runtime and test compatibility.
- Design recovery before production: if the vault secret is lost and there is no recovery key/wrapped key mechanism, encrypted records may be unrecoverable.

## Backend API observed in supplied source

- `POST /api/EncryptedDocuments`, authenticated; request has `encryptedEnvelope` and optional GUID `documentTypeId`; successful wrapper contains `data.recordId`.
- `GET /api/EncryptedDocuments/{id}`, authenticated; returns the encrypted envelope in the response data.
- `GET /api/EncryptedDocuments/owner/{ownerId}` exists and checks the caller against the requested owner (unless admin). A `/mine` endpoint would be cleaner for the frontend because it avoids needing to extract owner ID from a token.
- Current create service rejects invalid Base64 and decoded envelope bytes above 1,000,000.

## Next steps

1. Choose the vault secret and recovery model.
2. Select and vendor a maintained browser Argon2id implementation; avoid an unpinned runtime CDN dependency for production.
3. Freeze the envelope format and create test vectors.
4. Implement and test browser encrypt/decrypt independently.
5. Wire the Vault Save button to encrypt, then call the API client.
6. Add a safe current-user list endpoint or use the existing owner endpoint with a confirmed source for the owner's GUID; then retrieve and decrypt records locally.


## Phase 1 UI and integration status (2026-10-19)

- Login and registration now show a disabled submit button and spinner after a valid submit, with a progress label. The state resets naturally after the server responds and the page reloads.
- Dynamic form controls are explicitly styled with light backgrounds and readable foreground colours to prevent dark browser/theme defaults from making the form appear black.
- The API client already has methods for POST `/api/EncryptedDocuments` and GET `/api/EncryptedDocuments/{id}`. They are not yet connected to the dynamic form because encryption has not been implemented.
- Do not treat the existing “Prepare save request (demo)” as persistence. It only prepares a JavaScript object in memory.
- Before real save/retrieve can be enabled, the frontend must bundle/pin and test a maintained Argon2id implementation, finalize the vault-secret/recovery UX, implement a versioned envelope, and add a round-trip test. AES-GCM must reject wrong keys or tampered envelopes.
- The backend `GET /api/EncryptedDocuments/owner/{ownerId}` requires the owner ID in the URL and verifies it against the authenticated user's claims. For a safer frontend listing contract, prefer a `GET /api/EncryptedDocuments/mine` endpoint that derives owner ID from the token, or obtain the verified user ID from a trusted authenticated source. Do not accept an arbitrary owner ID from user-controlled form input.


## Client-side encryption implementation update (2026-10-20)

- Vault forms now derive a per-document 256-bit key from the separate vault password using Argon2id (time=3, memory=65536 KiB, parallelism=1, output=32 bytes), with a fresh 16-byte salt per document. These are initial implementation parameters and must be benchmarked on supported devices before production.
- AES-256-GCM uses a fresh 12-byte nonce and a 128-bit authentication tag appended to ciphertext as Web Crypto specifies. The JSON envelope is UTF-8 encoded and Base64 encoded for the existing API contract.
- The UI now calls MVC `/Vault/Save` and `/Vault/Retrieve` actions; these proxy authenticated calls to the existing encrypted-document API. The server-side API client receives only the Base64 envelope, never plaintext fields or the vault password.
- Retrieval currently uses a record ID returned by the save response; the owner-scoped list API is not wired because the current contract does not expose a `/mine` route. This is a known limitation, not a claim that the vault list is complete.
- Supporting-file selections remain local-only and are not included in encrypted payloads.
- Recovery-key and verified-email/phone recovery are not implemented by this round-trip flow. A secure recovery implementation needs a random vault data key and separately wrapped key copies (password-derived and recovery-key-derived) plus durable storage for the wraps. Do not claim recovery is available until that protocol and its tests are implemented.
- The browser Argon2id library is loaded from a version-pinned jsDelivr URL. For production, vendor and pin the reviewed library and its WASM assets locally rather than relying on a third-party CDN at runtime.
- Round-trip test checklist: save a sample, retrieve by record ID, decrypt with correct password; verify wrong password and tampered ciphertext fail; verify unsupported version/algorithm fails; verify no plaintext or password appears in network payloads or server logs.
