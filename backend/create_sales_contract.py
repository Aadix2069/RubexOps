import sys
import warnings
from datetime import datetime

from sales_contract_engine import ITEM_CODE, generate_contract_id, safe_percent
from database import append_scon, read_scon, safe_string

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


def create_sales_contract(args):
    if len(args) != EXPECTED_ARGUMENTS - 1:
        fail("Missing required arguments.")

    customer_name = safe_string(args[0])
    customer_id = safe_string(args[1])

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

    if customer_name == "":
        fail("Customer Name cannot be empty.")

    if customer_id == "":
        fail("Customer ID cannot be empty.")

    if end_date <= start_date:
        fail("End date must be after start date.")

    if base_price <= 0:
        fail("Base Price must be greater than 0.")

    if agreed_qty <= 0:
        fail("Agreed Quantity must be greater than 0.")

    if remedy_days < 0:
        fail("Remedy Days cannot be negative.")

    existing_contracts = read_scon()
    customer_key = customer_id.casefold()

    if any(safe_string(contract.get("customer_id")).casefold() == customer_key for contract in existing_contracts):
        fail("Customer ID already exists. Please renew the existing contract instead.")

    contract_id = generate_contract_id(customer_id, existing_contracts)

    append_scon(
        {
            "customer_name": customer_name,
            "customer_id": customer_id,
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

    return "Sales Contract Created Successfully"


try:
    print(create_sales_contract(sys.argv[1:]))
except Exception as error:
    fail(str(error))