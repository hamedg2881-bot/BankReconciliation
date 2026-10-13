# Chat Context

## Purpose
This repository contains the Bank Reconciliation module of an existing ASP.NET MVC5 + EF6 application. Changes must preserve the existing project structure and conventions.

## Collaboration Rules
- Before changing a file, fetch the current file from GitHub and inspect it.
- Make the smallest necessary change on top of the current GitHub version.
- Do not rewrite files from memory.
- Code should not contain inline comments unless explicitly requested.
- The user will take changed files directly from GitHub, so do not paste full changed files into chat unless explicitly requested.
- After each change, report the changed file(s), a short summary, and the commit SHA.
- Repository branch: main.
- Repository: hamedg2881-bot/BankReconciliation.

## Existing Project Conventions
- ASP.NET MVC5 + EF6.
- Repository queries use _repo.Query.Where(...) and _repo.Query.Any(...); do not use _repo.Query().
- Repositories expose Add() and Remove().
- IUnitOfWorkScope.Commit() handles the transaction and should normally be called once per write operation.
- Current user extraction: var userId = Convert.ToInt32(HttpContext.Current.Session["UserId"]);
- Response wrapper is CDSResponse<T> with Result, Message, and ResponseStatus.
- Success status is 1.
- Errors use ResponseStatus 2.

## Bank Reconciliation Domain
### BankAccount
BankAccount has Id, Title, AccountNumber, IBAN, CenterId/Center. The BankAccount Center is the Center used for reconciliation. A bank account without Center cannot be used for reconciliation.

### BankTransaction
Important fields: Id, BankAccountId, TransactionDate, Credit, Debit, Balance, Description, TrackingNumber.
There is no TransactionTime field. Display time is derived from TransactionDate using TransactionDate.ToString("HH:mm").
Exactly one of Credit/Debit is positive.
The unique transaction set is TransactionDate + Credit + Debit + TrackingNumber + Balance.
BankTransaction fields are not edited by reconciliation. TrackingNumber becomes locked after matching.

### AccountingDoc / AccountingDocDetail
AccountingDocDate is DateTime.
AccountingDocDetail contains Credit, Debit, AccountId/account, nullable AccountingDocRefId, centers, DepositSlip and Cheque relations.
AccountingDocDetail must not be edited or deleted by reconciliation.
Tracking source: DepositSlip.Number, otherwise Cheque.Serial. Do not use RefNo.

### Settings
Allowed accounting account IDs are stored in Setting under key BankReconciliationAllowedAccountIds.
The setting must be read each time; do not cache it.

### Reconciliation
BankReconciliation contains Id, BankAccountId, FromDate, ToDate, State, FinalizedDate, FinalizedByUserId, Matches.
Reconciliation ranges for the same bank account must not overlap.
State: Open = 0, Finalized = 1.
Create behavior for the Display/Load UX:
1. If the exact same BankAccountId + FromDate + ToDate reconciliation already exists, return that existing reconciliation.
2. Otherwise, if the requested range overlaps another reconciliation for the same bank account, return an error.
3. Otherwise create a new reconciliation.
Current overlap error: بازه زمانی وارد شده با یک مغایرت بانکی موجود تداخل دارد.

### Match
BankReconciliationMatch contains ReconciliationId, BankTransactionId, AccountingDocDetailId, MatchedAmount, MatchType, MatchDate.
Supported matching: Normal = 1 bank transaction to 1 accounting detail; Group = 1 bank transaction to N accounting details OR N bank transactions to 1 accounting detail. N:M is not supported.
Previous matches across all reconciliations must be considered when calculating remaining amounts so a transaction/detail cannot be double matched.
Normal match requires exact remaining amount.
Finalized reconciliation: Finalize does not require all transactions to be matched; Reopen clears finalized fields; Delete is forbidden while finalized; Delete removes reconciliation and matches, never bank transactions or accounting details.

