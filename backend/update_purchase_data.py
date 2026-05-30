import sys
from datetime import date, datetime

from contract_engine import ITEM_CODE, ITEM_NAME, parse_date, safe_string
from database import (
    PURCHASE_SHEET_NAME,
    PURCHASE_USED_COLUMNS,
    START_ROW,
    load_database,
    read_pcon,
    row_is_empty,
    safe_cell,
    safe_number,
    save_database,
)
from inventory_engine import build_purchase_summary, normalize_percent_value


DATE_FORMAT = "%d-%m-%Y"


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def looks_like_contract_id(value):
    return "-R" in safe_string(value).upper()


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


def parse_date_text(value, field_name):
    text = safe_string(value)
    if text == "":
        raise ValueError(f"{field_name} is required.")

    try:
        return datetime.strptime(text, DATE_FORMAT).date()
    except ValueError as error:
        raise ValueError(f"{field_name} must be in dd-MM-yyyy format.") from error


def parse_args(args):
    if len(args) not in {15, 16}:
        raise ValueError(
            "Expected 15 legacy arguments or 16 arguments including Contract ID."
        )

    row_number = args[0]
    vendor_name = safe_string(args[1])
    vendor_id = safe_string(args[2])

    if len(args) == 16:
        if looks_like_contract_id(args[3]):
            contract_id = safe_string(args[3])
            item_code = safe_string(args[4]) or ITEM_CODE
            values = args[5:]
        elif looks_like_contract_id(args[4]):
            item_code = safe_string(args[3]) or ITEM_CODE
            contract_id = safe_string(args[4])
            values = args[5:]
        else:
            contract_id = safe_string(args[3])
            item_code = safe_string(args[4]) or ITEM_CODE
            values = args[5:]
    else:
        contract_id = ""
        item_code = safe_string(args[3]) or ITEM_CODE
        values = args[4:]

    (
        invoice_number,
        purchase_order_date,
        delivery_date,
        invoice_weight,
        before_unloading,
        carrier_weight,
        number_of_bags,
        calculated_drc,
        gst_percent,
        tds_percent,
        unloading_charge,
    ) = values

    return {
        "row_number": row_number,
        "vendor_name": vendor_name,
        "vendor_id": vendor_id,
        "contract_id": contract_id,
        "item_code": item_code,
        "invoice_number": safe_string(invoice_number),
        "purchase_order_date": purchase_order_date,
        "delivery_date": delivery_date,
        "invoice_weight": invoice_weight,
        "before_unloading": before_unloading,
        "carrier_weight": carrier_weight,
        "number_of_bags": number_of_bags,
        "calculated_drc_percent": calculated_drc,
        "gst_percent": gst_percent,
        "tds_percent": tds_percent,
        "unloading_charge": unloading_charge,
    }


def find_contract_by_id(contracts, contract_id):
    key = safe_string(contract_id).casefold()
    if key == "":
        return None

    for contract in contracts:
        if safe_string(contract.get("contract_id")).casefold() == key:
            return contract

    return None


def find_contract_for_vendor(contracts, vendor_id, purchase_order_date):
    vendor_key = safe_string(vendor_id).casefold()
    matches = []

    for contract in contracts:
        if safe_string(contract.get("vendor_id")).casefold() != vendor_key:
            continue

        start_date = parse_date(contract.get("start_date"))
        end_date = parse_date(contract.get("end_date"))
        if start_date is None or end_date is None:
            continue

        if start_date <= purchase_order_date <= end_date:
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


def validate_contract(contract, vendor_name, vendor_id, purchase_order_date):
    if contract is None:
        raise ValueError("Selected contract was not found in the pcon sheet.")

    contract_id = safe_string(contract.get("contract_id"))
    if contract_id == "":
        raise ValueError("Selected contract is missing Contract ID.")

    if safe_string(contract.get("vendor_id")).casefold() != safe_string(vendor_id).casefold():
        raise ValueError("Vendor ID does not match the selected contract.")

    if vendor_name and safe_string(contract.get("vendor_name")).casefold() != vendor_name.casefold():
        raise ValueError("Vendor Name and Vendor ID do not match the pcon sheet.")

    start_date = parse_date(contract.get("start_date"))
    end_date = parse_date(contract.get("end_date"))

    if start_date is None or end_date is None:
        raise ValueError(f"Selected contract {contract_id} has invalid dates.")

    if purchase_order_date < start_date or purchase_order_date > end_date:
        raise ValueError(
            "Purchase Order Date is outside the selected contract period "
            f"({start_date.strftime(DATE_FORMAT)} - {end_date.strftime(DATE_FORMAT)})."
        )


def validate_invoice_number(sheet, row_number, invoice_number):
    invoice_key = safe_string(invoice_number).casefold()

    for row in range(START_ROW, sheet.max_row + 1):
        if row == row_number:
            continue

        existing_invoice = safe_string(safe_cell(sheet, row, "D"))
        if existing_invoice and existing_invoice.casefold() == invoice_key:
            raise ValueError("Invoice Number already exists in the purchase sheet.")


