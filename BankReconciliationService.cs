using CDS.BIMS.Application.Dto.CDSBase;
using CDS.BIMS.Application.Dto.Financial;
using CDS.BIMS.Application.ServiceContract.Financial;
using CDS.BIMS.Domain.Model.BaseInfo;
using CDS.BIMS.Domain.Model.Enum.Financial;
using CDS.BIMS.Domain.Model.Financial;
using CDS.Core.Domain;
using CDS.Core.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using CDS.BIMS.Data.RepositoryContract.Financial;

namespace CDS.BIMS.Application.Service.Financial
{

    public class BankReconciliationService : IBankReconciliationService
    {
        private readonly IUnitOfWorkScope _unitOfWorkScope;
        private readonly IRepository<BankReconciliation> _bankReconciliationRepository;
        private readonly IRepository<BankReconciliationMatch> _matchRepository;
        private readonly IRepository<BankTransaction> _bankTransactionRepository;
        private readonly IAccountingDocDetailRepository _accountingDocDetailRepository;
        private readonly IRepository<BankAccount> _bankAccountRepository;
        private readonly IRepository<Setting> _settingRepository;

        public BankReconciliationService(
            IUnitOfWorkScope unitOfWorkScope,
            IRepository<BankReconciliation> bankReconciliationRepository,
            IRepository<BankReconciliationMatch> matchRepository,
            IRepository<BankTransaction> bankTransactionRepository,
            IAccountingDocDetailRepository accountingDocDetailRepository,
            IRepository<BankAccount> bankAccountRepository,
            IRepository<Setting> settingRepository)
        {
            _unitOfWorkScope = unitOfWorkScope;
            _bankReconciliationRepository = bankReconciliationRepository;
            _matchRepository = matchRepository;
            _bankTransactionRepository = bankTransactionRepository;
            _accountingDocDetailRepository = accountingDocDetailRepository;
            _bankAccountRepository = bankAccountRepository;
            _settingRepository = settingRepository;
        }

        public CDSResponse<BankReconciliationDto> Create(
            BankReconciliationCreateRequest request)
        {
            try
            {
                if (request == null)
                    return Error<BankReconciliationDto>("اطلاعات ثبت مغایرت بانکی نامعتبر است.");

                var fromDate = request.FromDate.Date;
                var toDate = request.ToDate.Date;

                if (fromDate > toDate)
                    return Error<BankReconciliationDto>(
                        "تاریخ شروع نمی‌تواند بعد از تاریخ پایان باشد.");

                var bankAccount = _bankAccountRepository.First(
                    x => x.Id == request.BankAccountId);

                if (bankAccount == null)
                    return Error<BankReconciliationDto>(
                        "حساب بانکی مورد نظر یافت نشد.");

                if (!bankAccount.CenterId.HasValue)
                    return Error<BankReconciliationDto>(
                        "برای این حساب بانکی مرکز تعریف نشده است و امکان مغایرت‌گیری وجود ندارد.");

                var existingReconciliation =
                    _bankReconciliationRepository.Query
                        .FirstOrDefault(x =>
                            x.BankAccountId == request.BankAccountId &&
                            x.FromDate == fromDate &&
                            x.ToDate == toDate);

                if (existingReconciliation != null)
                    return Get(existingReconciliation.Id);

                var overlapExists = _bankReconciliationRepository.Query.Any(x =>
                    x.BankAccountId == request.BankAccountId &&
                    x.FromDate <= toDate &&
                    x.ToDate >= fromDate);

                if (overlapExists)
                    return Error<BankReconciliationDto>(
                        "بازه زمانی وارد شده با یک مغایرت بانکی موجود تداخل دارد.");

                var userId = Convert.ToInt32(
                    HttpContext.Current.Session["UserId"]);

                var reconciliation = new BankReconciliation
                {
                    BankAccountId = request.BankAccountId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    State = BankReconciliationState.Open,
                    CreatorUserId = userId,
                    CreateDate = DateTime.Now,
                    EntityState = EntityStates.Added
                };

                _bankReconciliationRepository.Add(reconciliation);

                _unitOfWorkScope.Commit();

                return Get(reconciliation.Id);
            }
            catch (Exception e)
            {
                return Error<BankReconciliationDto>(e.Message);
            }
        }

        public CDSResponse<BankReconciliationDto> Get(long id)
        {
            try
            {
                var reconciliation =
                    _bankReconciliationRepository.Query
                        .FirstOrDefault(x => x.Id == id);

                if (reconciliation == null)
                    return Error<BankReconciliationDto>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                return Success(ToDto(reconciliation));
            }
            catch (Exception e)
            {
                return Error<BankReconciliationDto>(e.Message);
            }
        }

        public CDSResponse<BankReconciliationHistoryPageDto> GetHistory(
            int? bankAccountId,
            int requestStart,
            int requestLength,
            Dictionary<string, string> columnFilters)
        {
            try
            {
                var query =
                    _bankReconciliationRepository.Query
                        .Include(x => x.BankAccount.Center)
                        .AsQueryable();

                if (bankAccountId.HasValue)
                {
                    query = query.Where(x => x.BankAccountId == bankAccountId.Value);
                }

                var totalCount = query.Count();

                if (columnFilters != null)
                {
                    foreach (var columnFilter in columnFilters)
                    {
                        var value = columnFilter.Value == null
                            ? null
                            : columnFilter.Value.Trim();

                        if (string.IsNullOrWhiteSpace(value))
                            continue;

                        if (columnFilter.Key == "BankAccountTitle")
                        {
                            query = query.Where(x => x.BankAccount.Title.Contains(value));
                        }
                        else if (columnFilter.Key == "FromDate")
                        {
                            DateTime date;

                            if (TryParseFilterDate(value, out date))
                            {
                                var nextDate = date.Date.AddDays(1);
                                query = query.Where(x => x.FromDate >= date.Date && x.FromDate < nextDate);
                            }
                            else
                            {
                                query = query.Where(x => false);
                            }
                        }
                        else if (columnFilter.Key == "ToDate")
                        {
                            DateTime date;

                            if (TryParseFilterDate(value, out date))
                            {
                                var nextDate = date.Date.AddDays(1);
                                query = query.Where(x => x.ToDate >= date.Date && x.ToDate < nextDate);
                            }
                            else
                            {
                                query = query.Where(x => false);
                            }
                        }
                        else if (columnFilter.Key == "State")
                        {
                            int state;

                            if (int.TryParse(value, out state))
                            {
                                query = query.Where(x => (int)x.State == state);
                            }
                            else
                            {
                                query = query.Where(x => false);
                            }
                        }
                    }
                }

                var filteredCount = query.Count();

                var items = query
                    .OrderByDescending(x => x.ToDate)
                    .ThenByDescending(x => x.FromDate)
                    .ThenByDescending(x => x.Id)
                    .Skip(Math.Max(0, requestStart))
                    .Take(requestLength <= 0 ? 10 : requestLength)
                    .ToList()
                    .Select(x => new BankReconciliationHistoryDto
                    {
                        Id = x.Id,
                        BankAccountId = x.BankAccountId,
                        BankAccountTitle = x.BankAccount != null ? x.BankAccount.Title : null,
                        FromDate = x.FromDate,
                        ToDate = x.ToDate,
                        State = x.State,
                        FinalizedDate = x.FinalizedDate
                    })
                    .ToList();

                return Success(new BankReconciliationHistoryPageDto
                {
                    Items = items,
                    TotalCount = filteredCount
                });
            }
            catch (Exception e)
            {
                return Error<BankReconciliationHistoryPageDto>(e.Message);
            }
        }