## Candidate Rules
Candidate dates are within +/- 2 calendar days.
Candidate must belong to an allowed accounting account, have the same Center as the BankAccount, have the opposite accounting direction, and have remaining amount > 0.
Normal candidate mode only returns CanMatch == true and requires exact amount.
Group candidate mode returns CanGroupMatch == true.
Candidate DTO exposes amount/date differences and tracking flags.
AutoMatch is only 1:1, with exactly one candidate, exact amount, non-empty exact tracking number.

## Current UI / JavaScript
Main files: BankReconciliation.js, BankReconciliation.css, Index.cshtml.
UI is Bootstrap 4 + jQuery + DataTables, RTL, Persian.
Important IDs: #BankAccountId, #FromDate, #ToDate, #btnLoad, #btnRefresh, #btnFinalize, #txtSearch, #btnAutoMatch, #bankTransactions, #accountingCandidates, #btnMatch, #btnGroupMatch, #btnOtherMatch, #btnRemoveMatch.
DataTables are server-side and use dataTablesCurrentLanguage, the existing dom layout, single row selection with selector td:not(:last-child), RequestVerificationToken from #forgeryToken, and rowId values Id / AccountingDocDetailId.
The Display button calls Create. Create now behaves as an idempotent load for an exact existing reconciliation and creates only when the range is free.

## Current GitHub State
Recent commits:
- Create request CSRF header fix: c9633c36aad46c53c29afd8f952fff2c6f0ad9fc
- Create exact-existing-reconciliation behavior: 9b049cfa57029f51cebf56973fcf4bf1a377ff9c
Recent file blob SHAs:
- BankReconciliation.js: c6cfbe2549a5c5aa66941b5cbcf1d56b3773998f
- BankReconciliation.css: 85577fc1a965e4c539a06070eed265647fb35686
- BankReconciliationController.cs: d450938d93215d0d3f847d6e319e4314f8d2ea2f
- BankReconciliationService.cs: b0ac1b53880770c0c1c6ac95cf13d9415990029e
- Index.cshtml: a060c593802f1bb07483a23c9d2584d46dde3e1e
- BankReconciliationCandidateService.cs: 6fe6420edec7f12078de5c3ff5eca6aced5e000b

## Current UX Flow
Display/Load: exact existing reconciliation -> load it; no existing reconciliation and free range -> create then load it; overlap with another reconciliation -> show error.
Bank table and candidate table are server-side DataTables.
The user prefers minimal, incremental changes and wants the actual GitHub files to remain the source of truth.

## Current Rules Update (2026-10-13)

### AutoMatch
- AutoMatch is 1:1 only. AccountingDocDate must exactly equal BankTransaction.TransactionDate.
- Direction must be opposite; remaining amounts must be equal and greater than zero.
- Score: exact amount +100; exact or partial direct tracking-number match +50; tracking number inside bank description +50; counterpart Center title inside bank description +30; exact date +20.
- Match only if the unique top score is strictly greater than 120. Amount plus exact date scores 120 and is insufficient. If top candidates tie, do not auto-match.
- Sort by descending score, then date difference, then accounting detail ID. Track amounts allocated during the current run; set Automatic type, MatchDate and audit fields; commit once.
- Result DTO fields include MatchScore, MatchScoreDetails, SecondCandidateScore and ScoreDifference.

### Candidate score and text matching
- Exact amount +100; exact or partial tracking-number match +50; tracking inside bank description +50; counterpart Center title inside bank description +30; exact date +20; one-day difference +10.
- Candidate DTO includes MatchScore and MatchScoreDetails. Text matching normalizes Arabic Yeh (ي/ى) to Persian Yeh (ی), Arabic Kaf (ك) to Persian Kaf (ک), Persian/Arabic digits to English digits, and whitespace. Direct tracking-number comparison in candidate scoring and AutoMatch accepts exact equality or a partial match when either normalized number contains the other; empty values never count as a match.
- Candidate date tolerance is configurable from 0 to 2 calendar days. Candidate must use an allowed account, same Center, opposite direction, and positive remaining amount. Normal mode requires exact remaining amount and CanMatch; group mode uses CanGroupMatch.
- Always account for matches across all reconciliations when computing remaining amounts. Group matching supports 1:N or N:1, not N:M.

