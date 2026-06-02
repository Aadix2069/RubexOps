import sys
from datetime import date, datetime

from sales_contract_engine import ITEM_CODE, build_contract_summary, parse_date, safe_string
from database import (
    SALES_SHEET_NAME,
    SALES_USED_COLUMNS,
    START_ROW,
    load_database,
    read_scon,
    row_is_empty,
    safe_cell,
    safe_number,
    save_database,
)
from sales_inventory_engine import build_sales_summary, normalize_percent_value


DATE_FORMAT = "%d-%m-%Y"


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def warn(message):
    print(f"WARNING: {message}", file=sys.stderr)


def looks_like_contract_id(value):
    return "-S" in safe_string(value).upper()


def clean_number(value):
    return safe_string(value).replace(",", "").replace("%", "")


def parse_required_decimal(value, field_name, allow_zero):
    text = clean_number(value)
    if text == "":
        raise ValueError(f"{field_name} is required.")

    try:
        number = float(text)
    except ValueError as error:
        raise ValueError(f"{field_name} must be a valid number.") from error

    if allow_zero:
        if number < 0:
            raise ValueError(f"{field_name} cannot be negative.")
    elif number <= 0:
        raise ValueError(f"{field_name} must be greater than zero.")

    return number


def parse_optional_decimal(value, field_name):
    text = clean_number(value)
    if text == "":
        return 0.0

    try:
        number = float(text)
    except ValueError as error:
        raise ValueError(f"{field_name} must be a valid number.") from error

    if number < 0:
        raise ValueError(f"{field_name} cannot be negative.")

    return number


def parse_percent_decimal(value, field_name):
    """
    Accepts:
      55   -> 55
      0.55 -> 55
    Returns a value in the 0..100 range.
    """
    number = parse_required_decimal(value, field_name, allow_zero=True)

    if 0 < number <= 1:
        number *= 100

    if number < 0 or number > 100:
        raise ValueError(f"{field_name} must be between 0 and 100.")

    return number


def parse_tcs_percent(value, field_name="TCS 194Q"):
    number = parse_required_decimal(
        value,
        field_name,
        allow_zero=True,
    )

    if number < 0 or number > 100:
        raise ValueError(
            f"{field_name} must be between 0 and 100."
        )

    return number


def parse_date_text(value, field_name, required=True):
    text = safe_string(value)
    if text == "":
        if required:
            raise ValueError(f"{field_name} is required.")
        return None

    try:
        return datetime.strptime(text, DATE_FORMAT).date()
    except ValueError as error:
        raise ValueError(f"{field_name} must be in dd-MM-yyyy format.") from error


def parse_args(args):
    """
    Expects exactly 12 arguments passed from C#:
    row_number, customer_name, customer_id, contract_id, item_code, invoice_number,
    sales_order_date, dispatch_date, weight, gst_percent, tcs_percent, loading_charge
    """
    if len(args) != 12:
        raise ValueError(f"Expected exactly 12 arguments, got {len(args)}.")

    return {
        "row_number": args[0],
        "customer_name": safe_string(args[1]),
        "customer_id": safe_string(args[2]),
        "contract_id": safe_string(args[3]),
        "item_code": safe_string(args[4]) or ITEM_CODE,
        "invoice_number": safe_string(args[5]),
        "sales_order_date": args[6],
        "dispatch_date": args[7],
        "weight": args[8],
        "gst_percent": args[9],
        "tcs_percent": args[10],
        "loading_charge": args[11],
    }


def find_contract_by_id(contracts, contract_id):
    key = safe_string(contract_id).casefold()
    if key == "":
        return None

    for contract in contracts:
        if safe_string(contract.get("contract_id")).casefold() == key:
            return contract

    return None


def find_contract_for_customer(contracts, customer_id, sales_order_date):
    customer_key = safe_string(customer_id).casefold()
    matches = []

    for contract in contracts:
        if safe_string(contract.get("customer_id")).casefold() != customer_key:
            continue

        start_date = parse_date(contract.get("start_date"))
        end_date = parse_date(contract.get("end_date"))
        if start_date is None or end_date is None:
            continue

        if start_date <= sales_order_date <= end_date:
            matches.append(contract)

    if not matches:
        return None

    matches.sort(
        key=lambda item: (
            parse_date(item.get("end_date")) or date.min,
            safe_string(item.get("contract_id")),
        ),
        reverse=True,
    )
    return matches[0]


