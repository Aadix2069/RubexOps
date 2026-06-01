import re
import sys
import warnings
from datetime import date, datetime

from sales_contract_engine import build_contract_summary, parse_date, safe_percent
from database import (
    SCON_REQUIRED_COLUMNS,
    SCON_SHEET_NAME,
    START_ROW,
    load_database,
    read_scon,
    read_sales,
    row_is_empty,
    safe_cell,
    safe_number,
    safe_string,
    save_database,
)

warnings.filterwarnings("ignore")


ALLOWED_RESPONSIBILITIES = {"Customer", "Company", "Pending"}
LEGACY_ARGUMENTS = 8
NEW_ARGUMENTS = 11


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


def parse_date_value(value, field_name, required=True):
    parsed = parse_date(value)

    if parsed is None:
        if required:
            fail(f"{field_name} is required.")
        return None

    if isinstance(parsed, datetime):
        return parsed.date()

    if isinstance(parsed, date):
        return parsed

    try:
        return date.fromisoformat(str(parsed))
    except Exception:
        fail(f"{field_name} must be a valid date.")


def parse_bool_flag(value):
    text = safe_string(value).strip().casefold()
    return text in {"1", "true", "yes", "y", "on"}


def is_currently_true_flag(value):
    return parse_bool_flag(value)


def read_contract_at_row(row_number):
    workbook = load_database()
    try:
        if SCON_SHEET_NAME not in workbook.sheetnames:
            fail(f"Sheet '{SCON_SHEET_NAME}' does not exist.")

        sheet = workbook[SCON_SHEET_NAME]

        if row_number < START_ROW or row_number > sheet.max_row:
            fail("Invalid contract row.")

        if row_is_empty(sheet, row_number, SCON_REQUIRED_COLUMNS):
            fail("No contract exists in selected row.")

        contract_id = safe_string(safe_cell(sheet, row_number, "D"))
        if contract_id == "":
            fail("Selected contract is missing Contract ID.")

        return {
            "row": row_number,
            "customer_name": safe_string(safe_cell(sheet, row_number, "B")),
            "customer_id": safe_string(safe_cell(sheet, row_number, "C")),
            "contract_id": contract_id,
            "start_date": safe_cell(sheet, row_number, "E"),
            "end_date": safe_cell(sheet, row_number, "F"),
            "base_price": safe_number(safe_cell(sheet, row_number, "G")),
            "agreed_qty": safe_number(safe_cell(sheet, row_number, "H")),
            "breach_responsibility": safe_string(safe_cell(sheet, row_number, "I")),
            "penalty_discount_percent": safe_number(safe_cell(sheet, row_number, "J")),
            "remedy_days": safe_number(safe_cell(sheet, row_number, "K")),
            "renewal_reference": safe_string(safe_cell(sheet, row_number, "L")),
            "ignore_remaining_qty": is_currently_true_flag(safe_cell(sheet, row_number, "M")),
        }
    finally:
        workbook.close()


def parse_contract_revision(contract_id):
    text = safe_string(contract_id).strip()
    match = re.match(r"^(?P<prefix>.+)-S(?P<revision>\d+)$", text, re.IGNORECASE)

    if not match:
        fail(f"Invalid Contract ID format: {text}")

    return int(match.group("revision"))


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


def load_contract_rows_for_customer(sheet, customer_id):
    customer_key = safe_string(customer_id).casefold()
    rows = []

    for row in range(START_ROW, sheet.max_row + 1):
        if row_is_empty(sheet, row, SCON_REQUIRED_COLUMNS):
            continue

        row_customer_id = safe_string(safe_cell(sheet, row, "C"))
        if row_customer_id.casefold() != customer_key:
            continue

        contract_id = safe_string(safe_cell(sheet, row, "D"))
        if contract_id == "":
            fail(f"Contract row {row} is missing Contract ID.")

        revision = parse_contract_revision(contract_id)

        rows.append(
            {
                "row": row,
                "customer_name": safe_string(safe_cell(sheet, row, "B")),
                "customer_id": row_customer_id,
                "contract_id": contract_id,
                "revision": revision,
                "start_date": safe_cell(sheet, row, "E"),
                "end_date": safe_cell(sheet, row, "F"),
                "base_price": safe_number(safe_cell(sheet, row, "G")),
                "agreed_qty": safe_number(safe_cell(sheet, row, "H")),
                "breach_responsibility": safe_string(safe_cell(sheet, row, "I")),
                "penalty_discount_percent": safe_number(safe_cell(sheet, row, "J")),
                "remedy_days": safe_number(safe_cell(sheet, row, "K")),
                "renewal_reference": safe_string(safe_cell(sheet, row, "L")),
                "ignore_remaining_qty": is_currently_true_flag(safe_cell(sheet, row, "M")),
            }
        )

    if not rows:
        fail("No contract chain found for the selected Customer ID.")

    rows.sort(key=lambda item: item["revision"])
    return rows


