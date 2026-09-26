using System.ComponentModel;

namespace CDS.BIMS.Domain.Model.Enum.Financial
{
    public enum BankReconciliationMatchType
    {
        [Description("دستی")]
        Manual = 1,
        [Description("خودکار")]
        Automatic = 2,
        [Description("گروهی")]
        Group = 3
    }
}