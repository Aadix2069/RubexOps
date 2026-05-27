import json
import os
import sys
from datetime import date
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
# STATUS ENGINE  (mirrors full Python status logic)
# ============================================================

def compute_status(start_date, end_date, remaining_qty, breach):
    """
    Returns one of: Upcoming | Active | Completed | Violated | Expired
    Status is never read from Excel — always computed here.
    """
    today = date.today()

    # Normalise types
    if isinstance(start_date, str):
        try:
            from datetime import datetime
            start_date = datetime.strptime(start_date, "%d-%m-%Y").date()
        except Exception:
            start_date = None

    if isinstance(end_date, str):
        try:
            from datetime import datetime
            end_date = datetime.strptime(end_date, "%d-%m-%Y").date()
        except Exception:
            end_date = None

    if start_date is None or end_date is None:
        return "Unknown"

    # Remaining quantity guard
    try:
        remaining = float(remaining_qty) if remaining_qty not in (None, "") else 0
    except (TypeError, ValueError):
        remaining = 0

    breach_yes = str(breach).strip().upper() == "YES" if breach else False

    # UPCOMING
    if today < start_date:
        return "Upcoming"

    # COMPLETED
    if remaining <= 0:
        return "Completed"

    # VIOLATED — end date passed, quantity remains
    if today > end_date and remaining > 0:
        return "Violated"

    # ACTIVE — within period, quantity remains, no breach
    if start_date <= today <= end_date and remaining > 0 and not breach_yes:
        return "Active"

    # EXPIRED — catch-all for resolved historical contracts
    return "Expired"


# ============================================================
# READ ACTIVE CONTRACTS
# ============================================================

def read_active_contracts():
    database_path = get_database_path()

    workbook = load_workbook(
        database_path,
        data_only=True      # read computed cell values, not formulas
    )

    if PCON_SHEET_NAME not in workbook.sheetnames:
        raise ValueError(
            f"Sheet not found: {PCON_SHEET_NAME}"
        )

    sheet = workbook[PCON_SHEET_NAME]

    contracts = []

    for row in range(START_ROW, sheet.max_row + 1):

        vendor_name    = sheet[f"B{row}"].value
        vendor_id      = sheet[f"C{row}"].value
        item_code      = sheet[f"E{row}"].value
        start_raw      = sheet[f"F{row}"].value
        end_raw        = sheet[f"G{row}"].value
        base_price     = sheet[f"L{row}"].value
        remaining_qty  = sheet[f"O{row}"].value
        breach         = sheet[f"Q{row}"].value

        # Skip empty rows
        if vendor_name is None or vendor_id is None:
            continue

        vendor_name = str(vendor_name).strip()
        vendor_id   = str(vendor_id).strip()

        if not vendor_name or not vendor_id:
            continue

        # Format dates for display and status computation
        start_str = ""
        end_str   = ""

        if hasattr(start_raw, "strftime"):
            start_str = start_raw.strftime("%d-%m-%Y")
            start_date = start_raw.date() if hasattr(start_raw, "date") else start_raw
        else:
            start_str  = str(start_raw).strip() if start_raw else ""
            start_date = start_raw

        if hasattr(end_raw, "strftime"):
            end_str = end_raw.strftime("%d-%m-%Y")
            end_date = end_raw.date() if hasattr(end_raw, "date") else end_raw
        else:
            end_str  = str(end_raw).strip() if end_raw else ""
            end_date = end_raw

        # Compute status dynamically
        status = compute_status(start_date, end_date, remaining_qty, breach)

        # Only include ACTIVE contracts
        if status != "Active":
            continue

        # Format base rate
        base_rate_str = ""
        if base_price not in (None, ""):
            try:
                base_rate_str = str(float(base_price))
            except (TypeError, ValueError):
                base_rate_str = str(base_price).strip()

        # Format item code
        item_code_str = str(item_code).strip() if item_code else ""

        contracts.append(
            {
                "VendorName": vendor_name,
                "VendorID":   vendor_id,
                "ItemCode":   item_code_str,
                "BaseRate":   base_rate_str,
                "StartDate":  start_str,
                "EndDate":    end_str,
            }
        )

    return contracts


# ============================================================
# MAIN
# ============================================================

try:
    print(
        json.dumps(
            read_active_contracts(),
            ensure_ascii=False
        )
    )

except Exception as error:
    print(f"ERROR: {error}", file=sys.stderr)
    sys.exit(1)