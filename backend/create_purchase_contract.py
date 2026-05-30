import sys
import warnings
from datetime import datetime

from contract_engine import ITEM_CODE, generate_contract_id, safe_percent
from database import append_pcon, read_pcon, safe_string

warnings.filterwarnings("ignore")


EXPECTED_ARGUMENTS = 10
DATE_FORMAT = "%d-%m-%Y"


def fail(message):
    sys.stderr.write(f"ERROR: {message}\n")
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


def create_purchase_contract(args):
    if len(args) != EXPECTED_ARGUMENTS - 1:
        fail("Missing required arguments.")

    vendor_name = safe_string(args[0])
    vendor_id = safe_string(args[1])

    # Kept only for old C# call compatibility. Item data is now constant.
    _legacy_item_code = safe_string(args[2]) or ITEM_CODE

    start_date = parse_date_text(args[3], "Start Date")
    end_date = parse_date_text(args[4], "End Date")
    base_price = parse_decimal(args[5], "Base Price")
    agreed_qty = parse_decimal(args[6], "Agreed Quantity")
    penalty_discount_percent = safe_percent(
        parse_decimal(args[7], "Penalty / Discount Percentage")
    )
    remedy_days = parse_decimal(args[8], "Remedy Days")

    if vendor_name == "":
        fail("Vendor Name cannot be empty.")

    if vendor_id == "":
        fail("Vendor ID cannot be empty.")

    if end_date <= start_date:
        fail("End date must be after start date.")

    if base_price <= 0:
        fail("Base Price must be greater than 0.")

    if agreed_qty <= 0:
        fail("Agreed Quantity must be greater than 0.")

    if remedy_days < 0:
        fail("Remedy Days cannot be negative.")

    existing_contracts = read_pcon()
    vendor_key = vendor_id.casefold()

    if any(safe_string(contract.get("vendor_id")).casefold() == vendor_key for contract in existing_contracts):
        fail("Vendor ID already exists. Please renew the existing contract instead.")

    contract_id = generate_contract_id(vendor_id, existing_contracts)

    append_pcon(
        {
            "vendor_name": vendor_name,
            "vendor_id": vendor_id,
            "contract_id": contract_id,
            "start_date": start_date,
            "end_date": end_date,
            "base_price": base_price,
            "agreed_qty": agreed_qty,
            "breach_responsibility": "Pending",
            "penalty_discount_percent": penalty_discount_percent,
            "remedy_days": remedy_days,
            "renewal_reference": "",
        }
    )

    return "Purchase Contract Created Successfully"


try:
    print(create_purchase_contract(sys.argv[1:]))
except Exception as error:
    fail(str(error))
