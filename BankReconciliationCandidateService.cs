using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using CDS.BIMS.Application.Dto.CDSBase;
using CDS.BIMS.Application.Dto.Financial;
using CDS.BIMS.Application.ServiceContract.Financial;
using CDS.BIMS.Data.RepositoryContract.Financial;
using CDS.BIMS.Domain.Model.BaseInfo;
using CDS.BIMS.Domain.Model.Enum.Financial;
using CDS.BIMS.Domain.Model.Financial;
using CDS.Core.Domain;
using CDS.Core.Domain.Enums;

namespace CDS.BIMS.Application.Service.Financial
{
    public class BankReconciliationCandidateService :
        IBankReconciliationCandidateService
    {
        private readonly IRepository<BankReconciliation>
            _bankReconciliationRepository;

        private readonly IRepository<BankTransaction>
            _bankTransactionRepository;

        private readonly IRepository<BankAccount>
            _bankAccountRepository;

        private readonly IRepository<BankReconciliationMatch>
            _matchRepository;

        private readonly IAccountingDocDetailRepository
            _accountingDocDetailRepository;

        private readonly IAccountingDocRepository
            _accountingDocRepository;

        private readonly IRepository<AccountingDoc>
            _accountingDocRepository;

        private readonly IRepository<Setting>
            _settingRepository;

        public BankReconciliationCandidateService(
            IRepository<BankReconciliation> bankReconciliationRepository,
            IRepository<BankTransaction> bankTransactionRepository,
            IRepository<BankAccount> bankAccountRepository,
            IRepository<BankReconciliationMatch> matchRepository,
            IAccountingDocDetailRepository accountingDocDetailRepository,
            IAccountingDocRepository accountingDocRepository,
            IRepository<Setting> settingRepository)
        {
            _bankReconciliationRepository = bankReconciliationRepository;

            _bankTransactionRepository = bankTransactionRepository;

            _bankAccountRepository = bankAccountRepository;

            _matchRepository = matchRepository;

            _accountingDocDetailRepository = accountingDocDetailRepository;

            _accountingDocRepository = accountingDocRepository;

            _settingRepository = settingRepository;
        }

        public CDSResponse<BankReconciliationCandidateResultDto>
            GetCandidates(
                long reconciliationId,
                long bankTransactionId,
                BankReconciliationCandidateFilterDto filter)
        {
            try
            {
                filter = filter ??
                         new BankReconciliationCandidateFilterDto();

                if (filter.Mode !=
                        BankReconciliationCandidateMode.Normal &&
                    filter.Mode !=
                        BankReconciliationCandidateMode.Group)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "نوع حالت دریافت کاندیدای مغایرت بانکی نامعتبر است.");
                }

                var reconciliation =
                    _bankReconciliationRepository.Query
                        .FirstOrDefault(x =>
                            x.Id == reconciliationId);

                if (reconciliation == null)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "مغایرت بانکی مورد نظر یافت نشد.");
                }

                var bankAccount =
                    _bankAccountRepository.Query
                        .Include(x => x.Center)
                        .FirstOrDefault(x =>
                            x.Id == reconciliation.BankAccountId);

                if (bankAccount == null)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "حساب بانکی مورد نظر یافت نشد.");
                }

                if (!bankAccount.CenterId.HasValue)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "برای این حساب بانکی مرکز تعریف نشده است و امکان مغایرت‌گیری وجود ندارد.");
                }

                var fromDate =
                    reconciliation.FromDate.Date;

                var toDate =
                    reconciliation.ToDate.Date.AddDays(1);

                var transaction =
                    _bankTransactionRepository.Query
                        .FirstOrDefault(x =>
                            x.Id == bankTransactionId &&
                            x.BankAccountId ==
                            reconciliation.BankAccountId &&
                            x.TransactionDate >= fromDate &&
                            x.TransactionDate < toDate);

                if (transaction == null)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "تراکنش بانکی در این مغایرت بانکی وجود ندارد.");
                }

                var bankAmount =
                    GetAmount(transaction);

                var bankMatchedAmount =
                    GetBankMatchedAmount(
                        transaction.Id);

                var bankRemainingAmount =
                    bankAmount - bankMatchedAmount;

                var result =
                    new BankReconciliationCandidateResultDto
                    {
                        BankTransactionId =
                            transaction.Id,

                        Transaction =
                            new BankReconciliationTransactionDto
                            {
                                Id = transaction.Id,
                                TransactionDate =
                                    transaction.TransactionDate,
                                TransactionTime =
                                    transaction.TransactionDate.ToString("HH:mm"),
                                Credit =
                                    transaction.Credit,
                                Debit =
                                    transaction.Debit,
                                Balance =
                                    transaction.Balance,
                                Description =
                                    transaction.Description,
                                TrackingNumber =
                                    transaction.TrackingNumber,
                                Amount =
                                    bankAmount,
                                MatchedAmount =
                                    bankMatchedAmount,
                                RemainingAmount =
                                    bankRemainingAmount,
                                Status =
                                    GetStatus(
                                        bankAmount,
                                        bankMatchedAmount)
                            },

                        Candidates =
                            new List<BankReconciliationCandidateDto>(),

                        Page =
                            filter.Page <= 0
                                ? 1
                                : filter.Page,

                        PageSize =
                            filter.PageSize <= 0
                                ? 20
                                : filter.PageSize
                    };

                if (bankRemainingAmount <= 0)
                    return Success(result);

                var allowedAccountIds =
                    GetAllowedAccountIds();

                if (allowedAccountIds.Count == 0)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "تنظیم حساب‌های مجاز برای مغایرت‌گیری بانکی معتبر نیست.");
                }

                var centerId =
                    bankAccount.CenterId.Value;

                var matchSums =
                    from match in _matchRepository.Query
                    group match by match.AccountingDocDetailId
                    into g
                    select new
                    {
                        AccountingDocDetailId =
                            g.Key,

                        MatchedAmount =
                            g.Sum(x => x.MatchedAmount)
                    };

                var transactionDate =
                    transaction.TransactionDate.Date;

                var candidateFromDate =
                    transactionDate.AddDays(-2);

                var candidateToDate =
                    transactionDate.AddDays(3);

                var detailsQuery =
                    from detail in
                        _accountingDocDetailRepository.Query
                            .Include(x => x.Account)
                            .Include(x => x.DepositSlip)
                            .Include(x => x.Cheque)

                    join accountingDoc in
                        _accountingDocRepository.Query
                        on detail.AccountingDocId equals
                        accountingDoc.Id

                    where
                        allowedAccountIds.Contains(
                            detail.AccountId)

                        &&
                        accountingDoc.AccountingDocDate
                        >= candidateFromDate

                        &&
                        accountingDoc.AccountingDocDate
                        < candidateToDate

                        &&
                        detail.AccountingDocDetailCenters
                            .Any(x =>
                                x.CenterId == centerId)

                    join matchSum in matchSums
                        on detail.Id equals
                        matchSum.AccountingDocDetailId
                        into detailMatches

                    from matchSum in
                        detailMatches.DefaultIfEmpty()

                    select new
                    {
                        Detail = detail,

                        AccountingDocDate =
                            accountingDoc.AccountingDocDate,

                        MatchedAmount =
                            matchSum == null
                                ? 0
                                : matchSum.MatchedAmount
                    };

                if (transaction.Credit > 0)
                {
                    detailsQuery =
                        detailsQuery.Where(x =>
                            x.Detail.Debit > 0);
                }
                else
                {
                    detailsQuery =
                        detailsQuery.Where(x =>
                            x.Detail.Credit > 0);
                }

                var candidates =
                    detailsQuery
                        .ToList()
                        .Select(x =>
                        {
                            var detail =
                                x.Detail;

                            var amount =
                                GetAmount(detail);

                            var remaining =
                                amount -
                                x.MatchedAmount;

                            if (remaining <= 0)
                                return null;

                            var dateDifference =
                                Math.Abs(
                                    (
                                        x.AccountingDocDate.Date
                                        -
                                        transaction
                                            .TransactionDate.Date
                                    ).Days);

                            if (dateDifference > 2)
                                return null;

                            var trackingNumber =
                                GetTrackingNumber(
                                    detail);

                            var trackingMatched =
                                !string.IsNullOrWhiteSpace(
                                    transaction.TrackingNumber)
                                &&
                                !string.IsNullOrWhiteSpace(
                                    trackingNumber)
                                &&
                                transaction.TrackingNumber ==
                                trackingNumber;

                            var amountDifference =
                                Math.Abs(
                                    bankRemainingAmount -
                                    remaining);

                            var amountMatched =
                                amountDifference == 0;

                            var dateMatched =
                                dateDifference == 0;

                            return new BankReconciliationCandidateDto
                            {
                                AccountingDocDetailId =
                                    detail.Id,

                                AccountingDocId =
                                    detail.AccountingDocId,

                                AccountingDocDate =
                                    x.AccountingDocDate,

                                AccountId =
                                    detail.AccountId,

                                AccountTitle =
                                    detail.Account != null
                                        ? detail.Account.Title
                                        : null,

                                CenterId =
                                    centerId,

                                CenterTitle =
                                    bankAccount.Center != null
                                        ? bankAccount.Center.Title
                                        : null,

                                Credit =
                                    detail.Credit,

                                Debit =
                                    detail.Debit,

                                Amount =
                                    amount,

                                MatchedAmount =
                                    x.MatchedAmount,

                                RemainingAmount =
                                    remaining,

                                AmountDifference =
                                    amountDifference,

                                DateDifference =
                                    dateDifference,

                                Description =
                                    detail.Description,

                                TrackingNumber =
                                    trackingNumber,

                                TrackingMatched =
                                    trackingMatched,

                                DateMatched =
                                    dateMatched,

                                AmountMatched =
                                    amountMatched,

                                CanMatch =
                                    amountMatched,

                                CanGroupMatch =
                                    remaining > 0,

                                TrackingSource =
                                    GetTrackingSource(
                                        detail)
                            };
                        })
                        .Where(x => x != null)
                        .ToList();

                if (!string.IsNullOrWhiteSpace(
                        filter.Search))
                {
                    var search =
                        filter.Search.Trim();

                    candidates =
                        candidates
                            .Where(x =>
                                (x.Description != null &&
                                 x.Description.Contains(search))
                                ||
                                (x.TrackingNumber != null &&
                                 x.TrackingNumber.Contains(search)))
                            .ToList();
                }

                if (filter.TrackingMatched.HasValue)
                {
                    candidates =
                        candidates
                            .Where(x =>
                                x.TrackingMatched ==
                                filter.TrackingMatched.Value)
                            .ToList();
                }

                if (filter.Mode ==
                    BankReconciliationCandidateMode.Normal)
                {
                    candidates =
                        candidates
                            .Where(x =>
                                x.CanMatch)
                            .ToList();
                }
                else
                {
                    candidates =
                        candidates
                            .Where(x =>
                                x.CanGroupMatch)
                            .ToList();
                }

                candidates =
                    candidates
                        .OrderByDescending(x =>
                            x.AmountMatched &&
                            x.DateMatched &&
                            x.TrackingMatched)

                        .ThenByDescending(x =>
                            x.AmountMatched &&
                            x.DateMatched)

                        .ThenByDescending(x =>
                            x.AmountMatched &&
                            x.TrackingMatched)

                        .ThenByDescending(x =>
                            x.AmountMatched)

                        .ThenByDescending(x =>
                            x.DateMatched &&
                            x.TrackingMatched)

                        .ThenByDescending(x =>
                            x.DateMatched)

                        .ThenByDescending(x =>
                            x.TrackingMatched)

                        .ThenBy(x =>
                            x.DateDifference)

                        .ThenBy(x =>
                            x.AmountDifference)

                        .ToList();

                result.TotalCount =
                    candidates.Count;

                result.Candidates =
                    candidates
                        .Skip(
                            (result.Page - 1) *
                            result.PageSize)
                        .Take(result.PageSize)
                        .ToList();

                return Success(result);
            }
            catch (Exception e)
            {
                return Error<BankReconciliationCandidateResultDto>(
                    e.Message);
            }
        }

        private decimal GetAmount(
            BankTransaction transaction)
        {
            return transaction.Credit > 0
                ? transaction.Credit
                : transaction.Debit;
        }

        private decimal GetAmount(
            AccountingDocDetail detail)
        {
            return detail.Credit > 0
                ? detail.Credit
                : detail.Debit;
        }

        private decimal GetBankMatchedAmount(
            long bankTransactionId)
        {
            return _matchRepository.Query
                .Where(x =>
                    x.BankTransactionId ==
                    bankTransactionId)
                .Select(x =>
                    (decimal?)x.MatchedAmount)
                .Sum() ?? 0;
        }

        private decimal GetAccountingMatchedAmount(
            long accountingDocDetailId)
        {
            return _matchRepository.Query
                .Where(x =>
                    x.AccountingDocDetailId ==
                    accountingDocDetailId)
                .Select(x =>
                    (decimal?)x.MatchedAmount)
                .Sum() ?? 0;
        }

        private BankReconciliationStatus GetStatus(
            decimal amount,
            decimal matchedAmount)
        {
            if (matchedAmount == 0)
                return BankReconciliationStatus.Unmatched;

            if (matchedAmount < amount)
                return BankReconciliationStatus.Partial;

            return BankReconciliationStatus.Matched;
        }

        private string GetTrackingNumber(
            AccountingDocDetail detail)
        {
            if (detail.DepositSlip != null &&
                !string.IsNullOrWhiteSpace(
                    detail.DepositSlip.Number))
            {
                return detail.DepositSlip.Number;
            }

            if (detail.Cheque != null &&
                !string.IsNullOrWhiteSpace(
                    detail.Cheque.Serial))
            {
                return detail.Cheque.Serial;
            }

            return null;
        }

        private string GetTrackingSource(
            AccountingDocDetail detail)
        {
            var depositSlip =
                detail.DepositSlip != null &&
                !string.IsNullOrWhiteSpace(
                    detail.DepositSlip.Number);

            var cheque =
                detail.Cheque != null &&
                !string.IsNullOrWhiteSpace(
                    detail.Cheque.Serial);

            if (depositSlip && cheque)
                return "DepositSlip, Cheque";

            if (depositSlip)
                return "DepositSlip";

            if (cheque)
                return "Cheque";

            return null;
        }

        private List<int> GetAllowedAccountIds()
        {
            var setting =
                _settingRepository.Query
                    .FirstOrDefault(x =>
                        x.Key ==
                        "BankReconciliationAllowedAccountIds");

            if (setting == null ||
                string.IsNullOrWhiteSpace(
                    setting.Value))
            {
                return new List<int>();
            }

            try
            {
                return Newtonsoft.Json.JsonConvert
                           .DeserializeObject<List<int>>(
                               setting.Value)
                       ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private CDSResponse<T> Success<T>(
            T data)
        {
            return new CDSResponse<T>(
                ResponseStatus.Success,
                Resources.General.SuccessMessage,
                data);
        }

        private CDSResponse<T> Error<T>(
            string message)
        {
            return new CDSResponse<T>(
                ResponseStatus.Error,
                message,
                default(T));
        }
    }
}