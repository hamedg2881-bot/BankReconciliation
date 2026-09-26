using CDS.BIMS.Application.Dto.Financial;
using CDS.BIMS.Presentation.WebUI.ActionFilter;
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

        [HttpPost]
        public JsonResult GetPage(
            long reconciliationId,
            BankReconciliationFilterDto filter)
        {
            var result =
                _bankReconciliationService.GetPage(
                    reconciliationId,
                    filter);

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
            BankReconciliationCandidateFilterDto filter)
        {
            var result =
                _candidateService.GetCandidates(
                    reconciliationId,
                    bankTransactionId,
                    filter);

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