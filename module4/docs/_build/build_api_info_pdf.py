#!/usr/bin/env python3
"""Сборка api_info.pdf — описание методов эмулятора отправки данных.

Запуск::

    python module4/docs/_build/build_api_info_pdf.py

Результат: ``module4/docs/api_info.pdf``.
"""

from __future__ import annotations

from pathlib import Path

from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.lib.enums import TA_LEFT
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    SimpleDocTemplate,
    Paragraph,
    Spacer,
    Preformatted,
    Table,
    TableStyle,
)
from reportlab.lib import colors

OUT_PATH = Path(__file__).resolve().parents[1] / "api_info.pdf"


def _register_fonts() -> str:
    candidates = [
        ("DejaVuSans", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
        ("DejaVuSans-Bold", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
        ("DejaVuSansMono", "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"),
    ]
    for name, path in candidates:
        if Path(path).exists():
            pdfmetrics.registerFont(TTFont(name, path))
    return "DejaVuSans" if Path(candidates[0][1]).exists() else "Helvetica"


def build() -> Path:
    base_font = _register_fonts()
    mono_font = "DejaVuSansMono" if Path(
        "/usr/share/fonts/truetype/dejavu/DejaVuSansMono.ttf"
    ).exists() else "Courier"

    styles = getSampleStyleSheet()
    title_style = ParagraphStyle(
        "Title", parent=styles["Title"],
        fontName=base_font, fontSize=18, leading=22,
        spaceAfter=10, alignment=TA_LEFT,
    )
    body_style = ParagraphStyle(
        "Body", parent=styles["BodyText"],
        fontName=base_font, fontSize=11, leading=14,
        spaceAfter=8,
    )
    h2_style = ParagraphStyle(
        "H2", parent=styles["Heading2"],
        fontName=base_font, fontSize=13, leading=16,
        spaceBefore=12, spaceAfter=6,
    )
    code_style = ParagraphStyle(
        "Code", parent=styles["Code"],
        fontName=mono_font, fontSize=9.5, leading=12,
    )

    doc = SimpleDocTemplate(
        str(OUT_PATH), pagesize=A4,
        leftMargin=18 * mm, rightMargin=18 * mm,
        topMargin=18 * mm, bottomMargin=18 * mm,
        title="api_info", author="Module 4",
    )

    story = []
    story.append(Paragraph(
        "API эмулятора отправки данных (Модуль 4, демо-экзамен 2025)",
        title_style,
    ))
    story.append(Paragraph(
        "Эмулятор реализован в файле <b>module4/api/server.py</b>. Запуск:",
        body_style,
    ))
    story.append(Preformatted(
        "python module4/api/server.py --host 127.0.0.1 --port 5000",
        code_style,
    ))
    story.append(Paragraph(
        "Базовый URL: <b>http://127.0.0.1:5000/</b>. Формат обмена — JSON, "
        "кодировка UTF-8. Эмулятор выдаёт паспорта по кругу из файла "
        "<i>module4/api/passports.json</i>.",
        body_style,
    ))

    story.append(Paragraph("Таблица методов", h2_style))
    table_data = [
        ["Метод", "Путь", "Описание"],
        ["GET", "/healthz", "Проверка работоспособности сервиса"],
        ["GET", "/get_data", "Получение очередного паспорта"],
        ["POST", "/set_result", "Приём результата валидации"],
    ]
    table = Table(table_data, colWidths=[20 * mm, 35 * mm, 110 * mm])
    table.setStyle(TableStyle([
        ("FONT", (0, 0), (-1, -1), base_font, 10),
        ("FONT", (0, 0), (-1, 0), base_font, 10),
        ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#E2E8F0")),
        ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#475569")),
        ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
        ("LEFTPADDING", (0, 0), (-1, -1), 4),
        ("RIGHTPADDING", (0, 0), (-1, -1), 4),
        ("TOPPADDING", (0, 0), (-1, -1), 4),
        ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
    ]))
    story.append(table)

    story.append(Paragraph("GET /get_data", h2_style))
    story.append(Paragraph(
        "Возвращает очередную запись паспорта. Параметров запроса нет.",
        body_style,
    ))
    story.append(Paragraph("Пример ответа (HTTP 200):", body_style))
    story.append(Preformatted(
        '{\n'
        '  "series": "4509",\n'
        '  "number": "638172",\n'
        '  "issued_at": "2014-03-12",\n'
        '  "comment": "Корректный паспорт РФ"\n'
        '}',
        code_style,
    ))

    story.append(Paragraph("POST /set_result", h2_style))
    story.append(Paragraph(
        "Принимает результат проверки данных. Тело запроса — JSON-объект:",
        body_style,
    ))
    story.append(Preformatted(
        '{\n'
        '  "series": "4509",\n'
        '  "number": "638172",\n'
        '  "is_valid": true,\n'
        '  "message": "Корректный паспорт",\n'
        '  "reasons": []\n'
        '}',
        code_style,
    ))
    story.append(Paragraph(
        "В случае ошибочных данных <i>is_valid=false</i>, в <i>reasons</i> "
        "перечисляются сработавшие правила валидатора.",
        body_style,
    ))
    story.append(Paragraph("Пример ответа (HTTP 200):", body_style))
    story.append(Preformatted('{ "status": "accepted" }', code_style))

    story.append(Paragraph("Коды ошибок", h2_style))
    story.append(Paragraph(
        "HTTP 400 — тело запроса не является корректным JSON; "
        "HTTP 404 — запрошен несуществующий маршрут. "
        "Сетевые ошибки и таймауты обрабатываются на стороне WPF-клиента "
        "(см. Module4.App / MainWindow).",
        body_style,
    ))

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    doc.build(story)
    return OUT_PATH


if __name__ == "__main__":
    print(f"OK -> {build()}")
