"""
Модуль 2. Создание SQLite БД по схеме, импорт данных из Автопарк.xlsx и
выполнение запросов (в т.ч. процент загрузки автомобиля).

Запуск:
    python module2/import_data.py
"""

from __future__ import annotations

import argparse
import re
import sqlite3
from pathlib import Path

import openpyxl

PROJECT_ROOT = Path(__file__).resolve().parents[1]
DEFAULT_DB_PATH = PROJECT_ROOT / "module2" / "app.db"
SCHEMA_PATH = PROJECT_ROOT / "module2" / "schema.sql"
AUTOPARK_PATH = PROJECT_ROOT / "data" / "customer_docs" / "Автопарк.xlsx"


# Грузоподъемность и объем кузова берутся из паспортных данных моделей
# (нормативная справочная информация по ТТХ грузовиков).
MODEL_SPECS: dict[str, dict[str, float]] = {
    'ГАЗ-33104 "Валдай" (Д-245.7Е2-4L-4,75-117-5М)': {
        "грузоподъемность_кг": 3750,
        "объем_кузова_м3": 15.0,
    },
    "КамАЗ-53212 (ЯМЗ-238Ф-8V-14,86-320-5М)": {
        "грузоподъемность_кг": 10000,
        "объем_кузова_м3": 36.0,
    },
    "КамАЗ-53215N (КамАЗ-740.13-8V-10,85-260-10М)": {
        "грузоподъемность_кг": 11000,
        "объем_кузова_м3": 38.0,
    },
    "МАЗ-53366 (ЯМЗ-238М2-8V-14,86-240-5М)": {
        "грузоподъемность_кг": 8650,
        "объем_кузова_м3": 30.0,
    },
    "МАЗ-63171 (ТМЗ-8421-8V-17,26-360-9М)": {
        "грузоподъемность_кг": 12000,
        "объем_кузова_м3": 45.0,
    },
}


def split_brand_model(full_name: str) -> tuple[str, str]:
    """Разделить строку вида "ГАЗ-33104 ..." на марку и модель.

    Марка - всё до первого пробела (ГАЗ, КамАЗ, МАЗ), остаток - модель.
    """
    parts = full_name.split(" ", 1)
    brand = parts[0]
    model = parts[1] if len(parts) > 1 else ""
    # Унифицируем дефис: "КамАЗ-53212 ..." -> марка "КамАЗ", модель "-53212 ..."
    if "-" in brand:
        brand, rest = brand.split("-", 1)
        model = f"{rest} {model}".strip()
    return brand.strip(), model.strip()


def create_schema(conn: sqlite3.Connection) -> None:
    sql = SCHEMA_PATH.read_text(encoding="utf-8")
    conn.executescript(sql)


def seed_dictionaries(conn: sqlite3.Connection) -> None:
    """Заполняем справочники начальными значениями."""
    cur = conn.cursor()
    for name in ("Администратор", "Логист", "Руководитель", "Пользователь"):
        cur.execute(
            "INSERT OR IGNORE INTO Роли(название) VALUES (?)", (name,)
        )
    for name in ("Логист", "Руководитель", "Водитель", "Технический персонал"):
        cur.execute(
            "INSERT OR IGNORE INTO Должности(название) VALUES (?)", (name,)
        )
    for name in (
        "Исправен",
        "Назначен к доставке",
        "Назначен к ТО",
        "Неисправен",
    ):
        cur.execute(
            "INSERT OR IGNORE INTO Статусы_ТС(название) VALUES (?)", (name,)
        )
    for name in (
        "Создан",
        "Назначен ТС",
        "В пути",
        "Доставлен",
        "Оплачен",
        "Отменен",
    ):
        cur.execute(
            "INSERT OR IGNORE INTO Статусы_заказа(название) VALUES (?)",
            (name,),
        )
    for name in (
        "Москва",
        "Санкт-Петербург",
        "Казань",
        "Уфа",
        "Сочи",
        "Тверь",
        "Ростов",
        "Кострома",
        "Краснодар",
        "Саратов",
    ):
        cur.execute(
            "INSERT OR IGNORE INTO Города(название) VALUES (?)", (name,)
        )
    for name, hours in (
        ("Плановое ТО", 4.0),
        ("Замена масла", 1.5),
        ("Замена шин", 2.0),
        ("Диагностика", 1.0),
        ("Капитальный ремонт", 24.0),
    ):
        cur.execute(
            "INSERT OR IGNORE INTO Виды_работ_ТО(название, норматив_часы) "
            "VALUES (?, ?)",
            (name, hours),
        )
    conn.commit()