### UI and history
- Relevant files: BankReconciliation.js, BankReconciliation.css, Index.cshtml, BankReconciliationController.cs, BankReconciliationService.cs, BankReconciliationCandidateService.cs.
- History may contain 1,000 rows; keep server-side DataTables with column filters.
- Bank transaction Type filter: DataTables column uses `data: "Credit"` (instead of `data: null`) so the server receives a valid field key; `GetPage` handles the `Credit` key as the transaction type filter and matches Persian values `واریز` / `برداشت`.
- Clearable date filters: #FromDate, #ToDate, #bankFilterTransactionDate, #candidateFilterAccountingDocDate, #historyFilterFromDate, #historyFilterToDate.
- Modal date pickers may need initialization on shown.bs.modal. Date filtering uses MdPersianDateTimePicker("getDate") and Persian calendar formatting. Do not normalize Persian digits server-side; user explicitly rejected this.
- DataTables scrollX was disabled to avoid horizontal overflow. Preserve unless a tested fix requires otherwise. Ensure date input labels stay initialized after history selection.
- Candidate rows show score and score-details tooltip. Selecting a bank transaction loads saved matches from /BankReconciliation/GetMatches and shows accounting date, document number, account, Center, description, tracking, matched amount and type. Removing a match uses /BankReconciliation/RemoveMatch and refreshes relevant views.
- POST requests use RequestVerificationToken from #forgeryToken.

### Implementation conventions
- Verify actual repository interfaces and DTO property names before edits. Current code uses Add() and Delete(); do not assume Remove(). Repository queries use _repo.Query.Where(...) and _repo.Query.Any(...), not _repo.Query().
- One _unitOfWorkScope.Commit() per normal write operation. No inline comments unless requested. Make incremental edits directly against current GitHub files; do not paste full files unless asked.

### Latest known GitHub state (checked 2026-10-13)
- Latest observed commits: `15858128ea2cf0644399b367182cc6e68a11ad33` — partial tracking-number matching in candidate scoring; `476dc799b2d25627ee5e76c018a2f1bf3d8015a7` — partial tracking-number matching in AutoMatch; `3647c356dce0bb7249fb70ae51f101a1192135fc` — normalize tracking numbers in candidate scoring; `de6356bbaf17e2ba19a7cc09539ebc4b7315610c` — normalize tracking numbers in AutoMatch scoring.
- Relevant commits: 1ef4aaa221ac1b30fbe6f8caee9dd2671a714950 (exact AutoMatch date); 708dc76439c6550436ba606ca578ed194d1d9653 (additional AutoMatch condition); 314fd7c01a5928e8c854945763b2a800dcd3c722 (load matched operations); 17aab1334333c893e3ce45ed8f488f6222cee3d9 (show registered matches); 139f27115154f1db3f468e84d36f8668255e091a (AutoMatch result scores); acff7694258a9c2511115a69a7afb358d7a35dc3 (score-based AutoMatch); 9da0d93f3ecb2980a116929cdbbcaabd4e1ccacb (candidate scores); 850878f1fa2766a203fca2739892346f69a0273d (score column); f07429b391c6280f7e2b601f5f798637d07ea8fc (candidate score display); c9cf6fa04527f270f24ed7b442ad87fce2c9408f (date-picker getDate filtering).
- UX: exact existing account/from/to reconciliation is loaded; a free range creates and loads; any other overlap errors with “بازه زمانی وارد شده با یک مغایرت بانکی موجود تداخل دارد.”


