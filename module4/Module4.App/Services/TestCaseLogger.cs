using System;
using System.Globalization;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Module4.Domain.Models;

namespace Module4.App.Services;

/// <summary>
/// Записывает результат валидации в столбец «Результат» документа
/// <c>ТестКейс.docx</c> (по закладкам <c>Результат1..N</c>) и в текстовый
/// журнал <c>results.log</c>. По требованиям модуля 4 каждое срабатывание
/// валидатора должно фиксироваться в соответствующей строке таблицы.
/// </summary>
public sealed class TestCaseLogger
{
    private readonly string _docxPath;
    private readonly string _logPath;
    private readonly object _lock = new();
    private int _bookmarkIndex;

    public TestCaseLogger(string docxPath, string logPath)
    {
        _docxPath = docxPath;
        _logPath = logPath;
    }

    public static TestCaseLogger Default
    {
        get
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var module4Root = ResolveModule4Root(appDir);
            var docsDir = Path.Combine(module4Root, "docs");
            Directory.CreateDirectory(docsDir);
            return new TestCaseLogger(
                Path.Combine(docsDir, "ТестКейс.docx"),
                Path.Combine(docsDir, "results.log"));
        }
    }

    private static string ResolveModule4Root(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            if (string.Equals(dir.Name, "module4", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }
        return startDir;
    }

    /// <summary>
    /// Дописывает в журнал и в ближайшую свободную закладку документа
    /// результат проверки. Сначала пытается заполнить закладку
    /// «Результат{index}», начиная с 1; если ни одна не найдена либо все
    /// заняты — добавляет новую строку в таблицу.
    /// </summary>
    public void Append(PassportData passport, ValidationOutcome outcome)
    {
        lock (_lock)
        {
            AppendLog(passport, outcome);
            if (File.Exists(_docxPath))
                AppendDocx(passport, outcome);
        }
    }

    private void AppendLog(PassportData passport, ValidationOutcome outcome)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var line = string.Format(
            CultureInfo.InvariantCulture,
            "{0}\t{1} {2}\tIsValid={3}\tMessage=\"{4}\"\tReasons=[{5}]",
            stamp,
            passport.Series,
            passport.Number,
            outcome.IsValid,
            outcome.Message,
            string.Join("; ", outcome.Reasons));
        File.AppendAllText(_logPath, line + Environment.NewLine);
    }

    private void AppendDocx(PassportData passport, ValidationOutcome outcome)
    {
        using var doc = WordprocessingDocument.Open(_docxPath, isEditable: true);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return;

        var text = outcome.IsValid
            ? $"{outcome.Message} ({passport.Series} {passport.Number})"
            : $"{outcome.Message}: {string.Join("; ", outcome.Reasons)} ({passport.Series} {passport.Number})";

        _bookmarkIndex++;
        var bookmarkName = $"Результат{_bookmarkIndex}";
        if (!FillBookmark(body, bookmarkName, text))
        {
            FillBookmark(body, "Результат1", text);
        }
        doc.MainDocumentPart!.Document.Save();
    }

    private static bool FillBookmark(Body body, string bookmarkName, string text)
    {
        var start = body
            .Descendants<BookmarkStart>()
            .FirstOrDefault(b => b.Name == bookmarkName);
        if (start is null) return false;

        var cell = start.Ancestors<TableCell>().FirstOrDefault();
        if (cell is null) return false;

        foreach (var p in cell.Elements<Paragraph>().ToList())
            p.Remove();

        var paragraph = new Paragraph(new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
        cell.AppendChild(paragraph);
        return true;
    }
}