def import_autopark(conn: sqlite3.Connection, xlsx_path: Path) -> int:
    """Импортировать данные автопарка из Excel в БД."""
    wb = openpyxl.load_workbook(xlsx_path)
    ws = wb.active
    cur = conn.cursor()
    cur.execute("SELECT id_статуса_тс FROM Статусы_ТС WHERE название = ?", ("Исправен",))
    serviceable_status_id = cur.fetchone()[0]

    imported = 0
    for row in ws.iter_rows(min_row=2, values_only=True):
        full_name, plate = row[0], row[1]
        if not full_name or not plate:
            continue
        brand, model = split_brand_model(str(full_name))
        specs = MODEL_SPECS.get(str(full_name))
        if not specs:
            raise RuntimeError(
                f"Не найдены ТТХ для модели '{full_name}'. "
                "Дополните MODEL_SPECS."
            )

        cur.execute(
            "INSERT OR IGNORE INTO Марки_ТС(название) VALUES (?)", (brand,)
        )
        cur.execute(
            "SELECT id_марки FROM Марки_ТС WHERE название = ?", (brand,)
        )
        brand_id = cur.fetchone()[0]

        cur.execute(
            "INSERT OR IGNORE INTO Модели_ТС"
            "(id_марки, название, грузоподъемность_кг, объем_кузова_м3) "
            "VALUES (?, ?, ?, ?)",
            (brand_id, model, specs["грузоподъемность_кг"], specs["объем_кузова_м3"]),
        )
        cur.execute(
            "SELECT id_модели FROM Модели_ТС WHERE id_марки = ? AND название = ?",
            (brand_id, model),
        )
        model_id = cur.fetchone()[0]

        cur.execute(
            "INSERT OR IGNORE INTO Транспортные_средства"
            "(id_модели, гос_номер, id_статуса_тс) VALUES (?, ?, ?)",
            (model_id, str(plate).strip(), serviceable_status_id),
        )
        if cur.rowcount:
            imported += 1
    conn.commit()
    return imported


def seed_sample_orders(conn: sqlite3.Connection) -> None:
    """Заполняем демо-заказы и маршрутные листы для проверки запроса."""
    cur = conn.cursor()

    cur.execute("SELECT COUNT(*) FROM Клиенты")
    if cur.fetchone()[0] > 0:
        return

    cur.execute(
        "INSERT INTO Сотрудники(фамилия, имя, отчество, id_должности, "
        "дата_приема) "
        "VALUES (?, ?, ?, (SELECT id_должности FROM Должности WHERE название = ?), ?)",
        ("Иванов", "Иван", "Иванович", "Логист", "2023-01-15"),
    )
    logist_id = cur.lastrowid

    drivers: list[int] = []
    for last, first, mid in (
        ("Петров", "Петр", "Петрович"),
        ("Сидоров", "Сидор", "Сидорович"),
        ("Кузнецов", "Алексей", "Викторович"),
    ):
        cur.execute(
            "INSERT INTO Сотрудники(фамилия, имя, отчество, id_должности, "
            "дата_приема) "
            "VALUES (?, ?, ?, (SELECT id_должности FROM Должности WHERE название = ?), ?)",
            (last, first, mid, "Водитель", "2023-03-01"),
        )
        drivers.append(cur.lastrowid)

    clients: list[int] = []
    for last, first, mid, ser, num in (
        ("Александров", "Петр", "Константинович", "4578", "234001"),
        ("Бахшиев", "Павел", "Иннокентьевич", "2487", "198002"),
        ("Мазалова", "Ольга", "Николаевна", "4578", "247003"),
    ):
        cur.execute(
            "INSERT INTO Клиенты(фамилия, имя, отчество, серия_паспорта, номер_паспорта) "
            "VALUES (?, ?, ?, ?, ?)",
            (last, first, mid, ser, num),
        )
        clients.append(cur.lastrowid)

    cur.execute("SELECT id_города FROM Города WHERE название = ?", ("Москва",))
    msk = cur.fetchone()[0]
    cur.execute(
        "SELECT id_города FROM Города WHERE название = ?", ("Санкт-Петербург",)
    )
    spb = cur.fetchone()[0]
    cur.execute("SELECT id_города FROM Города WHERE название = ?", ("Казань",))
    kzn = cur.fetchone()[0]

    cur.execute(
        "SELECT id_статуса_заказа FROM Статусы_заказа WHERE название = ?",
        ("Назначен ТС",),
    )
    status_assigned = cur.fetchone()[0]

    cur.execute(
        "SELECT id_тс FROM Транспортные_средства WHERE гос_номер = ?",
        ("м324ст797",),
    )
    tc_valdai = cur.fetchone()[0]
    cur.execute(
        "SELECT id_тс FROM Транспортные_средства WHERE гос_номер = ?",
        ("е592оу777",),
    )
    tc_kamaz_53212 = cur.fetchone()[0]
    cur.execute(
        "SELECT id_тс FROM Транспортные_средства WHERE гос_номер = ?",
        ("е337ео777",),
    )
    tc_maz_53366 = cur.fetchone()[0]

    # Заказы (одно ТС может быть назначено на несколько заказов).
    orders = [
        ("Заказ_4578_234", clients[0], msk, spb, 1500, 30000, tc_valdai),
        ("Заказ_4578_235", clients[0], msk, kzn, 1200, 28000, tc_valdai),
        ("Заказ_2487_198", clients[1], spb, msk, 8000, 60000, tc_kamaz_53212),
        ("Заказ_4578_247", clients[2], kzn, msk, 4000, 40000, tc_maz_53366),
        ("Заказ_4578_248", clients[2], msk, spb, 3500, 35000, tc_maz_53366),
    ]
    for num, cid, src, dst, weight, price, tc_id in orders:
        cur.execute(
            "INSERT INTO Заказы"
            "(номер_заказа, id_клиента, id_логиста, id_города_отправления, "
            "id_города_назначения, вес_груза_кг, стоимость, id_статуса_заказа) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, ?)",
            (num, cid, logist_id, src, dst, weight, price, status_assigned),
        )
        order_id = cur.lastrowid
        cur.execute(
            "INSERT INTO Маршрутные_листы(id_заказа, id_тс, id_водителя) "
            "VALUES (?, ?, ?)",
            (order_id, tc_id, drivers[0]),
        )

    conn.commit()


