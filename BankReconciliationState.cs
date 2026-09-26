using System.ComponentModel;

namespace CDS.BIMS.Domain.Model.Enum.Financial
{
    public enum BankReconciliationState
    {
        [Description("باز")]
        Open = 0,
        [Description("نهایی")]
        Finalized = 1
    }
}