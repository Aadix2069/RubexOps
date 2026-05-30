import sys
from datetime import datetime

from contract_engine import generate_contract_id, parse_date, safe_percent
from database import (
    PCON_REQUIRED_COLUMNS,
    PCON_SHEET_NAME,
    START_ROW,
    append_pcon,
    load_database,
    read_pcon,
    row_is_empty,
    safe_cell,
    safe_number,
    safe_string,
)


DATE_FORMAT = "%d-%m-%Y"


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def parse_date_text(value, field_name):
    text = safe_string(value)
    if text == "":
        fail(f"{field_name} is required.")

    try:
        return datetime.strptime(text, DATE_FORMAT).date()
    except ValueError:
        fail(f"{field_name} must be in dd-MM-yyyy format.")


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
            fail("Invalid old contract row.")

        if row_is_empty(sheet, row_number, PCON_REQUIRED_COLUMNS):
            fail("No contract exists in selected row.")

        contract_id = safe_string(safe_cell(sheet, row_number, "D"))
        if contract_id == "":
            fail("Selected contract is missing Contract ID.")

        return {
            "row": row_number,
            "sl_no": safe_number(safe_cell(sheet, row_number, "A")),
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


def ensure_not_already_renewed(old_contract, existing_contracts):
    old_contract_id = safe_string(old_contract.get("contract_id")).casefold()

    for contract in existing_contracts:
        renewal_reference = safe_string(contract.get("renewal_reference")).casefold()
        if renewal_reference == old_contract_id:
            fail(
                "This contract has already been renewed "
                f"as {safe_string(contract.get('contract_id'))}."
            )


def renew_contract(args):
    if len(args) != 7:
        fail(
            "Expected 7 arguments: old row, new start date, new end date, "
            "base price, agreed quantity, penalty percentage, remedy days."
        )

    try:
        old_row = int(args[0])
    except ValueError:
        fail("Invalid old contract row.")

    new_start_date = parse_date_text(args[1], "New Start Date")
    new_end_date = parse_date_text(args[2], "New End Date")

    if new_end_date <= new_start_date:
        fail("New End Date must be after New Start Date.")

    base_price = parse_decimal(args[3], "Base Price")
    agreed_qty = parse_decimal(args[4], "Agreed Quantity")
    penalty_discount_percent = safe_percent(parse_decimal(args[5], "Penalty Percentage"))
    remedy_days = parse_decimal(args[6], "Remedy Days")

    if base_price <= 0:
        fail("Base Price must be greater than zero.")

    if agreed_qty <= 0:
        fail("Agreed Quantity must be greater than zero.")

    if remedy_days < 0:
        fail("Remedy Days cannot be negative.")

    old_contract = read_contract_at_row(old_row)
    old_end_date = parse_date(old_contract.get("end_date"))

    if old_end_date is not None and new_start_date <= old_end_date:
        fail("New Start Date must be after the old contract End Date to prevent delivery overlap.")

    existing_contracts = read_pcon()
    ensure_not_already_renewed(old_contract, existing_contracts)

    new_contract_id = generate_contract_id(
        old_contract["vendor_id"],
        existing_contracts,
    )

    sl_no = append_pcon(
        {
            "vendor_name": old_contract["vendor_name"],
            "vendor_id": old_contract["vendor_id"],
            "contract_id": new_contract_id,
            "start_date": new_start_date,
            "end_date": new_end_date,
            "base_price": base_price,
            "agreed_qty": agreed_qty,
            "breach_responsibility": "Pending",
            "penalty_discount_percent": penalty_discount_percent,
            "remedy_days": remedy_days,
            "renewal_reference": old_contract["contract_id"],
        }
    )

    return (
        f"Contract renewed successfully. Old contract {old_contract['contract_id']} "
        f"is now referenced by new contract {new_contract_id}. Sl.No: {sl_no}."
    )


try:
    print(renew_contract(sys.argv[1:]))
except Exception as error:
    fail(str(error))