def validate_contract(contract, customer_name, customer_id, sales_order_date):
    if contract is None:
        raise ValueError("Selected contract was not found in the scon sheet.")

    contract_id = safe_string(contract.get("contract_id"))
    if contract_id == "":
        raise ValueError("Selected contract is missing Contract ID.")

    if safe_string(contract.get("customer_id")).casefold() != safe_string(customer_id).casefold():
        raise ValueError("Customer ID does not match the selected contract.")

    if customer_name and safe_string(contract.get("customer_name")).casefold() != customer_name.casefold():
        raise ValueError("Customer Name and Customer ID do not match the scon sheet.")

    start_date = parse_date(contract.get("start_date"))
    end_date = parse_date(contract.get("end_date"))

    if start_date is None or end_date is None:
        raise ValueError(f"Selected contract {contract_id} has invalid dates.")

    if sales_order_date < start_date or sales_order_date > end_date:
        raise ValueError(
            "Sales Order Date is outside the selected contract period "
            f"({start_date.strftime(DATE_FORMAT)} - {end_date.strftime(DATE_FORMAT)})."
        )

    today = date.today()

    if today < start_date:
        raise ValueError(
            f"The selected contract has not started yet (Start Date: {start_date.strftime(DATE_FORMAT)})."
        )

    if today > end_date:
        raise ValueError(
            f"The selected contract has expired (End Date: {end_date.strftime(DATE_FORMAT)}). "
            "Please renew the contract before entering sales."
        )


def validate_invoice_number(sheet, row_number, invoice_number):
    invoice_key = safe_string(invoice_number).casefold()

    for row in range(START_ROW, sheet.max_row + 1):
        if row == row_number:
            continue

        existing_invoice = safe_string(safe_cell(sheet, row, "D"))
        if existing_invoice and existing_invoice.casefold() == invoice_key:
            raise ValueError("Invoice Number already exists in the sales sheet.")


def read_sales_records_from_sheet(sheet, skip_row):
    records = []

    for row in range(START_ROW, sheet.max_row + 1):
        if row == skip_row:
            continue

        if row_is_empty(sheet, row, SALES_USED_COLUMNS):
            continue

        records.append(
            {
                "customer_id": safe_string(safe_cell(sheet, row, "B")),
                "contract_id": safe_string(safe_cell(sheet, row, "C")),
                "invoice_number": safe_string(safe_cell(sheet, row, "D")),
                "sales_order_date": safe_cell(sheet, row, "E"),
                "dispatch_date": safe_cell(sheet, row, "F"),
                "weight": safe_number(safe_cell(sheet, row, "G")),
                "gst_percent": safe_number(safe_cell(sheet, row, "H")),
                "tcs_percent": safe_number(safe_cell(sheet, row, "I")),
                "loading_charge": safe_number(safe_cell(sheet, row, "J")),
            }
        )

    return records


