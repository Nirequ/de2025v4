# Демо-экзамен 2025 — вариант 4

Учебный проект на тему «Информационная система транспортной компании».
Состоит из четырёх модулей. Бэкенд и десктопные приложения написаны
на **C# / WPF (.NET 8)**; вспомогательные генераторы документации и
эмулятор отправки данных — на **Python 3.11+**.

| Модуль | Содержимое | Технологии |
| ------ | ---------- | ---------- |
| [`module1/`](module1/) | ER-диаграмма в 3НФ, экспорт в PDF | Python (graphviz + reportlab) |
| [`module2/`](module2/) | Схема БД, импорт данных автопарка, запрос процента загрузки | SQLite + Python (для импорта) |
| [`module3/`](module3/) | WPF-приложение авторизации с ролями и блокировками | C# 12 / WPF / xUnit |
| [`module4/`](module4/) | WPF-приложение валидации паспортных данных + эмулятор API | C# 12 / WPF / xUnit + Python (HTTP сервер) |

## Требования

* **.NET 8 SDK** — `dotnet --version` ≥ 8.0.
* **Python 3.11+** с `python-docx`, `reportlab` (только для пересборки PDF/DOCX).
* **SQLite 3.40+** (используется через `Microsoft.Data.Sqlite`).
* Сборка WPF-проектов выполняется на Windows либо на Linux с включённой
  опцией `EnableWindowsTargeting` (уже прописана в *.csproj* модулей 3 и 4).

## Быстрый старт

```bash
# Сборка домена + тестов (кроссплатформенно)
dotnet build module3/Module3.sln
dotnet build module4/Module4.sln
dotnet test  module3/Module3.Tests/Module3.Tests.csproj
dotnet test  module4/Module4.Tests/Module4.Tests.csproj

# Запуск эмулятора отправки данных (модуль 4)
python module4/api/server.py --host 127.0.0.1 --port 5000

# Запуск WPF-приложений (Windows)
dotnet run --project module3/Module3.App/Module3.App.csproj
dotnet run --project module4/Module4.App/Module4.App.csproj
```

## Документация

* [`module3/docs/DOCUMENTATION.md`](module3/docs/DOCUMENTATION.md) — функциональное описание модуля авторизации.
* [`module4/docs/api_info.pdf`](module4/docs/api_info.pdf) — описание методов эмулятора.
* [`module4/docs/ТестКейс.docx`](module4/docs/ТестКейс.docx) — заполненный тест-кейс валидации (графа «Результат» — закладки).
* [`module4/docs/Требования к разработке.pdf`](module4/docs/Требования%20к%20разработке.pdf) — единые UI/UX требования для модулей 3 и 4.

## Учётные данные по умолчанию (Модуль 3)

| Логин   | Пароль        | Роль          |
| ------- | ------------- | ------------- |
| `admin` | `Admin#2025`  | Администратор |

После первого входа приложение потребует сменить пароль. Учётная запись
блокируется на 3-й подряд неверной попытке логина и при отсутствии
авторизации более 30 дней. Снять блокировку можно из панели администратора.
