using CDS.BIMS.Application.Dto.CDSBase;
using CDS.BIMS.Application.Dto.Financial;
using System.Collections.Generic;

namespace CDS.BIMS.Application.ServiceContract.Financial
{
    public interface IBankReconciliationCandidateService
    {
        CDSResponse<BankReconciliationCandidateResultDto> GetCandidates(
            long reconciliationId,
            long bankTransactionId,
            BankReconciliationCandidateFilterDto filter);
    }
}