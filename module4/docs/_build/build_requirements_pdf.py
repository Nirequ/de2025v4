#!/usr/bin/env python3
"""Сборка «Требования к разработке.pdf» — единый набор требований к UI/UX
авторизационного приложения (модуль 3) и приложения валидации данных
(модуль 4) демо-экзамена 2025.

Запуск::

    python module4/docs/_build/build_requirements_pdf.py
"""

from __future__ import annotations

from pathlib import Path

from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.units import mm
from reportlab.lib.enums import TA_LEFT, TA_JUSTIFY
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import SimpleDocTemplate, Paragraph, Spacer

OUT_PATH = Path(__file__).resolve().parents[1] / "Требования к разработке.pdf"


def _register_fonts() -> str:
    candidates = [
        ("DejaVuSans", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"),
        ("DejaVuSans-Bold", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"),
    ]
    found = False
    for name, path in candidates:
        if Path(path).exists():
            pdfmetrics.registerFont(TTFont(name, path))
            found = True
    return "DejaVuSans" if found else "Helvetica"


def build() -> Path:
    base_font = _register_fonts()
    styles = getSampleStyleSheet()
    title_style = ParagraphStyle(
        "Title", parent=styles["Title"],
        fontName=base_font, fontSize=18, leading=22,
        spaceAfter=12, alignment=TA_LEFT,
    )
    body_style = ParagraphStyle(
        "Body", parent=styles["BodyText"],
        fontName=base_font, fontSize=11, leading=14,
        spaceAfter=6, alignment=TA_JUSTIFY,
    )
    h2_style = ParagraphStyle(
        "H2", parent=styles["Heading2"],
        fontName=base_font, fontSize=13, leading=16,
        spaceBefore=12, spaceAfter=6,
    )
    bullet_style = ParagraphStyle(
        "Bullet", parent=body_style,
        leftIndent=14, bulletIndent=2, spaceAfter=4,
    )

    doc = SimpleDocTemplate(
        str(OUT_PATH), pagesize=A4,
        leftMargin=18 * mm, rightMargin=18 * mm,
        topMargin=18 * mm, bottomMargin=18 * mm,
        title="Требования к разработке",
        author="demo-exam-2025-v4",
    )
    story = [Paragraph("Требования к разработке (демо-экзамен 2025, вариант 4)", title_style)]

    story.append(Paragraph("1. Общие положения", h2_style))
    for text in (
        "Приложения модулей 3 и 4 разрабатываются на платформе .NET 8 с использованием технологии WPF.",
        "Базовый язык интерфейса — русский. Кодировка исходных файлов и текстовых ресурсов — UTF-8.",
        "Каждое приложение должно собираться без предупреждений с уровнем <i>WarningLevel=4</i> и проходить автоматические тесты xUnit.",
    ):
        story.append(Paragraph("• " + text, bullet_style))

    story.append(Paragraph("2. Цветовая палитра", h2_style))
    for text in (
        "Фон приложения: <b>#F4F6FB</b>. Карточки и поля ввода: <b>#FFFFFF</b>, граница <b>#D6DCE5</b>.",
        "Основной текст: <b>#1F2937</b>. Подсказки и второстепенные подписи: <b>#6B7280</b>.",
        "Акцентный цвет (кнопки «Войти», «Получить данные»): <b>#2F6FED</b>, наведение — <b>#1F4DB8</b>.",
        "Сообщение об успехе: <b>#2F855A</b>. Сообщение об ошибке: <b>#C0392B</b>.",
    ):
        story.append(Paragraph("• " + text, bullet_style))

    story.append(Paragraph("3. Типографика", h2_style))
    for text in (
        "Шрифт интерфейса — Segoe UI; основной размер кегля 13 пт, заголовки экранов — 22 пт SemiBold.",
        "Подписи полей оформляются 12 пт SemiBold цветом <b>#6B7280</b>.",
        "Сообщения о результате выводятся 13 пт SemiBold цветом успеха или ошибки.",
    ):
        story.append(Paragraph("• " + text, bullet_style))

    story.append(Paragraph("4. Раскладка экранов", h2_style))
    for text in (
        "Окна центрируются на экране (<i>WindowStartupLocation=CenterScreen</i>).",
        "Внутренние отступы карточки — 24 px; интервалы между полями — 12 px.",
        "Кнопки имеют скруглённые углы (CornerRadius=6) и минимальную высоту 40 px.",
        "Для модальных окон ширина окна не превышает 480 px; для рабочих столов — 720–960 px.",
    ):
        story.append(Paragraph("• " + text, bullet_style))

    story.append(Paragraph("5. Обработка ошибок и сообщений", h2_style))
    for text in (
        "Тексты сообщений должны соответствовать заданию экзамена дословно "
        "(«Вы ввели неверный логин или пароль…», «Вы заблокированы…», "
        "«Корректный паспорт», «Не корректный серия и номер паспорта» и т. д.).",
        "Любое исключение приложения отображается пользователю без стека вызовов; "
        "технические детали попадают в лог.",
        "Поля ввода с ошибками подсвечиваются красной рамкой и не блокируют повторный ввод.",
    ):
        story.append(Paragraph("• " + text, bullet_style))

    story.append(Paragraph("6. Доступность и удобство", h2_style))
    for text in (
        "Все формы должны поддерживать управление с клавиатуры (Tab, Enter).",
        "Кнопка «Войти» должна срабатывать по нажатию Enter в любом из полей формы.",
        "Не использовать модальные окна там, где можно ограничиться сообщением рядом с полем.",
    ):
        story.append(Paragraph("• " + text, bullet_style))

    story.append(Paragraph("7. Хранение данных и конфигурация", h2_style))
    for text in (
        "Локальная база данных — SQLite (файл <i>module2/app.db</i>). Доступ через <i>Microsoft.Data.Sqlite</i>.",
        "Хранение паролей — алгоритм PBKDF2-HMAC-SHA256, 200 000 итераций, 16-байтовая соль.",
        "Адрес API эмулятора модуля 4 задаётся переменной окружения <b>PASSPORT_API_URL</b> "
        "(значение по умолчанию — <i>http://127.0.0.1:5000/</i>).",
    ):
        story.append(Paragraph("• " + text, bullet_style))

    story.append(Spacer(0, 6))
    story.append(Paragraph(
        "Документ оформлен автоматически на основании макетов module3/UI и module4.png; "
        "при расхождении с эталонными требованиями преимущество имеют исходные документы заказчика.",
        body_style,
    ))

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    doc.build(story)
    return OUT_PATH


if __name__ == "__main__":
    print(f"OK -> {build()}")
