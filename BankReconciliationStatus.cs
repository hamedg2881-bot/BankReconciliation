using System.ComponentModel;

namespace CDS.BIMS.Domain.Model.Enum.Financial
{
    public enum BankReconciliationStatus
    {
        [Description("عدم تطبیق")]
        Unmatched = 0,
        [Description("نیاز به بررسی")]
        Partial = 1,
        [Description("تطبیق شده")]
        Matched = 2
    }
}