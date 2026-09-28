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
        private readonly IRepository<BankReconciliation> _bankReconciliationRepository;
        private readonly IRepository<BankTransaction> _bankTransactionRepository;
        private readonly IRepository<BankAccount> _bankAccountRepository;
        private readonly IRepository<BankReconciliationMatch> _matchRepository;
        private readonly IAccountingDocDetailRepository _accountingDocDetailRepository;
        private readonly IAccountingDocRepository _accountingDocRepository;
        private readonly IRepository<DepositSlip> _depositSlipRepository;
        private readonly IRepository<Cheque> _chequeRepository;
        private readonly IRepository<Setting> _settingRepository;

        public BankReconciliationCandidateService(
            IRepository<BankReconciliation> bankReconciliationRepository,
            IRepository<BankTransaction> bankTransactionRepository,
            IRepository<BankAccount> bankAccountRepository,
            IRepository<BankReconciliationMatch> matchRepository,
            IAccountingDocDetailRepository accountingDocDetailRepository,
            IAccountingDocRepository accountingDocRepository,
            IRepository<DepositSlip> depositSlipRepository,
            IRepository<Cheque> chequeRepository,
            IRepository<Setting> settingRepository)
        {
            _bankReconciliationRepository = bankReconciliationRepository;
            _bankTransactionRepository = bankTransactionRepository;
            _bankAccountRepository = bankAccountRepository;
            _matchRepository = matchRepository;
            _accountingDocDetailRepository = accountingDocDetailRepository;
            _accountingDocRepository = accountingDocRepository;
            _depositSlipRepository = depositSlipRepository;
            _chequeRepository = chequeRepository;
            _settingRepository = settingRepository;
        }

        public CDSResponse<BankReconciliationCandidateResultDto> GetCandidates(
            long reconciliationId,
            long bankTransactionId,
            BankReconciliationCandidateFilterDto filter,
            Dictionary<string, string> columnFilters)
        {
            try
            {
                filter = filter ?? new BankReconciliationCandidateFilterDto();

                if (filter.Mode != BankReconciliationCandidateMode.Normal &&
                    filter.Mode != BankReconciliationCandidateMode.Group)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "نوع حالت دریافت کاندیدای مغایرت بانکی نامعتبر است.");
                }

                var reconciliation = _bankReconciliationRepository.Query
                    .FirstOrDefault(x => x.Id == reconciliationId);

                if (reconciliation == null)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "مغایرت بانکی مورد نظر یافت نشد.");
                }

                var bankAccount = _bankAccountRepository.Query
                    .Include(x => x.Center)
                    .FirstOrDefault(x => x.Id == reconciliation.BankAccountId);

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

                var fromDate = reconciliation.FromDate.Date;
                var toDate = reconciliation.ToDate.Date.AddDays(1);

                var transaction = _bankTransactionRepository.Query
                    .FirstOrDefault(x =>
                        x.Id == bankTransactionId &&
                        x.BankAccountId == reconciliation.BankAccountId &&
                        x.TransactionDate >= fromDate &&
                        x.TransactionDate < toDate);

                if (transaction == null)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "تراکنش بانکی در این مغایرت بانکی وجود ندارد.");
                }

                var bankAmount = GetAmount(transaction);
                var bankMatchedAmount = GetBankMatchedAmount(transaction.Id);
                var bankRemainingAmount = bankAmount - bankMatchedAmount;

                var result = new BankReconciliationCandidateResultDto
                {
                    BankTransactionId = transaction.Id,
                    Transaction = new BankReconciliationTransactionDto
                    {
                        Id = transaction.Id,
                        TransactionDate = transaction.TransactionDate,
                        TransactionTime = transaction.TransactionDate.ToString("HH:mm"),
                        Credit = transaction.Credit,
                        Debit = transaction.Debit,
                        Balance = transaction.Balance,
                        Description = transaction.Description,
                        TrackingNumber = transaction.TrackingNumber,
                        Amount = bankAmount,
                        MatchedAmount = bankMatchedAmount,
                        RemainingAmount = bankRemainingAmount,
                        Status = GetStatus(bankAmount, bankMatchedAmount)
                    },
                    Candidates = new List<BankReconciliationCandidateDto>(),
                    Page = filter.Page <= 0 ? 1 : filter.Page,
                    PageSize = filter.PageSize <= 0 ? 20 : filter.PageSize
                };

                if (bankRemainingAmount <= 0)
                    return Success(result);

                var allowedAccountIds = GetAllowedAccountIds();

                if (allowedAccountIds.Count == 0)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "تنظیم حساب‌های مجاز برای مغایرت‌گیری بانکی معتبر نیست.");
                }

                var centerId = bankAccount.CenterId.Value;

                var matchSums =
                    from match in _matchRepository.Query
                    group match by match.AccountingDocDetailId
                    into g
                    select new
                    {
                        AccountingDocDetailId = g.Key,
                        MatchedAmount = g.Sum(x => x.MatchedAmount)
                    };

                var dateTolerance = filter.DateTolerance < 0 ? 0 : filter.DateTolerance;

                if (dateTolerance < 0 || dateTolerance > 2)
                {
                    return Error<BankReconciliationCandidateResultDto>(
                        "اختلاف تاریخ انتخاب شده نامعتبر است.");
                }

                var transactionDate = transaction.TransactionDate.Date;
                var candidateFromDate = transactionDate.AddDays(-dateTolerance);
                var candidateToDate = transactionDate.AddDays(dateTolerance + 1);

                var accountingDocDates = _accountingDocRepository.Query
                    .Where(x =>
                        x.AccountingDocDate >= candidateFromDate &&
                        x.AccountingDocDate < candidateToDate)
                    .Select(x => new
                    {
                        x.Id,
                        x.AccountingDocDate
                    })
                    .ToList();

                var accountingDocIds = accountingDocDates
                    .Select(x => x.Id)
                    .ToList();

                var details = _accountingDocDetailRepository.Query
                    .Include(x => x.Account)
                    .Include(x => x.AccountingDocDetailCenters.Select(y => y.Center))
                    .Where(x =>
                        accountingDocIds.Contains(x.AccountingDocId) &&
                        allowedAccountIds.Contains(x.AccountId) &&
                        x.AccountingDocDetailCenters.Any(y =>
                            y.CenterId == centerId))
                    .ToList();

                var counterpartDetails = _accountingDocDetailRepository.Query
                    .Include(x => x.AccountingDocDetailCenters.Select(y => y.Center))
                    .Where(x =>
                        accountingDocIds.Contains(x.AccountingDocId))
                    .Select(x => new
                    {
                        x.Id,
                        x.AccountingDocId,
                        CenterTitles = x.AccountingDocDetailCenters
                            .Where(y => y.Center != null)
                            .Select(y => y.Center.Title)
                    })
                    .ToList();

                var depositSlipIds = details
                    .Where(x => x.DepositSlipId.HasValue)
                    .Select(x => x.DepositSlipId.Value)
                    .Distinct()
                    .ToList();

                var chequeIds = details
                    .Where(x => x.ChequeId.HasValue)
                    .Select(x => x.ChequeId.Value)
                    .Distinct()
                    .ToList();

                var depositSlips = _depositSlipRepository.Query
                    .Where(x => depositSlipIds.Contains(x.Id))
                    .ToList();

                var cheques = _chequeRepository.Query
                    .Where(x => chequeIds.Contains(x.Id))
                    .ToList();

                var detailsQuery =
                    from detail in details
                    join accountingDoc in accountingDocDates
                        on detail.AccountingDocId equals accountingDoc.Id
                    join depositSlip in depositSlips
                        on detail.DepositSlipId equals (long?)depositSlip.Id
                        into depositSlipJoin
                    from depositSlip in depositSlipJoin.DefaultIfEmpty()
                    join cheque in cheques
                        on detail.ChequeId equals (long?)cheque.Id
                        into chequeJoin
                    from cheque in chequeJoin.DefaultIfEmpty()
                    join matchSum in matchSums.ToList()
                        on detail.Id equals matchSum.AccountingDocDetailId
                        into detailMatches
                    from matchSum in detailMatches.DefaultIfEmpty()
                    select new
                    {
                        Detail = detail,
                        AccountingDocDate = accountingDoc.AccountingDocDate,
                        DepositSlip = depositSlip,
                        Cheque = cheque,
                        MatchedAmount = matchSum == null ? 0 : matchSum.MatchedAmount
                    };

                if (transaction.Credit > 0)
                {
                    detailsQuery = detailsQuery.Where(x => x.Detail.Debit > 0);
                }
                else
                {
                    detailsQuery = detailsQuery.Where(x => x.Detail.Credit > 0);
                }

                var candidates = detailsQuery
                    .ToList()
                    .Select(x =>
                    {
                        var detail = x.Detail;
                        var amount = GetAmount(detail);
                        var remaining = amount - x.MatchedAmount;

                        if (remaining <= 0)
                            return null;

                        var dateDifference = Math.Abs(
                            (x.AccountingDocDate.Date -
                             transaction.TransactionDate.Date).Days);

                        if (dateDifference > dateTolerance)
                            return null;

                        var trackingNumber = GetTrackingNumber(
                            x.DepositSlip,
                            x.Cheque);

                        var trackingMatched =
                            !string.IsNullOrWhiteSpace(transaction.TrackingNumber) &&
                            !string.IsNullOrWhiteSpace(trackingNumber) &&
                            transaction.TrackingNumber == trackingNumber;

                        var amountDifference = Math.Abs(
                            bankRemainingAmount - remaining);

                        var amountMatched = amountDifference == 0;
                        var dateMatched = dateDifference == 0;

                        return new BankReconciliationCandidateDto
                        {
                            AccountingDocDetailId = detail.Id,
                            AccountingDocId = detail.AccountingDocId,
                            AccountingDocDate = x.AccountingDocDate,
                            AccountId = detail.AccountId,
                            AccountTitle = detail.Account != null ? detail.Account.Title : null,
                            CenterId = detail.AccountingDocDetailCenters
                                .Select(y => (int?)y.CenterId)
                                .FirstOrDefault() ?? centerId,
                            CenterTitle = string.Join(
                                "، ",
                                detail.AccountingDocDetailCenters
                                    .Where(y => y.Center != null)
                                    .Select(y => y.Center.Title)
                                    .Where(y => !string.IsNullOrWhiteSpace(y))
                                    .Distinct()
                                    .ToList()),
                            CounterpartCenterTitle = string.Join(
                                "، ",
                                counterpartDetails
                                    .Where(y =>
                                        y.AccountingDocId == detail.AccountingDocId &&
                                        y.Id != detail.Id)
                                    .SelectMany(y => y.CenterTitles)
                                    .Where(y => !string.IsNullOrWhiteSpace(y))
                                    .Distinct()
                                    .ToList()),
                            Credit = detail.Credit,
                            Debit = detail.Debit,
                            Amount = amount,
                            MatchedAmount = x.MatchedAmount,
                            RemainingAmount = remaining,
                            AmountDifference = amountDifference,
                            DateDifference = dateDifference,
                            Description = detail.Description,
                            TrackingNumber = trackingNumber,
                            TrackingMatched = trackingMatched,
                            DateMatched = dateMatched,
                            AmountMatched = amountMatched,
                            CanMatch = amountMatched,
                            CanGroupMatch = remaining > 0,
                            TrackingSource = GetTrackingSource(
                                x.DepositSlip,
                                x.Cheque)
                        };
                    })
                    .Where(x => x != null)
                    .ToList();

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var search = filter.Search.Trim();

                    candidates = candidates
                        .Where(x =>
                            (x.Description != null && x.Description.Contains(search)) ||
                            (x.TrackingNumber != null && x.TrackingNumber.Contains(search)))
                        .ToList();
                }

                if (filter.TrackingMatched.HasValue)
                {
                    candidates = candidates
                        .Where(x => x.TrackingMatched == filter.TrackingMatched.Value)
                        .ToList();
                }

                if (columnFilters != null)
                {
                    foreach (var columnFilter in columnFilters)
                    {
                        if (string.IsNullOrWhiteSpace(columnFilter.Value))
                            continue;

                        var value = columnFilter.Value.Trim();

                        if (columnFilter.Key == "AccountingDocDate")
                        {
                            DateTime date;

                            if (TryParseFilterDate(value, out date))
                            {
                                candidates = candidates
                                    .Where(x => x.AccountingDocDate.Date == date.Date)
                                    .ToList();
                            }
                            else
                            {
                                candidates.Clear();
                            }
                        }
                        else if (columnFilter.Key == "AccountingDocId")
                        {
                            long id;

                            if (long.TryParse(value, out id))
                            {
                                candidates = candidates
                                    .Where(x => x.AccountingDocId == id)
                                    .ToList();
                            }
                            else
                            {
                                candidates.Clear();
                            }
                        }
                        else if (columnFilter.Key == "AccountTitle")
                        {
                            candidates = candidates
                                .Where(x =>
                                    x.AccountTitle != null &&
                                    x.AccountTitle.Contains(value))
                                .ToList();
                        }
                        else if (columnFilter.Key == "CenterTitle")
                        {
                            candidates = candidates
                                .Where(x =>
                                    (x.CenterTitle != null &&
                                     x.CenterTitle.Contains(value)) ||
                                    (x.CounterpartCenterTitle != null &&
                                     x.CounterpartCenterTitle.Contains(value)))
                                .ToList();
                        }
                        else if (columnFilter.Key == "Description")
                        {
                            candidates = candidates
                                .Where(x =>
                                    x.Description != null &&
                                    x.Description.Contains(value))
                                .ToList();
                        }
                        else if (columnFilter.Key == "TrackingSource")
                        {
                            candidates = candidates
                                .Where(x =>
                                    x.TrackingSource != null &&
                                    x.TrackingSource.Contains(value))
                                .ToList();
                        }
                        else if (columnFilter.Key == "TrackingNumber")
                        {
                            candidates = candidates
                                .Where(x =>
                                    x.TrackingNumber != null &&
                                    x.TrackingNumber.Contains(value))
                                .ToList();
                        }
                        else if (columnFilter.Key == "Amount" ||
                                 columnFilter.Key == "AmountDifference")
                        {
                            decimal amount;

                            if (decimal.TryParse(
                                    value,
                                    NumberStyles.Number,
                                    CultureInfo.InvariantCulture,
                                    out amount) ||
                                decimal.TryParse(
                                    value,
                                    NumberStyles.Number,
                                    CultureInfo.CurrentCulture,
                                    out amount))
                            {
                                if (columnFilter.Key == "Amount")
                                {
                                    candidates = candidates
                                        .Where(x => x.Amount == amount)
                                        .ToList();
                                }
                                else
                                {
                                    candidates = candidates
                                        .Where(x => x.AmountDifference == amount)
                                        .ToList();
                                }
                            }
                            else
                            {
                                candidates.Clear();
                            }
                        }
                    }
                }

                if (filter.Mode == BankReconciliationCandidateMode.Normal)
                {
                    candidates = candidates
                        .Where(x => x.CanMatch)
                        .ToList();
                }
                else
                {
                    candidates = candidates
                        .Where(x => x.CanGroupMatch)
                        .ToList();
                }

                candidates = candidates
                    .Select(x =>
                    {
                        var trackingInDescription =
                            ContainsText(transaction.Description, x.TrackingNumber);

                        var counterpartCenterInDescription =
                            ContainsAnyText(
                                transaction.Description,
                                x.CounterpartCenterTitle);

                        var score = 0;
                        var scoreDetails = new List<string>();

                        if (x.AmountMatched)
                        {
                            score += 100;
                            scoreDetails.Add("مبلغ: +100");
                        }

                        if (x.TrackingMatched)
                        {
                            score += 50;
                            scoreDetails.Add("شماره پیگیری: +50");
                        }

                        if (trackingInDescription)
                        {
                            score += 50;
                            scoreDetails.Add("پیگیری در شرح: +50");
                        }

                        if (counterpartCenterInDescription)
                        {
                            score += 30;
                            scoreDetails.Add("مرکز طرف دوم در شرح: +30");
                        }

                        if (x.DateDifference == 0)
                        {
                            score += 20;
                            scoreDetails.Add("تاریخ: +20");
                        }
                        else if (x.DateDifference == 1)
                        {
                            score += 10;
                            scoreDetails.Add("اختلاف تاریخ یک روز: +10");
                        }

                        x.MatchScore = score;
                        x.MatchScoreDetails = scoreDetails.Count == 0
                            ? "بدون امتیاز"
                            : string.Join("، ", scoreDetails);

                        return x;
                    })
                    .OrderByDescending(x => x.MatchScore)
                    .ThenByDescending(x => x.AmountMatched)
                    .ThenByDescending(x => x.TrackingMatched)
                    .ThenBy(x => x.DateDifference)
                    .ThenBy(x => x.AmountDifference)
                    .ToList();

                result.TotalCount = candidates.Count;

                result.Candidates = candidates
                    .Skip((result.Page - 1) * result.PageSize)
                    .Take(result.PageSize)
                    .ToList();

                return Success(result);
            }
            catch (Exception e)
            {
                return Error<BankReconciliationCandidateResultDto>(e.Message);
            }
        }

        private bool TryParseFilterDate(
            string value,
            out DateTime date)
        {
            var formats = new[]
            {
                "yyyy/MM/dd",
                "yyyy-MM-dd",
                "yyyy/M/d",
                "yyyy-M-d"
            };

            if (DateTime.TryParseExact(
                    value,
                    formats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out date))
            {
                if (date.Year >= 1200 && date.Year <= 1700)
                {
                    var calendar = new PersianCalendar();

                    try
                    {
                        date = calendar.ToDateTime(
                            date.Year,
                            date.Month,
                            date.Day,
                            0,
                            0,
                            0,
                            0);
                    }
                    catch
                    {
                        return false;
                    }
                }

                return true;
            }

            date = default(DateTime);
            return false;
        }

        private bool ContainsAnyText(string source, string values)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(values))
                return false;

            return values
                .Split(new[] { '،', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(x => ContainsText(source, x.Trim()));
        }

        private bool ContainsText(string source, string value)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(value))
                return false;

            var normalizedSource = NormalizeText(source);
            var normalizedValue = NormalizeText(value);

            return normalizedSource.Contains(normalizedValue);
        }

        private string NormalizeText(string value)
        {
            return string.Join(
                " ",
                value
                    .Replace("ي", "ی")
                    .Replace("ى", "ی")
                    .Replace("ك", "ک")
                    .Split((char[])null, StringSplitOptions.RemoveEmptyEntries));
        }

        private decimal GetAmount(BankTransaction transaction)
        {
            return transaction.Credit > 0 ? transaction.Credit : transaction.Debit;
        }

        private decimal GetAmount(AccountingDocDetail detail)
        {
            return detail.Credit > 0 ? detail.Credit : detail.Debit;
        }

        private decimal GetBankMatchedAmount(long bankTransactionId)
        {
            return _matchRepository.Query
                .Where(x => x.BankTransactionId == bankTransactionId)
                .Select(x => (decimal?)x.MatchedAmount)
                .Sum() ?? 0;
        }

        private decimal GetAccountingMatchedAmount(long accountingDocDetailId)
        {
            return _matchRepository.Query
                .Where(x => x.AccountingDocDetailId == accountingDocDetailId)
                .Select(x => (decimal?)x.MatchedAmount)
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
            DepositSlip depositSlip,
            Cheque cheque)
        {
            if (depositSlip != null &&
                !string.IsNullOrWhiteSpace(depositSlip.Number))
            {
                return depositSlip.Number;
            }

            if (cheque != null &&
                !string.IsNullOrWhiteSpace(cheque.Serial))
            {
                return cheque.Serial;
            }

            return null;
        }

        private string GetTrackingSource(
            DepositSlip depositSlip,
            Cheque cheque)
        {
            var hasDepositSlip = depositSlip != null;
            var hasCheque = cheque != null;

            if (hasDepositSlip && hasCheque)
                return "DepositSlip, Cheque";

            if (hasDepositSlip)
                return "DepositSlip";

            if (hasCheque)
                return "Cheque";

            return null;
        }

        private List<int> GetAllowedAccountIds()
        {
            var setting = _settingRepository.Query
                .FirstOrDefault(x =>
                    x.Key == "BankReconciliationAllowedAccountIds");

            if (setting == null ||
                string.IsNullOrWhiteSpace(setting.Value))
            {
                return new List<int>();
            }

            try
            {
                return Newtonsoft.Json.JsonConvert
                    .DeserializeObject<List<int>>(setting.Value)
                    ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        private CDSResponse<T> Success<T>(T data)
        {
            return new CDSResponse<T>(
                ResponseStatus.Success,
                Resources.General.SuccessMessage,
                data);
        }

        private CDSResponse<T> Error<T>(string message)
        {
            return new CDSResponse<T>(
                ResponseStatus.Error,
                message,
                default(T));
        }
    }
}