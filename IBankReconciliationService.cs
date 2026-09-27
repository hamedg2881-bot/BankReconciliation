using CDS.BIMS.Application.Dto.Financial;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CDS.BIMS.Application.Dto.CDSBase;

namespace CDS.BIMS.Application.ServiceContract.Financial
{
    public interface IBankReconciliationService
    {
        CDSResponse<BankReconciliationDto> Create(
            BankReconciliationCreateRequest request);

        CDSResponse<BankReconciliationDto> Get(
            long id);

        CDSResponse<BankReconciliationPageDto> GetPage(
            long reconciliationId,
            BankReconciliationFilterDto filter,
            Dictionary<string, string> columnFilters);

        CDSResponse<List<BankReconciliationMatchDto>> GetMatches(
            long reconciliationId,
            long bankTransactionId);

        CDSResponse<List<BankReconciliationAutoMatchResultDto>> AutoMatch(
            long reconciliationId);

        CDSResponse<bool> Match(
            BankReconciliationMatchRequest request);

        CDSResponse<bool> GroupBankToDetails(
            BankReconciliationGroupBankToDetailsRequest request);

        CDSResponse<bool> GroupBanksToDetail(
            BankReconciliationGroupBanksToDetailRequest request);

        CDSResponse<bool> RemoveMatch(
            long reconciliationId,
            long matchId);

        CDSResponse<bool> Finalize(
            long reconciliationId);

        CDSResponse<bool> Reopen(
            long reconciliationId);

        CDSResponse<bool> Delete(
            long reconciliationId);
    }
}
