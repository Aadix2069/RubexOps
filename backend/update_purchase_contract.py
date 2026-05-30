import sys
import warnings
from datetime import date

from contract_engine import build_contract_summary, parse_date, safe_percent
from database import (
    PCON_REQUIRED_COLUMNS,
    PCON_SHEET_NAME,
    START_ROW,
    load_database,
    read_pcon,
    read_purchase,
    row_is_empty,
    safe_cell,
    safe_number,
    safe_string,
    save_database,
)

warnings.filterwarnings("ignore")


EXPECTED_ARGUMENTS = 8
ALLOWED_RESPONSIBILITIES = {"Vendor", "Company", "Shared", "Pending"}


def fail(message):
    sys.stderr.write(f"ERROR: {message}\n")
    sys.exit(1)


def parse_decimal(value, field_name):
    text = safe_string(value).replace(",", "").replace("%", "")
    if text == "":
        fail(f"{field_name} is required.")

    try:
        return float(text)
    except ValueError:
        fail(f"{field_name} must be numeric.")


def read_contract_at_row(row_number):
    workbook = load_database()
    try:
        if PCON_SHEET_NAME not in workbook.sheetnames:
            fail(f"Sheet '{PCON_SHEET_NAME}' does not exist.")

        sheet = workbook[PCON_SHEET_NAME]

        if row_number < START_ROW or row_number > sheet.max_row:
            fail("Invalid contract row.")

        if row_is_empty(sheet, row_number, PCON_REQUIRED_COLUMNS):
            fail("No contract exists in selected row.")

        contract_id = safe_string(safe_cell(sheet, row_number, "D"))
        if contract_id == "":
            fail("Selected contract is missing Contract ID.")

        return {
            "row": row_number,
            "vendor_name": safe_string(safe_cell(sheet, row_number, "B")),
            "vendor_id": safe_string(safe_cell(sheet, row_number, "C")),
            "contract_id": contract_id,
            "start_date": safe_cell(sheet, row_number, "E"),
            "end_date": safe_cell(sheet, row_number, "F"),
            "base_price": safe_number(safe_cell(sheet, row_number, "G")),
            "agreed_qty": safe_number(safe_cell(sheet, row_number, "H")),
            "breach_responsibility": safe_string(safe_cell(sheet, row_number, "I")),
            "penalty_discount_percent": safe_number(safe_cell(sheet, row_number, "J")),
            "remedy_days": safe_number(safe_cell(sheet, row_number, "K")),
            "renewal_reference": safe_string(safe_cell(sheet, row_number, "L")),
        }
    finally:
        workbook.close()


def find_successor_contract_id(contract_id, contracts):
    key = safe_string(contract_id).casefold()
    if key == "":
        return ""

    for contract in contracts:
        if safe_string(contract.get("renewal_reference")).casefold() == key:
            return safe_string(contract.get("contract_id"))

    return ""


def calculate_status(summary, has_successor):
    if has_successor:
        return "Expired"

    today = date.today()
    start_date = parse_date(summary.get("start_date"))
    end_date = parse_date(summary.get("end_date"))
    remaining_qty = safe_number(summary.get("remaining_qty"))

    if remaining_qty <= 0:
        if end_date is not None and today > end_date:
            return "Expired"
        return "Completed"

    if start_date is not None and today < start_date:
        return "Upcoming"

    if end_date is not None and today > end_date and remaining_qty > 0:
        return "Violated"

    if summary.get("breach_detected") and remaining_qty > 0:
        return "Violated"

    return "Active"


def update_purchase_contract(args):
    if len(args) != EXPECTED_ARGUMENTS:
        fail("Invalid number of arguments received from C#.")

    try:
        row_number = int(args[0])
    except ValueError:
        fail("Invalid row number.")

    vendor_name = safe_string(args[1])
    vendor_id = safe_string(args[2])
    base_price = parse_decimal(args[3], "Base Price")
    agreed_qty = parse_decimal(args[4], "Agreed Quantity")
    penalty_discount_percent = safe_percent(parse_decimal(args[5], "Penalty Percentage"))
    remedy_days = parse_decimal(args[6], "Remedy Days")
    breach_responsibility = safe_string(args[7])

    if vendor_name == "":
        fail("Vendor Name cannot be empty.")

    if vendor_id == "":
        fail("Vendor ID cannot be empty.")

    if base_price <= 0:
        fail("Base Price must be greater than zero.")

    if agreed_qty <= 0:
        fail("Agreed Quantity must be greater than zero.")

    if remedy_days < 0:
        fail("Remedy Days cannot be negative.")

    if breach_responsibility in {"", "None"}:
        breach_responsibility = "Pending"

    if breach_responsibility not in ALLOWED_RESPONSIBILITIES:
        fail("Invalid breach responsibility value.")

    current_contract = read_contract_at_row(row_number)

    if vendor_id.casefold() != current_contract["vendor_id"].casefold():
        fail("Vendor ID cannot be changed after Contract ID has been generated.")

    contracts = read_pcon()
    purchases = read_purchase()
    successor_contract_id = find_successor_contract_id(
        current_contract["contract_id"],
        contracts,
    )

    current_summary = build_contract_summary(current_contract, purchases)
    current_status = calculate_status(
        current_summary,
        successor_contract_id != "",
    )

    if current_status == "Expired":
        fail("Expired historical contracts cannot be edited.")

    if not current_summary["breach_detected"] and breach_responsibility != "Pending":
        fail("Breach responsibility can only be assigned when breach is detected.")

    workbook = load_database()
    try:
        if PCON_SHEET_NAME not in workbook.sheetnames:
            fail(f"Sheet '{PCON_SHEET_NAME}' does not exist.")

        sheet = workbook[PCON_SHEET_NAME]

        if row_number < START_ROW or row_number > sheet.max_row:
            fail("Contract row does not exist.")

        sheet[f"B{row_number}"] = vendor_name
        sheet[f"C{row_number}"] = vendor_id
        sheet[f"G{row_number}"] = base_price
        sheet[f"H{row_number}"] = agreed_qty
        sheet[f"I{row_number}"] = breach_responsibility
        sheet[f"J{row_number}"] = penalty_discount_percent
        sheet[f"K{row_number}"] = remedy_days

        save_database(workbook)

        return "Contract updated successfully."
    finally:
        workbook.close()


try:
    print(update_purchase_contract(sys.argv[1:]))
except PermissionError:
    fail("Cannot save workbook. Close Excel file first.")
except Exception as error:
    fail(str(error))