### Date input label fix (2026-10-13)
- For the main reconciliation date fields `#FromDate` and `#ToDate`, keep the enclosing `.md-form label` class `active` synchronized with the input value on `input`, `change`, and `blur`. This handles date-picker selection where the label may otherwise overlap the selected date.
- Code change: `BankReconciliation.js`, commit `277819a6137a735452296a58ec491585ad351f10`.


### Re-selecting dates after loading a reconciliation (2026-10-13)
- Clicking `#btnChangeReconciliation` calls `prepareNewReconciliation()`; this must remove `.br-readonly` from `#bankReconciliationArea`, re-enable the account/date fields, and reinitialize the Persian date pickers on `#FromDate` and `#ToDate` so another date range can be selected after loading a history item or range.
- Preserve the current field values when entering change mode; the user can edit/replace them and click `نمایش`.
- Code change: `BankReconciliation.js`, commit `339ba4c8aa94325c1cc50fa67afc0710414fe203`.


### Bank account Select2 display fix (2026-10-13)
- In `BankReconciliation.js`, normalize `/BankAccount/GetFullSelect` result items for Select2: preserve existing `id/text` payloads and map common alternative key names (`Id/Value` and `Text/Title/BankAccountTitle/AccountTitle/AccountNumber`) so the selected account title is displayed.


### Bank account title when selecting a history item (2026-10-13)
- `selectReconciliation()` loads the selected item from `GET /BankReconciliation/Get` and displays `BankAccountTitle` in the Select2 option.
- `BankReconciliationService.Get(long id)` must eagerly load `BankAccount` before mapping the entity to `BankReconciliationDto`; otherwise the navigation property may be null and the history selection falls back to the generic `حساب بانکی` label.
- Code change: `BankReconciliationService.cs`, commit `122adaabe0905fe1d0e7e0fe9ed3d12a12a99956`; query now includes `.Include(x => x.BankAccount)`.
- The current `BankReconciliation.js` Select2 `processResults` mapping also supports common alternative response keys for the account title.


### Delete open reconciliations from history (2026-10-13)
- The history table shows a Delete button only when `State != 1` (not finalized); finalized rows retain only the Select action.
- Deletion requires a SweetAlert warning and explicit confirmation. The warning explains that the reconciliation and its match records are deleted, but bank transactions and accounting documents are not.
- The UI calls the existing `POST /BankReconciliation/Delete` endpoint. `BankReconciliationService.Delete` already rejects finalized reconciliations server-side and deletes only the reconciliation and its match records.
- After successful deletion, the history table reloads. If the deleted reconciliation was currently loaded, the UI returns to the editable/new-reconciliation state.
- Code change: `BankReconciliation.js`, commit `3b2d215c0c7f3398e602946f5cae7b882e440401`.

### Account reconciliation timeline (2026-10-13)
- The main page now contains a compact horizontally scrollable timeline for the selected bank account, positioned immediately below the account/date filters.
- `GET /BankReconciliation/GetTimeline?bankAccountId=...` returns the latest 12 history records using the existing `GetHistory` service method; the existing full-history modal remains available through “همه سوابق”.
- Timeline cards show the reconciliation date range and whether it is finalized or still being edited. The currently loaded reconciliation is highlighted; selecting a card loads that reconciliation through the existing `selectReconciliation` flow.
- The timeline refreshes when the bank account changes, after creating/loading a reconciliation, after finalization, and after deleting a reconciliation.
- Relevant files: `Index.cshtml`, `BankReconciliationController.cs`, `BankReconciliation.js`, and `BankReconciliation.css`.
- Implementation commits: `bf547ff14281c2416376eb23656aab0b79b24253` (markup), `23c91eeb131e6ed639c4392283246f3e0e6b5973` (endpoint), `0606d03e1ca63604ea26f741589fe677fe6cb7da` (interaction), `cc6904791ba222d156f517862dfedca76c068ef6` (styles).
- The timeline is limited to the latest 12 records to keep the main page compact; the complete server-side history table remains available for older records.