        public CDSResponse<BankReconciliationPageDto> GetPage(
            long reconciliationId,
            BankReconciliationFilterDto filter,
            Dictionary<string, string> columnFilters)
        {
            try
            {
                var reconciliation =
                    _bankReconciliationRepository.Query
                        .Include(x => x.BankAccount.Center)
                        .FirstOrDefault(x => x.Id == reconciliationId);

                if (reconciliation == null)
                    return Error<BankReconciliationPageDto>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                filter = filter ?? new BankReconciliationFilterDto();

                var fromDate = reconciliation.FromDate.Date;
                var toDate = reconciliation.ToDate.Date.AddDays(1);

                var transactionQuery =
                    _bankTransactionRepository.Query.Where(x =>
                        x.BankAccountId == reconciliation.BankAccountId &&
                        x.TransactionDate >= fromDate &&
                        x.TransactionDate < toDate);

                var matchSums =
                    from match in _matchRepository.Query
                    group match by match.BankTransactionId
                    into g
                    select new
                    {
                        BankTransactionId = g.Key,
                        MatchedAmount = g.Sum(x => x.MatchedAmount)
                    };

                var baseQuery =
                    from transaction in transactionQuery
                    join matchSum in matchSums
                        on transaction.Id equals matchSum.BankTransactionId
                        into matches
                    from matchSum in matches.DefaultIfEmpty()
                    select new
                    {
                        Transaction = transaction,
                        MatchedAmount = matchSum == null
                            ? 0
                            : matchSum.MatchedAmount
                    };

                var calculatedQuery =
                    baseQuery.Select(x => new
                    {
                        x.Transaction,
                        x.MatchedAmount,
                        Amount = x.Transaction.Credit > 0
                            ? x.Transaction.Credit
                            : x.Transaction.Debit
                    });

                var totalTransactions = calculatedQuery.Count();

                var totalAmount =
                    calculatedQuery.Select(x => (decimal?)x.Amount).Sum() ?? 0;

                var totalMatchedAmount =
                    calculatedQuery.Select(x => (decimal?)x.MatchedAmount).Sum() ?? 0;

                var matchedTransactions =
                    calculatedQuery.Count(x =>
                        x.MatchedAmount >= x.Amount);

                var partialTransactions =
                    calculatedQuery.Count(x =>
                        x.MatchedAmount > 0 &&
                        x.MatchedAmount < x.Amount);

                var unmatchedTransactions =
                    calculatedQuery.Count(x =>
                        x.MatchedAmount == 0);

                var summary = new BankReconciliationSummaryDto
                {
                    TotalTransactions = totalTransactions,
                    MatchedTransactions = matchedTransactions,
                    PartialTransactions = partialTransactions,
                    UnmatchedTransactions = unmatchedTransactions,
                    TotalAmount = totalAmount,
                    MatchedAmount = totalMatchedAmount,
                    RemainingAmount = totalAmount - totalMatchedAmount,
                    MatchPercentage = totalAmount == 0
                        ? 0
                        : totalMatchedAmount / totalAmount * 100
                };

                var filteredQuery = calculatedQuery;

                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    var search = filter.Search.Trim();

                    filteredQuery = filteredQuery.Where(x =>
                        (x.Transaction.Description != null &&
                         x.Transaction.Description.Contains(search))
                        ||
                        (x.Transaction.TrackingNumber != null &&
                         x.Transaction.TrackingNumber.Contains(search)));
                }

                if (filter.Status.HasValue)
                {
                    if (filter.Status.Value ==
                        BankReconciliationStatus.Unmatched)
                    {
                        filteredQuery = filteredQuery.Where(x =>
                            x.MatchedAmount == 0);
                    }
                    else if (filter.Status.Value ==
                             BankReconciliationStatus.Partial)
                    {
                        filteredQuery = filteredQuery.Where(x =>
                            x.MatchedAmount > 0 &&
                            x.MatchedAmount < x.Amount);
                    }
                    else if (filter.Status.Value ==
                             BankReconciliationStatus.Matched)
                    {
                        filteredQuery = filteredQuery.Where(x =>
                            x.MatchedAmount >= x.Amount);
                    }
                }

                if (columnFilters != null)
                {
                    foreach (var columnFilter in columnFilters)
                    {
                        if (string.IsNullOrWhiteSpace(columnFilter.Value))
                            continue;

                        var value = columnFilter.Value.Trim();

                        if (columnFilter.Key == "TransactionDate")
                        {
                            DateTime date;
                            if (TryParseFilterDate(value, out date))
                            {
                                var nextDate = date.Date.AddDays(1);
                                filteredQuery = filteredQuery.Where(x => x.Transaction.TransactionDate >= date.Date && x.Transaction.TransactionDate < nextDate);
                            }
                            else
                            {
                                filteredQuery = filteredQuery.Where(x => false);
                            }
                        }
                        else if (columnFilter.Key == "Description")
                        {
                            filteredQuery = filteredQuery.Where(x => x.Transaction.Description != null && x.Transaction.Description.Contains(value));
                        }
                        else if (columnFilter.Key == "TrackingNumber")
                        {
                            filteredQuery = filteredQuery.Where(x => x.Transaction.TrackingNumber != null && x.Transaction.TrackingNumber.Contains(value));
                        }
                        else if (columnFilter.Key == "Type")
                        {
                            if (value == "واریز")
                                filteredQuery = filteredQuery.Where(x => x.Transaction.Credit > 0);
                            else if (value == "برداشت")
                                filteredQuery = filteredQuery.Where(x => x.Transaction.Debit > 0);
                            else
                                filteredQuery = filteredQuery.Where(x => false);
                        }
                        else if (columnFilter.Key == "Status")
                        {
                            int status;

                            if (int.TryParse(value, out status))
                            {
                                if (status == (int)BankReconciliationStatus.Matched)
                                    filteredQuery = filteredQuery.Where(x => x.MatchedAmount >= x.Amount);
                                else if (status == (int)BankReconciliationStatus.Partial)
                                    filteredQuery = filteredQuery.Where(x => x.MatchedAmount > 0 && x.MatchedAmount < x.Amount);
                                else if (status == (int)BankReconciliationStatus.Unmatched)
                                    filteredQuery = filteredQuery.Where(x => x.MatchedAmount == 0);
                                else
                                    filteredQuery = filteredQuery.Where(x => false);
                            }
                            else
                            {
                                filteredQuery = filteredQuery.Where(x => false);
                            }
                        }
                        else if (columnFilter.Key == "Amount" || columnFilter.Key == "Balance" || columnFilter.Key == "RemainingAmount")
                        {
                            decimal amount;
                            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount) || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
                            {
                                if (columnFilter.Key == "Amount")
                                    filteredQuery = filteredQuery.Where(x => x.Amount == amount);
                                else if (columnFilter.Key == "Balance")
                                    filteredQuery = filteredQuery.Where(x => x.Transaction.Balance == amount);
                                else
                                    filteredQuery = filteredQuery.Where(x => x.Amount - x.MatchedAmount == amount);
                            }
                            else
                            {
                                filteredQuery = filteredQuery.Where(x => false);
                            }
                        }
                    }
                }