def update_purchase_data(args):
    data = parse_args(args)

    try:
        row_number = int(data["row_number"])
    except ValueError as error:
        raise ValueError("Invalid purchase row number.") from error

    if data["vendor_name"] == "":
        raise ValueError("Vendor Name is required.")

    if data["vendor_id"] == "":
        raise ValueError("Vendor ID is required.")

    if data["invoice_number"] == "":
        raise ValueError("Invoice Number is required.")

    purchase_order_date = parse_date_text(data["purchase_order_date"], "Purchase Order date")
    delivery_date = parse_date_text(data["delivery_date"], "Delivery date")

    if delivery_date < purchase_order_date:
        raise ValueError("Delivery date must be on or after Purchase Order date.")

    invoice_weight = parse_optional_decimal(data["invoice_weight"], "Invoice Weight")
    before_unloading = parse_required_decimal(data["before_unloading"], "Before Unloading", allow_zero=False)
    carrier_weight = parse_required_decimal(data["carrier_weight"], "Carrier Weight", allow_zero=True)
    number_of_bags = parse_optional_decimal(data["number_of_bags"], "Number Of Bags")
    calculated_drc_percent = normalize_percent_value(
        parse_required_decimal(data["calculated_drc_percent"], "Calculated DRC", allow_zero=True)
    )
    gst_percent = normalize_percent_value(
        parse_required_decimal(data["gst_percent"], "GST", allow_zero=True)
    )
    tds_percent = normalize_percent_value(
        parse_required_decimal(data["tds_percent"], "TDS 194Q", allow_zero=True)
    )
    unloading_charge = parse_required_decimal(data["unloading_charge"], "Unloading Charge", allow_zero=True)

    if carrier_weight > before_unloading:
        raise ValueError("Carrier Weight cannot be greater than Before Unloading weight.")

    workbook = load_database()
    try:
        if PURCHASE_SHEET_NAME not in workbook.sheetnames:
            raise ValueError(f"Sheet not found: {PURCHASE_SHEET_NAME}")

        sheet = workbook[PURCHASE_SHEET_NAME]

        if row_number < START_ROW or row_number > sheet.max_row:
            raise ValueError("Selected purchase row does not exist.")

        if row_is_empty(sheet, row_number, PURCHASE_USED_COLUMNS):
            raise ValueError("Selected purchase row is empty.")

        existing_contract_id = safe_string(safe_cell(sheet, row_number, "C"))
        contract_id = data["contract_id"] or existing_contract_id

        contracts = read_pcon()
        contract = find_contract_by_id(contracts, contract_id)
        if contract is None:
            contract = find_contract_for_vendor(contracts, data["vendor_id"], purchase_order_date)

        validate_contract(
            contract,
            data["vendor_name"],
            data["vendor_id"],
            purchase_order_date,
        )

        validate_invoice_number(sheet, row_number, data["invoice_number"])

        purchase_record = {
            "vendor_id": data["vendor_id"],
            "contract_id": safe_string(contract.get("contract_id")),
            "invoice_number": data["invoice_number"],
            "purchase_order_date": purchase_order_date,
            "delivery_date": delivery_date,
            "invoice_weight": invoice_weight,
            "before_unloading": before_unloading,
            "carrier_weight": carrier_weight,
            "number_of_bags": number_of_bags,
            "calculated_drc_percent": calculated_drc_percent,
            "gst_percent": gst_percent,
            "tds_percent": tds_percent,
            "unloading_charge": unloading_charge,
        }

        build_purchase_summary(
            purchase_record,
            base_price=safe_number(contract.get("base_price")),
        )

        sheet[f"B{row_number}"] = purchase_record["vendor_id"]
        sheet[f"C{row_number}"] = purchase_record["contract_id"]
        sheet[f"D{row_number}"] = purchase_record["invoice_number"]
        sheet[f"E{row_number}"] = purchase_order_date
        sheet[f"F{row_number}"] = delivery_date
        sheet[f"G{row_number}"] = invoice_weight
        sheet[f"H{row_number}"] = before_unloading
        sheet[f"I{row_number}"] = carrier_weight
        sheet[f"J{row_number}"] = number_of_bags
        sheet[f"K{row_number}"] = calculated_drc_percent
        sheet[f"L{row_number}"] = gst_percent
        sheet[f"M{row_number}"] = tds_percent
        sheet[f"N{row_number}"] = unloading_charge

        save_database(workbook)

        return f"Purchase data updated successfully at row {row_number}."
    finally:
        workbook.close()


try:
    print(update_purchase_data(sys.argv[1:]))
except PermissionError:
    fail("Cannot save workbook. Close Excel file first.")
except Exception as error:
    fail(str(error))
