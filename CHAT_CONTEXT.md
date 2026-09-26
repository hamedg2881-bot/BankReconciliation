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
- BankReconciliationService.cs: c0dcca570f83bab0ccac328701d192c276e26d83
- Index.cshtml: a060c593802f1bb07483a23c9d2584d46dde3e1e
- BankReconciliationCandidateService.cs: d808f38f73b018c992aa2a69dc82be4708169e1c

## Current UX Flow
Display/Load: exact existing reconciliation -> load it; no existing reconciliation and free range -> create then load it; overlap with another reconciliation -> show error.
Bank table and candidate table are server-side DataTables.
The user prefers minimal, incremental changes and wants the actual GitHub files to remain the source of truth.