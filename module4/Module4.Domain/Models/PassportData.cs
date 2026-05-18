using System;

namespace Module4.Domain.Models;

/// <summary>
/// Сведения о паспорте, полученные от эмулятора отправки данных.
/// </summary>
/// <param name="Series">Серия паспорта в исходном виде, как пришла от клиента.</param>
/// <param name="Number">Номер паспорта в исходном виде.</param>
/// <param name="IssuedAt">Дата выдачи (если передана клиентом).</param>
/// <param name="Comment">Комментарий клиента к записи (для отладки / диагностики).</param>
public sealed record PassportData(
    string Series,
    string Number,
    DateOnly? IssuedAt = null,
    string? Comment = null)
{
    /// <summary>Возвращает текст вида «СССС НННННН» либо «СССС-НННННН» при пустых полях.</summary>
    public string FormattedSeriesAndNumber => $"{Series} {Number}".Trim();
}
