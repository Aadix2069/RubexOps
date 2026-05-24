import json
import os
import sys
from pathlib import Path

from openpyxl import load_workbook


# ============================================================
# CONFIG
# ============================================================

CONFIG_PATH = (
    Path.home()
    / "Documents"
    / "RubexOps"
    / "database_config.json"
)

PCON_SHEET_NAME = "pcon"
START_ROW = 5


# ============================================================
# DATABASE PATH
# ============================================================

def get_database_path():
    if not CONFIG_PATH.exists():
        raise FileNotFoundError(
            f"Database config file not found: {CONFIG_PATH}"
        )

    with open(CONFIG_PATH, "r", encoding="utf-8") as file:
        config = json.load(file)

    database_path = (
        config.get("database_path")
        or config.get("excel_path")
        or config.get("path")
    )

    if not database_path:
        raise ValueError(
            "Database path was not found in database_config.json."
        )

    if not os.path.exists(database_path):
        raise FileNotFoundError(
            f"Excel database file not found: {database_path}"
        )

    return database_path


# ============================================================
# READ VENDORS
# ============================================================

def read_vendors():
    database_path = get_database_path()

    workbook = load_workbook(
        database_path,
        data_only=False
    )

    if PCON_SHEET_NAME not in workbook.sheetnames:
        raise ValueError(
            f"Sheet not found: {PCON_SHEET_NAME}"
        )

    sheet = workbook[PCON_SHEET_NAME]

    vendors = []
    seen_vendor_ids = set()

    for row in range(START_ROW, sheet.max_row + 1):
        vendor_name = sheet[f"B{row}"].value
        vendor_id = sheet[f"C{row}"].value

        if vendor_name is None or vendor_id is None:
            continue

        vendor_name = str(vendor_name).strip()
        vendor_id = str(vendor_id).strip()

        if not vendor_name or not vendor_id:
            continue

        if vendor_id in seen_vendor_ids:
            continue

        vendors.append(
            {
                "VendorName": vendor_name,
                "VendorID": vendor_id
            }
        )

        seen_vendor_ids.add(vendor_id)

    return vendors


# ============================================================
# MAIN
# ============================================================

try:
    print(
        json.dumps(
            read_vendors(),
            ensure_ascii=False
        )
    )

except Exception as error:
    print(f"ERROR: {error}", file=sys.stderr)
    sys.exit(1)