                var totalCount = filteredQuery.Count();

                var page = filter.Page <= 0
                    ? 1
                    : filter.Page;

                var pageSize = filter.PageSize <= 0
                    ? 20
                    : filter.PageSize;

                var transactions = filteredQuery
                    .OrderByDescending(x => x.Transaction.TransactionDate)
                    .ThenByDescending(x => x.Transaction.Id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(x =>
                    {
                        var status = GetStatus(
                            x.Amount,
                            x.MatchedAmount);

                        return new BankReconciliationTransactionDto
                        {
                            Id = x.Transaction.Id,
                            TransactionDate = x.Transaction.TransactionDate,
                            TransactionTime = x.Transaction.TransactionDate.ToString("HH:mm"),
                            Credit = x.Transaction.Credit,
                            Debit = x.Transaction.Debit,
                            Balance = x.Transaction.Balance,
                            Description = x.Transaction.Description,
                            TrackingNumber = x.Transaction.TrackingNumber,
                            Amount = x.Amount,
                            MatchedAmount = x.MatchedAmount,
                            RemainingAmount =
                                x.Amount - x.MatchedAmount,
                            Status = status
                        };
                    })
                    .ToList();

                return Success(new BankReconciliationPageDto
                {
                    Reconciliation = ToDto(reconciliation),
                    Summary = summary,
                    Transactions = transactions,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                });
            }
            catch (Exception e)
            {
                return Error<BankReconciliationPageDto>(e.Message);
            }
        }

        public CDSResponse<List<BankReconciliationMatchDto>> GetMatches(
            long reconciliationId,
            long bankTransactionId)
        {
            try
            {
                var reconciliation =
                    _bankReconciliationRepository.Query
                        .Include(x => x.BankAccount.Center)
                        .FirstOrDefault(x => x.Id == reconciliationId);

                if (reconciliation == null)
                    return Error<List<BankReconciliationMatchDto>>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (!reconciliation.BankAccount.CenterId.HasValue)
                    return Error<List<BankReconciliationMatchDto>>(
                        "برای این حساب بانکی مرکز تعریف نشده است.");

                var fromDate = reconciliation.FromDate.Date;
                var toDate = reconciliation.ToDate.Date.AddDays(1);

                var transactionExists =
                    _bankTransactionRepository.Query.Any(x =>
                        x.Id == bankTransactionId &&
                        x.BankAccountId == reconciliation.BankAccountId &&
                        x.TransactionDate >= fromDate &&
                        x.TransactionDate < toDate);

                if (!transactionExists)
                    return Error<List<BankReconciliationMatchDto>>(
                        "تراکنش بانکی در این مغایرت بانکی وجود ندارد.");

                var query =
                    from match in _matchRepository.Query
                    where match.ReconciliationId == reconciliationId &&
                          match.BankTransactionId == bankTransactionId
                    select new BankReconciliationMatchDto
                    {
                        Id = match.Id,
                        BankTransactionId =
                            match.BankTransactionId,
                        AccountingDocDetailId =
                            match.AccountingDocDetailId,
                        AccountingDocId =
                            match.AccountingDocDetail.AccountingDocId,
                        AccountingDocDate =
                            match.AccountingDocDetail.AccountingDoc.AccountingDocDate,
                        MatchedAmount =
                            match.MatchedAmount,
                        MatchType =
                            match.MatchType,
                        MatchDate =
                            match.MatchDate,
                        AccountId =
                            match.AccountingDocDetail.AccountId,
                        AccountTitle =
                            match.AccountingDocDetail.Account.Title,
                        CenterId =
                            reconciliation.BankAccount.CenterId.Value,
                        CenterTitle =
                            reconciliation.BankAccount.Center.Title,
                        Description =
                            match.AccountingDocDetail.Description,
                        TrackingNumber =
                            match.AccountingDocDetail.DepositSlip != null
                                ? match.AccountingDocDetail.DepositSlip.Number
                                : match.AccountingDocDetail.Cheque != null
                                    ? match.AccountingDocDetail.Cheque.Serial
                                    : null
                    };

                var result = query
                    .OrderByDescending(x => x.MatchDate)
                    .ThenByDescending(x => x.Id)
                    .ToList();

                return Success(result);
            }
            catch (Exception e)
            {
                return Error<List<BankReconciliationMatchDto>>(e.Message);
            }
        }

