-- =====================================================================
-- Модуль 2. Схема базы данных транспортной компании (3НФ).
-- СУБД: SQLite (foreign_keys = ON, ссылочная целостность включена).
-- =====================================================================

PRAGMA foreign_keys = ON;

-- ===================== Справочники =====================

CREATE TABLE IF NOT EXISTS Роли (
    id_роли           INTEGER PRIMARY KEY AUTOINCREMENT,
    название          TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Должности (
    id_должности      INTEGER PRIMARY KEY AUTOINCREMENT,
    название          TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Города (
    id_города         INTEGER PRIMARY KEY AUTOINCREMENT,
    название          TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Марки_ТС (
    id_марки          INTEGER PRIMARY KEY AUTOINCREMENT,
    название          TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Модели_ТС (
    id_модели         INTEGER PRIMARY KEY AUTOINCREMENT,
    id_марки          INTEGER NOT NULL,
    название          TEXT    NOT NULL,
    грузоподъемность_кг INTEGER NOT NULL CHECK (грузоподъемность_кг > 0),
    объем_кузова_м3   REAL,
    UNIQUE (id_марки, название),
    FOREIGN KEY (id_марки) REFERENCES Марки_ТС(id_марки) ON UPDATE CASCADE ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Статусы_ТС (
    id_статуса_тс     INTEGER PRIMARY KEY AUTOINCREMENT,
    название          TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Статусы_заказа (
    id_статуса_заказа INTEGER PRIMARY KEY AUTOINCREMENT,
    название          TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Виды_работ_ТО (
    id_вида_работ     INTEGER PRIMARY KEY AUTOINCREMENT,
    название          TEXT    NOT NULL UNIQUE,
    норматив_часы     REAL    NOT NULL CHECK (норматив_часы > 0)
);

-- ===================== Сущности =====================

CREATE TABLE IF NOT EXISTS Сотрудники (
    id_сотрудника     INTEGER PRIMARY KEY AUTOINCREMENT,
    фамилия           TEXT    NOT NULL,
    имя               TEXT    NOT NULL,
    отчество          TEXT,
    id_должности      INTEGER NOT NULL,
    телефон           TEXT,
    email             TEXT,
    дата_приема       DATE    NOT NULL,
    FOREIGN KEY (id_должности) REFERENCES Должности(id_должности) ON UPDATE CASCADE ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Клиенты (
    id_клиента        INTEGER PRIMARY KEY AUTOINCREMENT,
    фамилия           TEXT    NOT NULL,
    имя               TEXT    NOT NULL,
    отчество          TEXT,
    серия_паспорта    TEXT    NOT NULL CHECK (length(серия_паспорта) = 4),
    номер_паспорта    TEXT    NOT NULL CHECK (length(номер_паспорта) = 6),
    телефон           TEXT,
    email             TEXT,
    UNIQUE (серия_паспорта, номер_паспорта)
);

CREATE TABLE IF NOT EXISTS Пользователи (
    id_пользователя           INTEGER PRIMARY KEY AUTOINCREMENT,
    логин                     TEXT    NOT NULL UNIQUE,
    пароль_хеш                TEXT    NOT NULL,
    id_роли                   INTEGER NOT NULL,
    id_сотрудника             INTEGER UNIQUE,
    дата_создания             DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    дата_последнего_входа     DATETIME,
    число_неудачных_попыток   INTEGER NOT NULL DEFAULT 0 CHECK (число_неудачных_попыток >= 0),
    признак_блокировки        INTEGER NOT NULL DEFAULT 0 CHECK (признак_блокировки IN (0,1)),
    признак_первого_входа     INTEGER NOT NULL DEFAULT 1 CHECK (признак_первого_входа IN (0,1)),
    FOREIGN KEY (id_роли)       REFERENCES Роли(id_роли)             ON UPDATE CASCADE ON DELETE RESTRICT,
    FOREIGN KEY (id_сотрудника) REFERENCES Сотрудники(id_сотрудника) ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS Транспортные_средства (
    id_тс             INTEGER PRIMARY KEY AUTOINCREMENT,
    id_модели         INTEGER NOT NULL,
    гос_номер         TEXT    NOT NULL UNIQUE,
    vin               TEXT,
    год_выпуска       INTEGER CHECK (год_выпуска IS NULL OR год_выпуска BETWEEN 1980 AND 2100),
    id_статуса_тс     INTEGER NOT NULL,
    FOREIGN KEY (id_модели)     REFERENCES Модели_ТС(id_модели)     ON UPDATE CASCADE ON DELETE RESTRICT,
    FOREIGN KEY (id_статуса_тс) REFERENCES Статусы_ТС(id_статуса_тс) ON UPDATE CASCADE ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS Заказы (
    id_заказа                 INTEGER PRIMARY KEY AUTOINCREMENT,
    номер_заказа              TEXT    NOT NULL UNIQUE,
    id_клиента                INTEGER NOT NULL,
    id_логиста                INTEGER,
    id_города_отправления     INTEGER NOT NULL,
    id_города_назначения      INTEGER NOT NULL,
    дата_создания             DATE    NOT NULL DEFAULT (date('now')),
    плановая_дата_доставки    DATE,
    вес_груза_кг              REAL    NOT NULL CHECK (вес_груза_кг > 0),
    объем_груза_м3            REAL    CHECK (объем_груза_м3 IS NULL OR объем_груза_м3 > 0),
    стоимость                 REAL    CHECK (стоимость IS NULL OR стоимость >= 0),
    id_статуса_заказа         INTEGER NOT NULL,
    FOREIGN KEY (id_клиента)            REFERENCES Клиенты(id_клиента)             ON UPDATE CASCADE ON DELETE RESTRICT,
    FOREIGN KEY (id_логиста)            REFERENCES Сотрудники(id_сотрудника)       ON UPDATE CASCADE ON DELETE SET NULL,
    FOREIGN KEY (id_города_отправления) REFERENCES Города(id_города)               ON UPDATE CASCADE ON DELETE RESTRICT,
    FOREIGN KEY (id_города_назначения)  REFERENCES Города(id_города)               ON UPDATE CASCADE ON DELETE RESTRICT,
    FOREIGN KEY (id_статуса_заказа)     REFERENCES Статусы_заказа(id_статуса_заказа) ON UPDATE CASCADE ON DELETE RESTRICT,
    CHECK (id_города_отправления <> id_города_назначения)
);

CREATE TABLE IF NOT EXISTS Маршрутные_листы (
    id_маршрута       INTEGER PRIMARY KEY AUTOINCREMENT,
    id_заказа         INTEGER NOT NULL,
    id_тс             INTEGER NOT NULL,
    id_водителя       INTEGER,
    дата_начала       DATETIME,
    дата_окончания    DATETIME,
    пробег_км         REAL,
    FOREIGN KEY (id_заказа)   REFERENCES Заказы(id_заказа)                 ON UPDATE CASCADE ON DELETE CASCADE,
    FOREIGN KEY (id_тс)       REFERENCES Транспортные_средства(id_тс)      ON UPDATE CASCADE ON DELETE RESTRICT,
    FOREIGN KEY (id_водителя) REFERENCES Сотрудники(id_сотрудника)         ON UPDATE CASCADE ON DELETE SET NULL,
    CHECK (дата_окончания IS NULL OR дата_начала IS NULL OR дата_окончания >= дата_начала)
);

CREATE TABLE IF NOT EXISTS Договоры (
    id_договора       INTEGER PRIMARY KEY AUTOINCREMENT,
    id_заказа         INTEGER NOT NULL UNIQUE,
    дата_договора     DATE    NOT NULL,
    сумма_договора    REAL    NOT NULL CHECK (сумма_договора >= 0),
    FOREIGN KEY (id_заказа) REFERENCES Заказы(id_заказа) ON UPDATE CASCADE ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS Оплаты (
    id_оплаты         INTEGER PRIMARY KEY AUTOINCREMENT,
    id_договора       INTEGER NOT NULL,
    дата_оплаты       DATE    NOT NULL,
    сумма_оплаты      REAL    NOT NULL CHECK (сумма_оплаты > 0),
    способ_оплаты     TEXT,
    FOREIGN KEY (id_договора) REFERENCES Договоры(id_договора) ON UPDATE CASCADE ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS График_ТО (
    id_то             INTEGER PRIMARY KEY AUTOINCREMENT,
    id_тс             INTEGER NOT NULL,
    id_тех_персонала  INTEGER,
    id_вида_работ     INTEGER NOT NULL,
    плановая_дата     DATE    NOT NULL,
    фактическая_дата  DATE,
    результат         TEXT,
    FOREIGN KEY (id_тс)            REFERENCES Транспортные_средства(id_тс)  ON UPDATE CASCADE ON DELETE CASCADE,
    FOREIGN KEY (id_тех_персонала) REFERENCES Сотрудники(id_сотрудника)     ON UPDATE CASCADE ON DELETE SET NULL,
    FOREIGN KEY (id_вида_работ)    REFERENCES Виды_работ_ТО(id_вида_работ)  ON UPDATE CASCADE ON DELETE RESTRICT
);

-- ===================== Индексы для производительности =====================

CREATE INDEX IF NOT EXISTS idx_тс_статус       ON Транспортные_средства(id_статуса_тс);
CREATE INDEX IF NOT EXISTS idx_тс_модель       ON Транспортные_средства(id_модели);
CREATE INDEX IF NOT EXISTS idx_заказы_клиент   ON Заказы(id_клиента);
CREATE INDEX IF NOT EXISTS idx_заказы_статус   ON Заказы(id_статуса_заказа);
CREATE INDEX IF NOT EXISTS idx_марш_заказ      ON Маршрутные_листы(id_заказа);
CREATE INDEX IF NOT EXISTS idx_марш_тс         ON Маршрутные_листы(id_тс);
CREATE INDEX IF NOT EXISTS idx_оплаты_договор  ON Оплаты(id_договора);
