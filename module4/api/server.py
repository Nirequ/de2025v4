"""HTTP-эмулятор отправки данных от клиента для модуля 4.

Запуск::

    python module4/api/server.py [--host 127.0.0.1] [--port 5000]

API (формат JSON):

* ``GET /get_data``                – возвращает очередной паспорт из очереди;
* ``POST /set_result``             – принимает результат проверки данных;
* ``GET  /healthz``                – проверка работоспособности.

Сервис возвращает паспорта по кругу: и валидные, и заведомо неправильные
(чтобы валидатор в WPF-приложении мог продемонстрировать обе ветки).
"""

from __future__ import annotations

import argparse
import json
import logging
import threading
from datetime import datetime
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any


HERE = Path(__file__).resolve().parent
DATA_FILE = HERE / "passports.json"
RESULTS_LOG = HERE / "results.log"

_lock = threading.Lock()
_index = 0
_passports: list[dict[str, Any]] = []


def _load_passports() -> None:
    global _passports
    if not DATA_FILE.exists():
        raise FileNotFoundError(
            f"Не найден файл с паспортами: {DATA_FILE}. "
            "Файл должен содержать JSON со списком объектов "
            "{'series': '....', 'number': '......', 'comment': '...'}"
        )
    with DATA_FILE.open(encoding="utf-8") as fp:
        data = json.load(fp)
    if not isinstance(data, list) or not data:
        raise ValueError(f"Файл {DATA_FILE} должен содержать непустой список.")
    _passports = data


def _next_passport() -> dict[str, Any]:
    global _index
    with _lock:
        if not _passports:
            _load_passports()
        item = _passports[_index % len(_passports)]
        _index += 1
        return dict(item)


class _Handler(BaseHTTPRequestHandler):
    server_version = "PassportEmulator/1.0"

    def log_message(self, format: str, *args: Any) -> None:
        logging.info("%s - %s", self.address_string(), format % args)

    def _send_json(self, status: int, payload: dict[str, Any]) -> None:
        body = json.dumps(payload, ensure_ascii=False).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self) -> None:  # noqa: N802
        if self.path == "/healthz":
            self._send_json(HTTPStatus.OK, {"status": "ok"})
            return
        if self.path == "/get_data":
            passport = _next_passport()
            self._send_json(
                HTTPStatus.OK,
                {
                    "series": passport["series"],
                    "number": passport["number"],
                    "issued_at": passport.get("issued_at"),
                    "comment": passport.get("comment"),
                },
            )
            return
        self._send_json(HTTPStatus.NOT_FOUND, {"error": "Маршрут не найден."})

    def do_POST(self) -> None:  # noqa: N802
        if self.path != "/set_result":
            self._send_json(HTTPStatus.NOT_FOUND, {"error": "Маршрут не найден."})
            return
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length).decode("utf-8") if length else ""
        try:
            payload = json.loads(raw) if raw else {}
        except json.JSONDecodeError:
            self._send_json(
                HTTPStatus.BAD_REQUEST,
                {"error": "Тело запроса должно быть корректным JSON."},
            )
            return
        record = {
            "received_at": datetime.utcnow().isoformat(timespec="seconds") + "Z",
            "client": self.address_string(),
            "payload": payload,
        }
        with RESULTS_LOG.open("a", encoding="utf-8") as fp:
            fp.write(json.dumps(record, ensure_ascii=False) + "\n")
        logging.info("Получен результат проверки: %s", payload)
        self._send_json(HTTPStatus.OK, {"status": "accepted"})


def main(host: str, port: int) -> None:
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(message)s",
    )
    _load_passports()
    server = ThreadingHTTPServer((host, port), _Handler)
    logging.info(
        "Эмулятор запущен на http://%s:%d, паспортов в очереди: %d",
        host,
        port,
        len(_passports),
    )
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        logging.info("Эмулятор остановлен пользователем.")
    finally:
        server.server_close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Эмулятор отправки паспортных данных.")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=5000)
    args = parser.parse_args()
    main(args.host, args.port)