def report_loading_percentage(conn: sqlite3.Connection) -> None:
    """Запрос: процент загрузки автомобиля.

    Загрузка = SUM(вес_груза_кг по назначенным заказам) / грузоподъемность * 100
    """
    sql = (
        "SELECT\n"
        "    тс.id_тс,\n"
        "    тс.гос_номер,\n"
        "    марк.название || ' ' || мод.название AS марка_модель,\n"
        "    мод.грузоподъемность_кг AS вместимость_кг,\n"
        "    COALESCE(SUM(з.вес_груза_кг), 0) AS загружено_кг,\n"
        "    ROUND(\n"
        "        COALESCE(SUM(з.вес_груза_кг), 0) * 100.0 /\n"
        "        мод.грузоподъемность_кг,\n"
        "        2\n"
        "    ) AS процент_загрузки\n"
        "FROM Транспортные_средства тс\n"
        "JOIN Модели_ТС мод ON мод.id_модели = тс.id_модели\n"
        "JOIN Марки_ТС марк ON марк.id_марки = мод.id_марки\n"
        "LEFT JOIN Маршрутные_листы мл ON мл.id_тс = тс.id_тс\n"
        "LEFT JOIN Заказы з ON з.id_заказа = мл.id_заказа\n"
        "GROUP BY тс.id_тс, тс.гос_номер, марк.название, мод.название, "
        "мод.грузоподъемность_кг\n"
        "ORDER BY процент_загрузки DESC, тс.гос_номер;\n"
    )
    print("\n=== Процент загрузки автомобиля ===")
    print(sql)
    cur = conn.execute(sql)
    rows = cur.fetchall()
    header = (
        "id_ТС",
        "Гос. номер",
        "Марка / модель",
        "Вместимость, кг",
        "Загружено, кг",
        "Загрузка, %",
    )
    print("{:>5} | {:>11} | {:<60} | {:>15} | {:>13} | {:>11}".format(*header))
    print("-" * 130)
    for row in rows:
        print(
            "{:>5} | {:>11} | {:<60} | {:>15} | {:>13} | {:>11}".format(
                *[str(c) if c is not None else "" for c in row]
            )
        )


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--db",
        type=Path,
        default=DEFAULT_DB_PATH,
        help="Путь к файлу БД SQLite",
    )
    parser.add_argument(
        "--xlsx",
        type=Path,
        default=AUTOPARK_PATH,
        help="Путь к Автопарк.xlsx",
    )
    parser.add_argument(
        "--reset",
        action="store_true",
        help="Удалить существующую БД перед созданием",
    )
    args = parser.parse_args()

    if args.reset and args.db.exists():
        args.db.unlink()

    with sqlite3.connect(args.db) as conn:
        conn.execute("PRAGMA foreign_keys = ON")
        create_schema(conn)
        seed_dictionaries(conn)
        imported = import_autopark(conn, args.xlsx)
        print(f"Импортировано в Транспортные_средства: {imported} записей")
        seed_sample_orders(conn)
        report_loading_percentage(conn)


if __name__ == "__main__":
    main()
