using System.Collections.Generic;
using System.Linq;

namespace Module4.Domain.Models;

/// <summary>
/// Результат проверки паспортных данных. Содержит итоговое сообщение в стиле
/// макета (например, «Корректный паспорт» или «Не корректный серия и номер паспорта»)
/// и список сработавших причин для журнала / тест-кейса.
/// </summary>
public sealed record ValidationOutcome(
    bool IsValid,
    string Message,
    IReadOnlyList<string> Reasons)
{
    public static readonly string ValidMessage = "Корректный паспорт";
    public static readonly string InvalidMessage = "Не корректный серия и номер паспорта";

    public static ValidationOutcome Valid() =>
        new(true, ValidMessage, new List<string>(0));

    public static ValidationOutcome Invalid(params string[] reasons) =>
        new(false, InvalidMessage, reasons.ToList());
}
