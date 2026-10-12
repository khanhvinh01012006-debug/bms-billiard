#!/bin/sh
set -e
DATA_DIR="${Storage__DataDirectory:-/data}"
mkdir -p "$DATA_DIR/uploads"
if [ ! -f "$DATA_DIR/bms.db" ] && [ -f /app/seed/bms.db ]; then
    echo "[BMS] Chưa có $DATA_DIR/bms.db -> chép database demo vào."
    cp /app/seed/bms.db "$DATA_DIR/bms.db"
fi
exec dotnet /app/BMS.API.dll