def update_sales_data(args):
    data = parse_args(args)

    try:
        row_number = int(data["row_number"])
    except ValueError as error:
        raise ValueError("Invalid sales row number.") from error

    if row_number < START_ROW:
        raise ValueError("Invalid sales row number.")

    if data["customer_id"] == "":
        raise ValueError("Customer ID is required.")

    if data["invoice_number"] == "":
        raise ValueError("Invoice Number is required.")

    # Item code is permanent in this project. Accept blank as default, but reject wrong values.
    if safe_string(data["item_code"]) and safe_string(data["item_code"]).casefold() != ITEM_CODE.casefold():
        raise ValueError(f"Item Code must be {ITEM_CODE}.")

    sales_order_date = parse_date_text(
        data["sales_order_date"],
        "Sales Order date",
        required=True,
    )

    dispatch_date = parse_date_text(
        data["dispatch_date"],
        "Dispatch date",
        required=False,
    )

    if dispatch_date is not None and dispatch_date < sales_order_date:
        raise ValueError("Dispatch date must be on or after Sales Order date.")

    weight = parse_required_decimal(data["weight"], "Weight", allow_zero=False)
    
    gst_percent = normalize_percent_value(
        parse_percent_decimal(data["gst_percent"], "GST")
    )
    
    tcs_percent = parse_tcs_percent(
        data["tcs_percent"], "TCS 194Q"
    )
    
    loading_charge = parse_optional_decimal(
        data["loading_charge"],
        "Loading Charge",
    )

    workbook = load_database()
    try:
        if SALES_SHEET_NAME not in workbook.sheetnames:
            raise ValueError(f"Sheet not found: {SALES_SHEET_NAME}")

        sheet = workbook[SALES_SHEET_NAME]

        if row_number > sheet.max_row:
            raise ValueError("Selected sales row does not exist.")

        if row_is_empty(sheet, row_number, SALES_USED_COLUMNS):
            raise ValueError("Selected sales row is empty.")

        existing_contract_id = safe_string(safe_cell(sheet, row_number, "C"))
        selected_contract_id = safe_string(data["contract_id"]) or existing_contract_id

        contracts = read_scon()
        sales_without_current_row = read_sales_records_from_sheet(
            sheet,
            skip_row=row_number,
        )

        contract = None
        if selected_contract_id:
            contract = find_contract_by_id(contracts, selected_contract_id)
            if contract is None:
                raise ValueError("Selected contract was not found in the scon sheet.")
        else:
            contract = find_contract_for_customer(contracts, data["customer_id"], sales_order_date)
            if contract is None:
                raise ValueError("No active contract found for the selected customer and date.")

        validate_contract(
            contract,
            data["customer_name"],
            data["customer_id"],
            sales_order_date,
        )

        # Build contract summary using all sales except the current row being updated.
        contract_summary = build_contract_summary(contract, sales_without_current_row)

        if contract_summary["remaining_qty"] <= 0:
            raise ValueError("The selected contract is already completed. No further sales can be entered.")

        if contract_summary["breach_detected"]:
            raise ValueError("The selected contract is in breach. Resolve the breach before entering sales.")

        validate_invoice_number(sheet, row_number, data["invoice_number"])

        contract_customer_id = safe_string(contract.get("customer_id"))
        contract_id = safe_string(contract.get("contract_id"))

        if data["customer_id"].casefold() != contract_customer_id.casefold():
            raise ValueError("Customer ID does not match the selected contract.")

        sales_record = {
            "customer_id": contract_customer_id,
            "contract_id": contract_id,
            "invoice_number": data["invoice_number"],
            "sales_order_date": sales_order_date,
            "dispatch_date": dispatch_date or "",
            "weight": weight,
            "gst_percent": gst_percent,
            "tcs_percent": tcs_percent,
            "loading_charge": loading_charge,
        }

        sales_summary = build_sales_summary(
            sales_record,
            base_price=safe_number(contract.get("base_price")),
        )

        final_weight = float(sales_summary["weight"])
        remaining_qty = float(contract_summary["remaining_qty"])

        if final_weight <= 0:
            raise ValueError("Weight must be greater than zero.")

        if final_weight > remaining_qty:
            raise ValueError(
                f"Weight ({final_weight:,.2f}) exceeds the remaining contract quantity "
                f"({remaining_qty:,.2f})."
            )

        sheet[f"B{row_number}"] = sales_record["customer_id"]
        sheet[f"C{row_number}"] = sales_record["contract_id"]
        sheet[f"D{row_number}"] = sales_record["invoice_number"]
        sheet[f"E{row_number}"] = sales_order_date
        sheet[f"F{row_number}"] = dispatch_date
        sheet[f"G{row_number}"] = weight
        sheet[f"H{row_number}"] = gst_percent
        sheet[f"I{row_number}"] = tcs_percent
        sheet[f"J{row_number}"] = loading_charge

        save_database(workbook)

        return f"Sales data updated successfully at row {row_number}."
    finally:
        workbook.close()


try:
    print(update_sales_data(sys.argv[1:]))
except PermissionError:
    fail("Cannot save workbook. Close Excel file first.")
except Exception as error:
    fail(str(error))