def customer_id_exists_elsewhere(new_customer_id, old_customer_id, contracts, sales_records):
    new_key = safe_string(new_customer_id).casefold()
    old_key = safe_string(old_customer_id).casefold()

    for contract in contracts:
        customer = safe_string(contract.get("customer_id")).casefold()
        if customer == "":
            continue
        if customer == new_key and customer != old_key:
            return True

    for record in sales_records:
        customer = safe_string(record.get("customer_id")).casefold()
        if customer == "":
            continue
        if customer == new_key and customer != old_key:
            return True

    return False


def update_contract_row(
    sheet,
    row_number,
    customer_name,
    customer_id,
    start_date,
    end_date,
    base_price,
    agreed_qty,
    penalty_discount_percent,
    remedy_days,
    breach_responsibility,
    ignore_remaining_qty,
):
    if safe_string(sheet["M4"].value) == "":
        sheet["M4"] = "Ignore Remaining Qty"

    sheet[f"B{row_number}"] = customer_name
    sheet[f"C{row_number}"] = customer_id
    sheet[f"E{row_number}"] = start_date
    sheet[f"F{row_number}"] = end_date
    sheet[f"G{row_number}"] = base_price
    sheet[f"H{row_number}"] = agreed_qty
    sheet[f"I{row_number}"] = breach_responsibility
    sheet[f"J{row_number}"] = penalty_discount_percent
    sheet[f"K{row_number}"] = remedy_days
    sheet[f"M{row_number}"] = 1 if ignore_remaining_qty else 0


def update_chain_customer_identifiers(
    sheet,
    chain_rows,
    new_customer_name,
    new_customer_id,
    propagate_customer_name,
):
    old_to_new = {}

    for item in chain_rows:
        old_contract_id = item["contract_id"]
        revision = item["revision"]
        new_contract_id = f"{new_customer_id}-S{revision}"
        old_to_new[old_contract_id.casefold()] = new_contract_id

    for item in chain_rows:
        row = item["row"]
        old_contract_id = item["contract_id"]
        new_contract_id = old_to_new[old_contract_id.casefold()]

        if propagate_customer_name:
            sheet[f"B{row}"] = new_customer_name

        sheet[f"C{row}"] = new_customer_id
        sheet[f"D{row}"] = new_contract_id

        old_reference = safe_string(sheet[f"L{row}"].value)
        if old_reference:
            sheet[f"L{row}"] = old_to_new.get(old_reference.casefold(), old_reference)

    return old_to_new


def update_sales_rows(workbook, old_customer_id, new_customer_id, contract_id_map):
    if "sales" not in workbook.sheetnames:
        return

    sheet = workbook["sales"]
    old_key = safe_string(old_customer_id).casefold()

    for row in range(START_ROW, sheet.max_row + 1):
        if row_is_empty(sheet, row, ("B", "C", "D", "E", "F", "G", "H", "I", "J")):
            continue

        row_customer_id = safe_string(safe_cell(sheet, row, "B"))
        row_contract_id = safe_string(safe_cell(sheet, row, "C"))

        customer_matches = row_customer_id.casefold() == old_key
        contract_matches = row_contract_id.casefold() in contract_id_map

        if not customer_matches and not contract_matches:
            continue

        sheet[f"B{row}"] = new_customer_id

        if contract_matches:
            sheet[f"C{row}"] = contract_id_map[row_contract_id.casefold()]


