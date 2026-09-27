using CDS.BIMS.Application.Dto.Financial;
using CDS.BIMS.Presentation.WebUI.ActionFilter;
using DataTables.AspNet.Core;
using System.Linq;
using CDS.BIMS.Domain.Model.Enum.Financial;
using System.Web.Mvc;
using CDS.BIMS.Application.ServiceContract.Financial;

namespace CDS.BIMS.Presentation.WebUI.Controllers.Financial
{
    public class BankReconciliationController : CDSBaseController
    {
        private readonly IBankReconciliationService
            _bankReconciliationService;

        private readonly IBankReconciliationCandidateService
            _candidateService;

        public BankReconciliationController(
            IBankReconciliationService bankReconciliationService,
            IBankReconciliationCandidateService candidateService)
        {
            _bankReconciliationService =
                bankReconciliationService;

            _candidateService =
                candidateService;
        }

        [HttpGet]
        [TokenAuthorize]
        public ActionResult Index()
        {
            return View();
        }



        [HttpPost]
        public JsonResult Create(
            BankReconciliationCreateRequest request)
        {
            var result =
                _bankReconciliationService.Create(
                    request);

            return Json(result);
        }

        [HttpGet]
        public JsonResult Get(long id)
        {
            var result =
                _bankReconciliationService.Get(id);

            return Json(
                result,
                JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetHistory(int? bankAccountId)
        {
            var result =
                _bankReconciliationService.GetHistory(bankAccountId);

            return Json(
                result,
                JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GetPage(
            long reconciliationId,
            BankReconciliationStatus? status,
            IDataTablesRequest request)
        {
            var columnFilters = request.Columns
                .Where(x => x.Search != null &&
                            !string.IsNullOrWhiteSpace(x.Search.Value))
                .ToDictionary(
                    x => x.Field.Substring(x.Field.LastIndexOf('.') + 1),
                    x => x.Search.Value);

            var filter = new BankReconciliationFilterDto
            {
                Search = request.Search == null
                    ? null
                    : request.Search.Value,
                Status = status,
                Page = request.Length <= 0
                    ? 1
                    : request.Start / request.Length + 1,
                PageSize = request.Length <= 0
                    ? 20
                    : request.Length
            };

            var result =
                _bankReconciliationService.GetPage(
                    reconciliationId,
                    filter,
                    columnFilters);

            return Json(result);
        }

        [HttpGet]
        public JsonResult GetMatches(
            long reconciliationId,
            long bankTransactionId)
        {
            var result =
                _bankReconciliationService.GetMatches(
                    reconciliationId,
                    bankTransactionId);

            return Json(
                result,
                JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult GetCandidates(
            long reconciliationId,
            long bankTransactionId,
            int dateTolerance,
            BankReconciliationCandidateMode mode,
            bool? trackingMatched,
            IDataTablesRequest request)
        {
            var columnFilters = request.Columns
                .Where(x => x.Search != null &&
                            !string.IsNullOrWhiteSpace(x.Search.Value))
                .ToDictionary(
                    x => x.Field.Substring(x.Field.LastIndexOf('.') + 1),
                    x => x.Search.Value);

            var filter = new BankReconciliationCandidateFilterDto
            {
                Search = request.Search == null
                    ? null
                    : request.Search.Value,
                TrackingMatched = trackingMatched,
                DateTolerance = dateTolerance,
                Mode = mode,
                Page = request.Length <= 0
                    ? 1
                    : request.Start / request.Length + 1,
                PageSize = request.Length <= 0
                    ? 20
                    : request.Length
            };

            var result =
                _candidateService.GetCandidates(
                    reconciliationId,
                    bankTransactionId,
                    filter,
                    columnFilters);

            return Json(result);
        }

        [HttpPost]
        public JsonResult Match(
            BankReconciliationMatchRequest request)
        {
            var result =
                _bankReconciliationService.Match(
                    request);

            return Json(result);
        }

        [HttpPost]
        public JsonResult GroupBankToDetails(
            BankReconciliationGroupBankToDetailsRequest request)
        {
            var result =
                _bankReconciliationService.GroupBankToDetails(
                    request);

            return Json(result);
        }

        [HttpPost]
        public JsonResult GroupBanksToDetail(
            BankReconciliationGroupBanksToDetailRequest request)
        {
            var result =
                _bankReconciliationService.GroupBanksToDetail(
                    request);

            return Json(result);
        }

        [HttpPost]
        public JsonResult RemoveMatch(
            long reconciliationId,
            long matchId)
        {
            var result =
                _bankReconciliationService.RemoveMatch(
                    reconciliationId,
                    matchId);

            return Json(result);
        }

        [HttpPost]
        public JsonResult AutoMatch(
            long reconciliationId)
        {
            var result =
                _bankReconciliationService.AutoMatch(
                    reconciliationId);

            return Json(result);
        }

        [HttpPost]
        public JsonResult Finalize(
            long reconciliationId)
        {
            var result =
                _bankReconciliationService.Finalize(
                    reconciliationId);

            return Json(result);
        }

        [HttpPost]
        public JsonResult Reopen(
            long reconciliationId)
        {
            var result =
                _bankReconciliationService.Reopen(
                    reconciliationId);

            return Json(result);
        }

        [HttpPost]
        public JsonResult Delete(
            long reconciliationId)
        {
            var result =
                _bankReconciliationService.Delete(
                    reconciliationId);

            return Json(result);
        }


    }
}