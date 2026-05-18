using System;
using System.Collections.Generic;
using System.Linq;
using Module4.Domain.Models;

namespace Module4.Domain.Services;

/// <summary>
/// Валидатор серии и номера паспорта гражданина РФ.
/// Проверяет два самостоятельных критерия (см. ТестКейс.docx):
/// <list type="number">
///   <item><description>
///     <b>Формат</b>: серия — ровно 4 цифры, номер — ровно 6 цифр;
///     обе части состоят только из цифр.
///   </description></item>
///   <item><description>
///     <b>Содержательная корректность</b>: серия и номер не должны
///     состоять из одних нулей (фиктивные данные клиента).
///   </description></item>
/// </list>
/// </summary>
public static class PassportValidator
{
    /// <summary>Длина серии паспорта РФ.</summary>
    public const int SeriesLength = 4;

    /// <summary>Длина номера паспорта РФ.</summary>
    public const int NumberLength = 6;

    /// <summary>
    /// Полная проверка паспорта. Возвращает результат с признаком корректности,
    /// сообщением для UI и списком сработавших причин.
    /// </summary>
    public static ValidationOutcome Validate(PassportData passport)
    {
        ArgumentNullException.ThrowIfNull(passport);

        var reasons = new List<string>();

        var series = passport.Series?.Trim() ?? string.Empty;
        var number = passport.Number?.Trim() ?? string.Empty;

        if (series.Length == 0)
            reasons.Add("Серия паспорта не передана.");
        else if (series.Length != SeriesLength)
            reasons.Add($"Серия должна содержать ровно {SeriesLength} цифры.");
        else if (!series.All(char.IsDigit))
            reasons.Add("Серия должна состоять только из цифр.");

        if (number.Length == 0)
            reasons.Add("Номер паспорта не передан.");
        else if (number.Length != NumberLength)
            reasons.Add($"Номер должен содержать ровно {NumberLength} цифр.");
        else if (!number.All(char.IsDigit))
            reasons.Add("Номер должен состоять только из цифр.");

        if (reasons.Count == 0
            && long.TryParse(series, out var s)
            && long.TryParse(number, out var n)
            && s == 0
            && n == 0)
        {
            reasons.Add("Серия и номер не могут одновременно состоять из нулей.");
        }

        return reasons.Count == 0
            ? ValidationOutcome.Valid()
            : ValidationOutcome.Invalid(reasons.ToArray());
    }
}