def update_sales_contract(args):
    if len(args) not in {LEGACY_ARGUMENTS, NEW_ARGUMENTS}:
        fail(f"Invalid number of arguments received from C# (Got {len(args)}, expected {LEGACY_ARGUMENTS} or {NEW_ARGUMENTS}).")

    try:
        row_number = int(args[0])
    except ValueError:
        fail("Invalid row number.")

    current_contract = read_contract_at_row(row_number)
    current_summary = build_contract_summary(
        {
            "customer_name": current_contract["customer_name"],
            "customer_id": current_contract["customer_id"],
            "contract_id": current_contract["contract_id"],
            "start_date": current_contract["start_date"],
            "end_date": current_contract["end_date"],
            "base_price": current_contract["base_price"],
            "agreed_qty": current_contract["agreed_qty"],
            "breach_responsibility": current_contract["breach_responsibility"],
            "penalty_discount_percent": current_contract["penalty_discount_percent"],
            "remedy_days": current_contract["remedy_days"],
            "renewal_reference": current_contract["renewal_reference"],
            "ignore_remaining_qty": current_contract["ignore_remaining_qty"],
        },
        read_sales(),
    )

    current_status = calculate_status(
        current_summary,
        find_successor_contract_id(
            current_contract["contract_id"],
            read_scon(),
        ) != "",
    )

    if current_status == "Expired":
        fail("Expired historical contracts cannot be edited.")

    if len(args) == LEGACY_ARGUMENTS:
        customer_name = safe_string(args[1])
        customer_id = safe_string(args[2])
        start_date = parse_date_value(current_contract["start_date"], "Start Date")
        end_date = parse_date_value(current_contract["end_date"], "End Date")
        base_price = parse_decimal(args[3], "Base Price")
        agreed_qty = parse_decimal(args[4], "Agreed Quantity")
        penalty_discount_percent = safe_percent(parse_decimal(args[5], "Penalty Percentage"))
        remedy_days = parse_decimal(args[6], "Remedy Days")
        breach_responsibility = safe_string(args[7])
        ignore_remaining_qty = current_contract["ignore_remaining_qty"]
    else:
        customer_name = safe_string(args[1])
        customer_id = safe_string(args[2])
        start_date = parse_date_value(args[3], "Start Date")
        end_date = parse_date_value(args[4], "End Date")
        base_price = parse_decimal(args[5], "Base Price")
        agreed_qty = parse_decimal(args[6], "Agreed Quantity")
        penalty_discount_percent = safe_percent(parse_decimal(args[7], "Penalty Percentage"))
        remedy_days = parse_decimal(args[8], "Remedy Days")
        breach_responsibility = safe_string(args[9])
        ignore_remaining_qty = parse_bool_flag(args[10])

    if customer_name == "":
        fail("Customer Name cannot be empty.")

    if customer_id == "":
        fail("Customer ID cannot be empty.")

    if start_date is None:
        fail("Start Date is required.")

    if end_date is None:
        fail("End Date is required.")

    if end_date < start_date:
        fail("End Date cannot be earlier than Start Date.")

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

    contracts = read_scon()
    sales_records = read_sales()

    old_customer_id = safe_string(current_contract["customer_id"])
    customer_changed = customer_id.casefold() != old_customer_id.casefold()

    if customer_changed and customer_id_exists_elsewhere(
        customer_id,
        old_customer_id,
        contracts,
        sales_records,
    ):
        fail("Another contract already uses this Customer ID. Customer ID must remain unique.")

    if (not ignore_remaining_qty) and (not current_summary["breach_detected"]) and breach_responsibility != "Pending":
        fail("Breach responsibility can only be assigned when breach is detected.")

    workbook = load_database()
    try:
        if SCON_SHEET_NAME not in workbook.sheetnames:
            fail(f"Sheet '{SCON_SHEET_NAME}' does not exist.")

        scon_sheet = workbook[SCON_SHEET_NAME]

        if row_number < START_ROW or row_number > scon_sheet.max_row:
            fail("Contract row does not exist.")

        if customer_changed:
            chain_rows = load_contract_rows_for_customer(scon_sheet, old_customer_id)
            contract_id_map = update_chain_customer_identifiers(
                scon_sheet,
                chain_rows,
                customer_name,
                customer_id,
                propagate_customer_name=True,
            )
            update_sales_rows(
                workbook,
                old_customer_id,
                customer_id,
                contract_id_map,
            )
        else:
            update_contract_row(
                scon_sheet,
                row_number,
                customer_name,
                customer_id,
                start_date,
                end_date,
                base_price,
                agreed_qty,
                penalty_discount_percent,
                remedy_days,
                breach_responsibility,
                ignore_remaining_qty,
            )

        if customer_changed:
            # The selected row was already updated through the chain mapping above.
            # Update the selected row's contract-specific fields as well.
            scon_sheet[f"E{row_number}"] = start_date
            scon_sheet[f"F{row_number}"] = end_date
            scon_sheet[f"G{row_number}"] = base_price
            scon_sheet[f"H{row_number}"] = agreed_qty
            scon_sheet[f"I{row_number}"] = breach_responsibility
            scon_sheet[f"J{row_number}"] = penalty_discount_percent
            scon_sheet[f"K{row_number}"] = remedy_days
            scon_sheet[f"M{row_number}"] = 1 if ignore_remaining_qty else 0

        save_database(workbook)

        return "Contract updated successfully."
    finally:
        workbook.close()


try:
    print(update_sales_contract(sys.argv[1:]))
except PermissionError:
    fail("Cannot save workbook. Close Excel file first.")
except Exception as error:
    fail(str(error))