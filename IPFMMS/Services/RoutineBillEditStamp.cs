using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IPFMMS.Models;
namespace IPFMMS.Services;
public static class RoutineBillEditStamp
{
    public static string For(RoutineBill bill) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new object?[]
    {
        bill.billId,bill.userId,bill.expenseCategoryId,bill.billName,bill.billType,
        bill.amount.ToString("0.00",CultureInfo.InvariantCulture),bill.dueDate.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
        bill.frequency,bill.paymentMethod,bill.reminderPeriod,bill.description,bill.status,bill.createdDate.Ticks,bill.recurrenceAnchorDay
    }))));
}