        public CDSResponse<bool> Match(
            BankReconciliationMatchRequest request)
        {
            try
            {
                if (request == null)
                    return Error<bool>(
                        "اطلاعات تطبیق نامعتبر است.");

                var reconciliation =
                    GetReconciliationForWrite(
                        request.ReconciliationId);

                if (reconciliation == null)
                    return Error<bool>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State !=
                    BankReconciliationState.Open)
                    return Error<bool>(
                        "مغایرت بانکی نهایی شده است.");

                if (!reconciliation.BankAccount.CenterId.HasValue)
                    return Error<bool>(
                        "برای این حساب بانکی مرکز تعریف نشده است و امکان مغایرت‌گیری وجود ندارد.");

                var bankTransaction =
                    GetBankTransaction(
                        reconciliation,
                        request.BankTransactionId);

                if (bankTransaction == null)
                    return Error<bool>(
                        "تراکنش بانکی در این مغایرت بانکی وجود ندارد.");

                var detail =
                    _accountingDocDetailRepository.Query
                        .Include(x => x.AccountingDocDetailCenters)
                        .Include(x => x.AccountingDoc)
                        .FirstOrDefault(x =>
                            x.Id == request.AccountingDocDetailId);

                if (detail == null)
                    return Error<bool>(
                        "سند حسابداری مورد نظر یافت نشد.");

                var validation =
                    ValidateAccountingDetail(
                        detail,
                        reconciliation,
                        bankTransaction);

                if (!validation.Valid)
                    return Error<bool>(validation.Message);

                var bankAmount = GetAmount(bankTransaction);

                var bankMatchedAmount =
                    GetBankMatchedAmount(
                        bankTransaction.Id);

                var bankRemaining =
                    bankAmount - bankMatchedAmount;

                var detailAmount = GetAmount(detail);

                var detailMatchedAmount =
                    GetAccountingMatchedAmount(
                        detail.Id);

                var detailRemaining =
                    detailAmount - detailMatchedAmount;

                if (bankRemaining <= 0)
                    return Error<bool>(
                        "مانده تراکنش بانکی قبلاً به طور کامل تطبیق داده شده است.");

                if (detailRemaining <= 0)
                    return Error<bool>(
                        "مانده سند حسابداری قبلاً به طور کامل تطبیق داده شده است.");

                if (bankRemaining != detailRemaining)
                    return Error<bool>(
                        "مبلغ باقی‌مانده تراکنش بانکی و سند حسابداری برابر نیست.");

                var userId = GetCurrentUserId();

                var match = new BankReconciliationMatch
                {
                    ReconciliationId = reconciliation.Id,
                    BankTransactionId = bankTransaction.Id,
                    AccountingDocDetailId = detail.Id,
                    MatchedAmount = bankRemaining,
                    MatchType = BankReconciliationMatchType.Manual,
                    MatchDate = DateTime.Now,
                    CreatorUserId = userId,
                    CreateDate = DateTime.Now,
                    EntityState = EntityStates.Added
                };

                _matchRepository.Add(match);

                _unitOfWorkScope.Commit();

                return Success(true);
            }
            catch (Exception e)
            {
                return Error<bool>(e.Message);
            }
        }

        public CDSResponse<bool> GroupBankToDetails(
            BankReconciliationGroupBankToDetailsRequest request)
        {
            try
            {
                if (request == null ||
                    request.AccountingDocDetailIds == null ||
                    request.AccountingDocDetailIds.Count == 0)
                    return Error<bool>(
                        "حداقل یک سند حسابداری باید انتخاب شود.");

                var reconciliation =
                    GetReconciliationForWrite(
                        request.ReconciliationId);

                if (reconciliation == null)
                    return Error<bool>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State !=
                    BankReconciliationState.Open)
                    return Error<bool>(
                        "مغایرت بانکی نهایی شده است.");

                if (!reconciliation.BankAccount.CenterId.HasValue)
                    return Error<bool>(
                        "برای این حساب بانکی مرکز تعریف نشده است.");

                var bankTransaction =
                    GetBankTransaction(
                        reconciliation,
                        request.BankTransactionId);

                if (bankTransaction == null)
                    return Error<bool>(
                        "تراکنش بانکی در این مغایرت بانکی وجود ندارد.");

                var bankRemaining =
                    GetRemainingBankAmount(
                        bankTransaction);

                if (bankRemaining <= 0)
                    return Error<bool>(
                        "مانده تراکنش بانکی صفر است.");

                var ids = request.AccountingDocDetailIds
                    .Distinct()
                    .ToList();

                var details =
                    _accountingDocDetailRepository.Query
                        .Include(x => x.AccountingDocDetailCenters)
                        .Include(x => x.AccountingDoc)
                        .Where(x => ids.Contains(x.Id))
                        .ToList();

                if (details.Count != ids.Count)
                    return Error<bool>(
                        "یک یا چند سند حسابداری یافت نشد.");

                decimal totalRemaining = 0;

                foreach (var detail in details)
                {
                    var validation =
                        ValidateAccountingDetail(
                            detail,
                            reconciliation,
                            bankTransaction);

                    if (!validation.Valid)
                        return Error<bool>(validation.Message);

                    var remaining =
                        GetRemainingAccountingAmount(detail);

                    if (remaining <= 0)
                        return Error<bool>(
                            "یکی از اسناد حسابداری مانده قابل استفاده ندارد.");

                    if (remaining > bankRemaining)
                        return Error<bool>(
                            "مبلغ یکی از اسناد حسابداری از مانده تراکنش بانکی بیشتر است.");

                    totalRemaining += remaining;
                }

                if (totalRemaining != bankRemaining)
                    return Error<bool>(
                        "مجموع مبالغ انتخاب‌شده با مانده تراکنش بانکی برابر نیست.");

                var userId = GetCurrentUserId();

                foreach (var detail in details)
                {
                    var amount =
                        GetRemainingAccountingAmount(detail);

                    _matchRepository.Add(
                        new BankReconciliationMatch
                        {
                            ReconciliationId =
                                reconciliation.Id,
                            BankTransactionId =
                                bankTransaction.Id,
                            AccountingDocDetailId =
                                detail.Id,
                            MatchedAmount =
                                amount,
                            MatchType =
                                BankReconciliationMatchType.Group,
                            MatchDate =
                                DateTime.Now,
                            CreatorUserId =
                                userId,
                            CreateDate =
                                DateTime.Now,
                            EntityState =
                                EntityStates.Added
                        });
                }

                _unitOfWorkScope.Commit();

                return Success(true);
            }
            catch (Exception e)
            {
                return Error<bool>(e.Message);
            }
        }

        public CDSResponse<bool> GroupBanksToDetail(
            BankReconciliationGroupBanksToDetailRequest request)
        {
            try
            {
                if (request == null ||
                    request.BankTransactionIds == null ||
                    request.BankTransactionIds.Count == 0)
                    return Error<bool>(
                        "حداقل یک تراکنش بانکی باید انتخاب شود.");

                var reconciliation =
                    GetReconciliationForWrite(
                        request.ReconciliationId);

                if (reconciliation == null)
                    return Error<bool>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State !=
                    BankReconciliationState.Open)
                    return Error<bool>(
                        "مغایرت بانکی نهایی شده است.");

                if (!reconciliation.BankAccount.CenterId.HasValue)
                    return Error<bool>(
                        "برای این حساب بانکی مرکز تعریف نشده است.");

                var detail =
                    _accountingDocDetailRepository.Query
                        .Include(x => x.AccountingDocDetailCenters)
                        .Include(x => x.AccountingDoc)
                        .FirstOrDefault(x =>
                            x.Id == request.AccountingDocDetailId);

                if (detail == null)
                    return Error<bool>(
                        "سند حسابداری مورد نظر یافت نشد.");

                var ids = request.BankTransactionIds
                    .Distinct()
                    .ToList();

                var transactions =
                    _bankTransactionRepository.Query
                        .Where(x =>
                            ids.Contains(x.Id) &&
                            x.BankAccountId ==
                            reconciliation.BankAccountId)
                        .ToList();

                if (transactions.Count != ids.Count)
                    return Error<bool>(
                        "یک یا چند تراکنش بانکی متعلق به این مغایرت نیستند.");

                var detailRemaining =
                    GetRemainingAccountingAmount(detail);

                if (detailRemaining <= 0)
                    return Error<bool>(
                        "مانده سند حسابداری صفر است.");

                decimal totalRemaining = 0;

                foreach (var transaction in transactions)
                {
                    var validation =
                        ValidateAccountingDetail(
                            detail,
                            reconciliation,
                            transaction);

                    if (!validation.Valid)
                        return Error<bool>(validation.Message);

                    var remaining =
                        GetRemainingBankAmount(transaction);

                    if (remaining <= 0)
                        return Error<bool>(
                            "یکی از تراکنش‌های بانکی مانده قابل استفاده ندارد.");

                    if (remaining > detailRemaining)
                        return Error<bool>(
                            "مبلغ یکی از تراکنش‌های بانکی از مانده سند حسابداری بیشتر است.");

                    totalRemaining += remaining;
                }

                if (totalRemaining != detailRemaining)
                    return Error<bool>(
                        "مجموع مبالغ تراکنش‌های بانکی با مانده سند حسابداری برابر نیست.");

                var userId = GetCurrentUserId();

                foreach (var transaction in transactions)
                {
                    var amount =
                        GetRemainingBankAmount(transaction);

                    _matchRepository.Add(
                        new BankReconciliationMatch
                        {
                            ReconciliationId =
                                reconciliation.Id,
                            BankTransactionId =
                                transaction.Id,
                            AccountingDocDetailId =
                                detail.Id,
                            MatchedAmount =
                                amount,
                            MatchType =
                                BankReconciliationMatchType.Group,
                            MatchDate =
                                DateTime.Now,
                            CreatorUserId =
                                userId,
                            CreateDate =
                                DateTime.Now,
                            EntityState =
                                EntityStates.Added
                        });
                }

                _unitOfWorkScope.Commit();

                return Success(true);
            }
            catch (Exception e)
            {
                return Error<bool>(e.Message);
            }
        }

        public CDSResponse<bool> RemoveMatch(
            long reconciliationId,
            long matchId)
        {
            try
            {
                var reconciliation =
                    GetReconciliationForWrite(reconciliationId);

                if (reconciliation == null)
                    return Error<bool>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State !=
                    BankReconciliationState.Open)
                    return Error<bool>(
                        "مغایرت بانکی نهایی شده است.");

                var match =
                    _matchRepository.Query
                        .FirstOrDefault(x =>
                            x.Id == matchId &&
                            x.ReconciliationId == reconciliationId);

                if (match == null)
                    return Error<bool>(
                        "تطبیق مورد نظر یافت نشد.");

                _matchRepository.Delete(match);

                _unitOfWorkScope.Commit();

                return Success(true);
            }
            catch (Exception e)
            {
                return Error<bool>(e.Message);
            }
        }

        public CDSResponse<bool> Finalize(long reconciliationId)
        {
            try
            {
                var reconciliation =
                    GetReconciliationForWrite(reconciliationId);

                if (reconciliation == null)
                    return Error<bool>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State ==
                    BankReconciliationState.Finalized)
                    return Error<bool>(
                        "مغایرت بانکی قبلاً نهایی شده است.");

                var userId = GetCurrentUserId();

                reconciliation.State =
                    BankReconciliationState.Finalized;

                reconciliation.FinalizedDate =
                    DateTime.Now;

                reconciliation.FinalizedByUserId =
                    userId;

                reconciliation.ModifierUserId =
                    userId;

                reconciliation.ModifyDate =
                    DateTime.Now;

                reconciliation.EntityState =
                    EntityStates.Modify;

                _unitOfWorkScope.Commit();

                return Success(true);
            }
            catch (Exception e)
            {
                return Error<bool>(e.Message);
            }
        }

        public CDSResponse<bool> Reopen(long reconciliationId)
        {
            try
            {
                var reconciliation =
                    GetReconciliationForWrite(reconciliationId);

                if (reconciliation == null)
                    return Error<bool>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State !=
                    BankReconciliationState.Finalized)
                    return Error<bool>(
                        "مغایرت بانکی باز است.");

                var userId = GetCurrentUserId();

                reconciliation.State =
                    BankReconciliationState.Open;

                reconciliation.FinalizedDate = null;

                reconciliation.FinalizedByUserId = null;

                reconciliation.ModifierUserId =
                    userId;

                reconciliation.ModifyDate =
                    DateTime.Now;

                reconciliation.EntityState =
                    EntityStates.Modify;

                _unitOfWorkScope.Commit();

                return Success(true);
            }
            catch (Exception e)
            {
                return Error<bool>(e.Message);
            }
        }

        public CDSResponse<bool> Delete(long reconciliationId)
        {
            try
            {
                var reconciliation =
                    GetReconciliationForWrite(reconciliationId);

                if (reconciliation == null)
                    return Error<bool>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State ==
                    BankReconciliationState.Finalized)
                    return Error<bool>(
                        "مغایرت بانکی نهایی شده است و امکان حذف آن وجود ندارد.");

                var matches =
                    _matchRepository.Query
                        .Where(x =>
                            x.ReconciliationId == reconciliationId)
                        .ToList();

                foreach (var match in matches)
                    _matchRepository.Delete(match);

                _bankReconciliationRepository.Delete(
                    reconciliation);

                _unitOfWorkScope.Commit();

                return Success(true);
            }
            catch (Exception e)
            {
                return Error<bool>(e.Message);
            }
        }

        public CDSResponse<List<BankReconciliationAutoMatchResultDto>> AutoMatch(
            long reconciliationId)
        {
            try
            {
                var reconciliation =
                    GetReconciliationForWrite(reconciliationId);

                if (reconciliation == null)
                    return Error<List<BankReconciliationAutoMatchResultDto>>(
                        "مغایرت بانکی مورد نظر یافت نشد.");

                if (reconciliation.State !=
                    BankReconciliationState.Open)
                    return Error<List<BankReconciliationAutoMatchResultDto>>(
                        "مغایرت بانکی نهایی شده است.");

                if (!reconciliation.BankAccount.CenterId.HasValue)
                    return Error<List<BankReconciliationAutoMatchResultDto>>(
                        "برای این حساب بانکی مرکز تعریف نشده است.");

                var allowedAccountIds =
                    GetAllowedAccountIds();

                if (allowedAccountIds.Count == 0)
                    return Error<List<BankReconciliationAutoMatchResultDto>>(
                        "تنظیم حساب‌های مجاز برای مغایرت‌گیری بانکی معتبر نیست.");

                var fromDate = reconciliation.FromDate.Date;
                var toDate = reconciliation.ToDate.Date.AddDays(1);
                var candidateFromDate = reconciliation.FromDate.Date.AddDays(-2);
                var candidateToDate = reconciliation.ToDate.Date.AddDays(3);
                var centerId = reconciliation.BankAccount.CenterId.Value;

                var transactions =
                    _bankTransactionRepository.Query
                        .Where(x =>
                            x.BankAccountId == reconciliation.BankAccountId &&
                            x.TransactionDate >= fromDate &&
                            x.TransactionDate < toDate)
                        .ToList();

                var transactionIds = transactions.Select(x => x.Id).ToList();

                var bankMatchedAmounts =
                    _matchRepository.Query
                        .Where(x => transactionIds.Contains(x.BankTransactionId))
                        .GroupBy(x => x.BankTransactionId)
                        .Select(g => new
                        {
                            BankTransactionId = g.Key,
                            MatchedAmount = g.Sum(x => x.MatchedAmount)
                        })
                        .ToList()
                        .ToDictionary(x => x.BankTransactionId, x => x.MatchedAmount);

                var details =
                    _accountingDocDetailRepository.Query
                        .Include(x => x.AccountingDocDetailCenters)
                        .Include(x => x.AccountingDoc)
                        .Include(x => x.DepositSlip)
                        .Include(x => x.Cheque)
                        .Where(x =>
                            allowedAccountIds.Contains(x.AccountId) &&
                            x.AccountingDoc.AccountingDocDate >= candidateFromDate &&
                            x.AccountingDoc.AccountingDocDate < candidateToDate &&
                            x.AccountingDocDetailCenters.Any(y => y.CenterId == centerId))
                        .ToList();

                var accountingDocIds = details
                    .Select(x => x.AccountingDocId)
                    .Distinct()
                    .ToList();

                var counterpartDetails =
                    _accountingDocDetailRepository.Query
                        .Include(x => x.AccountingDocDetailCenters.Select(y => y.Center))
                        .Where(x => accountingDocIds.Contains(x.AccountingDocId))
                        .Select(x => new
                        {
                            x.Id,
                            x.AccountingDocId,
                            CenterTitles = x.AccountingDocDetailCenters
                                .Where(y => y.Center != null)
                                .Select(y => y.Center.Title)
                        })
                        .ToList();

                var counterpartCenterTitlesByDetailId =
                    details.ToDictionary(
                        x => x.Id,
                        x => string.Join(
                            "، ",
                            counterpartDetails
                                .Where(y =>
                                    y.AccountingDocId == x.AccountingDocId &&
                                    y.Id != x.Id)
                                .SelectMany(y => y.CenterTitles)
                                .Where(y => !string.IsNullOrWhiteSpace(y))
                                .Distinct()
                                .ToList()));

                var detailIds = details.Select(x => x.Id).ToList();

                var accountingMatchedAmounts =
                    _matchRepository.Query
                        .Where(x => detailIds.Contains(x.AccountingDocDetailId))
                        .GroupBy(x => x.AccountingDocDetailId)
                        .Select(g => new
                        {
                            AccountingDocDetailId = g.Key,
                            MatchedAmount = g.Sum(x => x.MatchedAmount)
                        })
                        .ToList()
                        .ToDictionary(x => x.AccountingDocDetailId, x => x.MatchedAmount);

                var results =
                    new List<BankReconciliationAutoMatchResultDto>();

                var userId = GetCurrentUserId();

                foreach (var transaction in transactions)
                {
                    var bankAmount = GetAmount(transaction);

                    decimal bankMatchedAmount;
                    if (!bankMatchedAmounts.TryGetValue(transaction.Id, out bankMatchedAmount))
                        bankMatchedAmount = 0;

                    var bankRemaining = bankAmount - bankMatchedAmount;

                    if (bankRemaining <= 0)
                    {
                        results.Add(new BankReconciliationAutoMatchResultDto
                        {
                            BankTransactionId = transaction.Id,
                            Matched = false,
                            MatchedAmount = 0,
                            Status = GetStatus(bankAmount, bankMatchedAmount),
                            CandidateCount = 0,
                            Reason = "مانده تراکنش بانکی صفر است."
                        });
                        continue;
                    }

                    var candidates =
                        new List<AutoMatchCandidate>();

                    foreach (var detail in details)
                    {
                        if (!IsOppositeDirection(transaction, detail))
                            continue;

                        var dateDifference =
                            Math.Abs(
                                (detail.AccountingDoc.AccountingDocDate.Date -
                                 transaction.TransactionDate.Date).Days);

                        if (dateDifference > 2)
                            continue;

                        decimal accountingMatchedAmount;
                        if (!accountingMatchedAmounts.TryGetValue(
                                detail.Id,
                                out accountingMatchedAmount))
                        {
                            accountingMatchedAmount = 0;
                        }

                        var remaining =
                            GetAmount(detail) - accountingMatchedAmount;

                        if (remaining <= 0 || remaining != bankRemaining)
                            continue;

                        var tracking = GetTrackingNumber(detail);
                        var trackingMatched =
                            !string.IsNullOrWhiteSpace(transaction.TrackingNumber) &&
                            !string.IsNullOrWhiteSpace(tracking) &&
                            transaction.TrackingNumber == tracking;

                        var trackingInDescription =
                            ContainsText(transaction.Description, tracking);

                        string counterpartCenterTitle;
                        if (!counterpartCenterTitlesByDetailId.TryGetValue(
                                detail.Id,
                                out counterpartCenterTitle))
                        {
                            counterpartCenterTitle = null;
                        }

                        var counterpartCenterInDescription =
                            ContainsAnyText(
                                transaction.Description,
                                counterpartCenterTitle);

                        var score = 100;
                        var scoreDetails = new List<string>
                        {
                            "مبلغ: +100"
                        };

                        if (trackingMatched)
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

                        if (dateDifference == 0)
                        {
                            score += 20;
                            scoreDetails.Add("تاریخ: +20");
                        }
                        else if (dateDifference == 1)
                        {
                            score += 10;
                            scoreDetails.Add("اختلاف تاریخ یک روز: +10");
                        }

                        candidates.Add(new AutoMatchCandidate
                        {
                            Detail = detail,
                            Score = score,
                            ScoreDetails = string.Join("، ", scoreDetails)
                        });
                    }

                    var orderedCandidates = candidates
                        .OrderByDescending(x => x.Score)
                        .ThenBy(x => Math.Abs(
                            (x.Detail.AccountingDoc.AccountingDocDate.Date -
                             transaction.TransactionDate.Date).Days))
                        .ThenBy(x => x.Detail.Id)
                        .ToList();

                    if (orderedCandidates.Count == 0)
                    {
                        results.Add(new BankReconciliationAutoMatchResultDto
                        {
                            BankTransactionId = transaction.Id,
                            Matched = false,
                            MatchedAmount = 0,
                            Status = GetStatus(
                                bankAmount,
                                bankMatchedAmount),
                            CandidateCount = 0,
                            Reason = "کاندیدای معتبر یافت نشد."
                        });

                        continue;
                    }

                    var selectedCandidate = orderedCandidates[0];
                    var topScore = selectedCandidate.Score;

                    if (topScore <= 100)
                    {
                        results.Add(new BankReconciliationAutoMatchResultDto
                        {
                            BankTransactionId = transaction.Id,
                            Matched = false,
                            MatchedAmount = 0,
                            Status = GetStatus(
                                bankAmount,
                                bankMatchedAmount),
                            CandidateCount = orderedCandidates.Count,
                            MatchScore = topScore,
                            MatchScoreDetails = selectedCandidate.ScoreDetails,
                            SecondCandidateScore = orderedCandidates.Count > 1
                                ? orderedCandidates[1].Score
                                : 0,
                            ScoreDifference = orderedCandidates.Count > 1
                                ? topScore - orderedCandidates[1].Score
                                : topScore,
                            Reason = "امتیاز تطبیق باید بیشتر از 100 باشد."
                        });

                        continue;
                    }

                    var sameScoreCount = orderedCandidates.Count(
                        x => x.Score == topScore);

                    if (sameScoreCount != 1)
                    {
                        results.Add(new BankReconciliationAutoMatchResultDto
                        {
                            BankTransactionId = transaction.Id,
                            Matched = false,
                            MatchedAmount = 0,
                            Status = GetStatus(
                                bankAmount,
                                bankMatchedAmount),
                            CandidateCount = orderedCandidates.Count,
                            MatchScore = topScore,
                            MatchScoreDetails = selectedCandidate.ScoreDetails,
                            SecondCandidateScore = topScore,
                            ScoreDifference = 0,
                            Reason = "بیش از یک کاندیدای هم‌امتیاز وجود دارد."
                        });

                        continue;
                    }

                    var secondScore =
                        orderedCandidates.Count > 1
                            ? orderedCandidates[1].Score
                            : 0;

                    var selected = selectedCandidate.Detail;

                    _matchRepository.Add(
                        new BankReconciliationMatch
                        {
                            ReconciliationId =
                                reconciliation.Id,
                            BankTransactionId =
                                transaction.Id,
                            AccountingDocDetailId =
                                selected.Id,
                            MatchedAmount =
                                bankRemaining,
                            MatchType =
                                BankReconciliationMatchType.Automatic,
                            MatchDate =
                                DateTime.Now,
                            CreatorUserId =
                                userId,
                            CreateDate =
                                DateTime.Now,
                            EntityState =
                                EntityStates.Added
                        });

                    bankMatchedAmounts[transaction.Id] =
                        bankMatchedAmount + bankRemaining;

                    decimal selectedMatchedAmount;
                    if (!accountingMatchedAmounts.TryGetValue(
                            selected.Id,
                            out selectedMatchedAmount))
                    {
                        selectedMatchedAmount = 0;
                    }

                    accountingMatchedAmounts[selected.Id] =
                        selectedMatchedAmount + bankRemaining;

                    results.Add(new BankReconciliationAutoMatchResultDto
                    {
                        BankTransactionId =
                            transaction.Id,
                        Matched = true,
                        AccountingDocDetailId =
                            selected.Id,
                        MatchedAmount =
                            bankRemaining,
                        Status =
                            BankReconciliationStatus.Matched,
                        CandidateCount =
                            orderedCandidates.Count,
                        MatchScore =
                            selectedCandidate.Score,
                        MatchScoreDetails =
                            selectedCandidate.ScoreDetails,
                        SecondCandidateScore =
                            secondScore,
                        ScoreDifference =
                            selectedCandidate.Score - secondScore,
                        Reason =
                            "تطبیق خودکار انجام شد."
                    });
                }

                _unitOfWorkScope.Commit();

                return Success(results);
            }
            catch (Exception e)
            {
                return Error<List<BankReconciliationAutoMatchResultDto>>(
                    e.Message);
            }
        }

        private bool ContainsAnyText(
            string source,
            string values)
        {
            if (string.IsNullOrWhiteSpace(source) ||
                string.IsNullOrWhiteSpace(values))
                return false;

            return values
                .Split(new[] { '،', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Any(x => ContainsText(source, x.Trim()));
        }

        private bool ContainsText(
            string source,
            string value)
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

        private BankReconciliation GetReconciliationForWrite(
            long reconciliationId)
        {
            return _bankReconciliationRepository.Query
                .Include(x => x.BankAccount.Center)
                .FirstOrDefault(x => x.Id == reconciliationId);
        }

        private BankTransaction GetBankTransaction(
            BankReconciliation reconciliation,
            long bankTransactionId)
        {
            var fromDate = reconciliation.FromDate.Date;
            var toDate = reconciliation.ToDate.Date.AddDays(1);

            return _bankTransactionRepository.Query
                .FirstOrDefault(x =>
                    x.Id == bankTransactionId &&
                    x.BankAccountId ==
                        reconciliation.BankAccountId &&
                    x.TransactionDate >= fromDate &&
                    x.TransactionDate < toDate);
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
                    x.BankTransactionId == bankTransactionId)
                .Select(x => (decimal?)x.MatchedAmount)
                .Sum() ?? 0;
        }

        private decimal GetAccountingMatchedAmount(
            long accountingDocDetailId)
        {
            return _matchRepository.Query
                .Where(x =>
                    x.AccountingDocDetailId ==
                    accountingDocDetailId)
                .Select(x => (decimal?)x.MatchedAmount)
                .Sum() ?? 0;
        }

        private decimal GetRemainingBankAmount(
            BankTransaction transaction)
        {
            return GetAmount(transaction) -
                   GetBankMatchedAmount(transaction.Id);
        }

        private decimal GetRemainingAccountingAmount(
            AccountingDocDetail detail)
        {
            return GetAmount(detail) -
                   GetAccountingMatchedAmount(detail.Id);
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

        private ValidationResult ValidateAccountingDetail(
            AccountingDocDetail detail,
            BankReconciliation reconciliation,
            BankTransaction transaction)
        {
            if (!GetAllowedAccountIds()
                .Contains(detail.AccountId))
            {
                return ValidationResult.Fail(
                    "حساب سند حسابداری در لیست حساب‌های مجاز مغایرت‌گیری نیست.");
            }

            var centerId =
                reconciliation.BankAccount.CenterId;

            if (!centerId.HasValue)
            {
                return ValidationResult.Fail(
                    "برای حساب بانکی مرکز تعریف نشده است.");
            }

            var hasCenter =
                detail.AccountingDocDetailCenters
                    .Any(x => x.CenterId == centerId.Value);

            if (!hasCenter)
            {
                return ValidationResult.Fail(
                    "مرکز سند حسابداری با مرکز حساب بانکی مطابقت ندارد.");
            }

            if (!IsOppositeDirection(
                    transaction,
                    detail))
            {
                return ValidationResult.Fail(
                    "جهت تراکنش بانکی و سند حسابداری مطابقت ندارد.");
            }

            var validation =
                ValidateBankDate(
                    reconciliation,
                    transaction);

            if (!validation.Valid)
                return validation;

            var dateDifference =
                Math.Abs(
                    (detail.AccountingDoc.AccountingDocDate.Date -
                     transaction.TransactionDate.Date).Days);

            if (dateDifference > 2)
            {
                return ValidationResult.Fail(
                    "اختلاف تاریخ بیشتر از دو روز است.");
            }

            return ValidationResult.Ok();
        }

        private ValidationResult ValidateBankDate(
            BankReconciliation reconciliation,
            BankTransaction transaction)
        {
            var fromDate =
                reconciliation.FromDate.Date;

            var toDate =
                reconciliation.ToDate.Date;

            if (transaction.TransactionDate.Date < fromDate ||
                transaction.TransactionDate.Date > toDate)
            {
                return ValidationResult.Fail(
                    "تراکنش بانکی خارج از بازه مغایرت است.");
            }

            return ValidationResult.Ok();
        }

        private bool IsOppositeDirection(
            BankTransaction transaction,
            AccountingDocDetail detail)
        {
            if (transaction.Credit > 0)
                return detail.Debit > 0;

            return detail.Credit > 0;
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

        private List<int> GetAllowedAccountIds()
        {
            var setting =
                _settingRepository.Query
                    .FirstOrDefault(x =>
                        x.Key ==
                        "BankReconciliationAllowedAccountIds");

            if (setting == null ||
                string.IsNullOrWhiteSpace(setting.Value))
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

        private bool HasCenter(
            AccountingDocDetail detail,
            int centerId)
        {
            return detail.AccountingDocDetailCenters
                .Any(x => x.CenterId == centerId);
        }

        private BankReconciliationDto ToDto(
            BankReconciliation reconciliation)
        {
            var bankAccount =
                reconciliation.BankAccount;

            return new BankReconciliationDto
            {
                Id = reconciliation.Id,
                BankAccountId =
                    reconciliation.BankAccountId,
                BankAccountTitle =
                    bankAccount != null
                        ? bankAccount.Title
                        : null,
                AccountNumber =
                    bankAccount != null
                        ? bankAccount.AccountNumber
                        : null,
                IBAN =
                    bankAccount != null
                        ? bankAccount.IBAN
                        : null,
                CenterId =
                    bankAccount != null
                        ? bankAccount.CenterId
                        : null,
                CenterTitle =
                    bankAccount != null &&
                    bankAccount.Center != null
                        ? bankAccount.Center.Title
                        : null,
                FromDate =
                    reconciliation.FromDate,
                ToDate =
                    reconciliation.ToDate,
                State =
                    reconciliation.State,
                FinalizedDate =
                    reconciliation.FinalizedDate,
                FinalizedByUserId =
                    reconciliation.FinalizedByUserId
            };
        }

        private int GetCurrentUserId()
        {
            return Convert.ToInt32(
                HttpContext.Current.Session["UserId"]);
        }


        private bool TryParseFilterDate(string value, out DateTime date)
        {
            var formats = new[] { "yyyy/MM/dd", "yyyy-MM-dd", "yyyy/M/d", "yyyy-M-d" };
            if (DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                if (date.Year >= 1200 && date.Year <= 1700)
                {
                    var calendar = new PersianCalendar();
                    try { date = calendar.ToDateTime(date.Year, date.Month, date.Day, 0, 0, 0, 0); }
                    catch { return false; }
                }
                return true;
            }
            date = default(DateTime);
            return false;
        }
        private CDSResponse<T> Success<T>(T data)
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

        private class AutoMatchCandidate
        {
            public AccountingDocDetail Detail { get; set; }
            public int Score { get; set; }
            public string ScoreDetails { get; set; }
        }

        private class ValidationResult
        {
            public bool Valid { get; set; }
            public string Message { get; set; }

            public static ValidationResult Ok()
            {
                return new ValidationResult
                {
                    Valid = true
                };
            }

            public static ValidationResult Fail(
                string message)
            {
                return new ValidationResult
                {
                    Valid = false,
                    Message = message
                };
            }
        }
    }
}