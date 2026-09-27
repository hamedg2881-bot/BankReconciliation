using CDS.BIMS.Domain.Model.Enum.Financial;
using System;
using System.Collections.Generic;

namespace CDS.BIMS.Application.Dto.Financial
{
    public class BankReconciliationDto
    {
        public long Id { get; set; }

        public int BankAccountId { get; set; }
        public string BankAccountTitle { get; set; }

        public string AccountNumber { get; set; }
        public string IBAN { get; set; }

        public int? CenterId { get; set; }
        public string CenterTitle { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public BankReconciliationState State { get; set; }

        public DateTime? FinalizedDate { get; set; }
        public int? FinalizedByUserId { get; set; }
    }
    public class BankReconciliationHistoryDto
    {
        public long Id { get; set; }
        public int BankAccountId { get; set; }
        public string BankAccountTitle { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public BankReconciliationState State { get; set; }
        public DateTime? FinalizedDate { get; set; }
    }

    public class BankReconciliationTransactionDto
    {
        public long Id { get; set; }

        public DateTime TransactionDate { get; set; }
        public string TransactionTime { get; set; }

        public decimal Credit { get; set; }
        public decimal Debit { get; set; }
        public decimal Balance { get; set; }

        public string Description { get; set; }
        public string TrackingNumber { get; set; }

        public decimal Amount { get; set; }

        public decimal MatchedAmount { get; set; }
        public decimal RemainingAmount { get; set; }

        public BankReconciliationStatus Status { get; set; }
    }
    public class BankReconciliationSummaryDto
    {
        public int TotalTransactions { get; set; }

        public int MatchedTransactions { get; set; }

        public int PartialTransactions { get; set; }

        public int UnmatchedTransactions { get; set; }

        public decimal TotalAmount { get; set; }

        public decimal MatchedAmount { get; set; }

        public decimal RemainingAmount { get; set; }

        public decimal MatchPercentage { get; set; }
    }
    public class BankReconciliationPageDto
    {
        public BankReconciliationDto Reconciliation { get; set; }

        public BankReconciliationSummaryDto Summary { get; set; }

        public List<BankReconciliationTransactionDto> Transactions { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class BankReconciliationFilterDto
    {
        public string Search { get; set; }

        public BankReconciliationStatus? Status { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class BankReconciliationCandidateDto
    {
        public long AccountingDocDetailId { get; set; }

        public long AccountingDocId { get; set; }

        public DateTime AccountingDocDate { get; set; }

        public int AccountId { get; set; }
        public string AccountTitle { get; set; }

        public int CenterId { get; set; }
        public string CenterTitle { get; set; }
        public string CounterpartCenterTitle { get; set; }

        public decimal Credit { get; set; }
        public decimal Debit { get; set; }

        public decimal Amount { get; set; }

        public decimal MatchedAmount { get; set; }
        public decimal RemainingAmount { get; set; }

        public decimal AmountDifference { get; set; }

        public int DateDifference { get; set; }

        public string Description { get; set; }
        public string TrackingNumber { get; set; }

        public bool TrackingMatched { get; set; }

        public bool DateMatched { get; set; }

        public bool AmountMatched { get; set; }

        public bool CanMatch { get; set; }

        public bool CanGroupMatch { get; set; }

        public string TrackingSource { get; set; }
    }

    public class BankReconciliationCandidateResultDto
    {
        public long BankTransactionId { get; set; }

        public BankReconciliationTransactionDto Transaction { get; set; }

        public List<BankReconciliationCandidateDto> Candidates { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class BankReconciliationCandidateFilterDto
    {
        public string Search { get; set; }

        public bool? TrackingMatched { get; set; }

        public int DateTolerance { get; set; }

        public BankReconciliationCandidateMode Mode { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class BankReconciliationCreateRequest
    {
        public int BankAccountId { get; set; }

        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }
    }

    public class BankReconciliationMatchRequest
    {
        public long ReconciliationId { get; set; }

        public long BankTransactionId { get; set; }

        public long AccountingDocDetailId { get; set; }
    }

    public class BankReconciliationGroupBankToDetailsRequest
    {
        public long ReconciliationId { get; set; }

        public long BankTransactionId { get; set; }

        public List<long> AccountingDocDetailIds { get; set; }
    }

    public class BankReconciliationGroupBanksToDetailRequest
    {
        public long ReconciliationId { get; set; }

        public List<long> BankTransactionIds { get; set; }

        public long AccountingDocDetailId { get; set; }
    }

    public class BankReconciliationMatchDto
    {
        public long Id { get; set; }

        public long BankTransactionId { get; set; }

        public long AccountingDocDetailId { get; set; }

        public long AccountingDocId { get; set; }

        public DateTime AccountingDocDate { get; set; }

        public decimal MatchedAmount { get; set; }

        public BankReconciliationMatchType MatchType { get; set; }

        public DateTime MatchDate { get; set; }

        public int AccountId { get; set; }
        public string AccountTitle { get; set; }

        public int CenterId { get; set; }
        public string CenterTitle { get; set; }

        public string Description { get; set; }

        public string TrackingNumber { get; set; }
    }

    public class BankReconciliationAutoMatchResultDto
    {
        public long BankTransactionId { get; set; }

        public bool Matched { get; set; }

        public long? AccountingDocDetailId { get; set; }

        public decimal MatchedAmount { get; set; }

        public BankReconciliationStatus Status { get; set; }

        public int CandidateCount { get; set; }

        public string Reason { get; set; }
    }


}
