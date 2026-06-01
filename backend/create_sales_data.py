import sys
from datetime import date, datetime

from sales_contract_engine import build_contract_summary, parse_date, safe_string
from database import append_sales, read_scon, read_sales
from sales_inventory_engine import build_sales_summary


DATE_FORMAT = "%d-%m-%Y"


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def looks_like_contract_id(value):
    text = safe_string(value)
    return "-S" in text.upper()


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


def clean_number(value):
    return safe_string(value).replace(",", "").replace("%", "")


def parse_required_number(value, field_name, allow_zero=True):
    text = clean_number(value)

    if text == "":
        raise ValueError(f"{field_name} is required.")

    try:
        number = float(text)
    except ValueError as error:
        raise ValueError(f"{field_name} must be numeric.") from error

    if allow_zero:
        if number < 0:
            raise ValueError(f"{field_name} cannot be negative.")
    else:
        if number <= 0:
            raise ValueError(f"{field_name} must be greater than zero.")

    return number


def parse_optional_number(value, field_name):
    text = clean_number(value)

    if text == "":
        return 0.0

    try:
        number = float(text)
    except ValueError as error:
        raise ValueError(f"{field_name} must be numeric.") from error

    if number < 0:
        raise ValueError(f"{field_name} cannot be negative.")

    return number


def parse_percent_number(value, field_name):
    """
    Accepts:
        55   -> 55
        0.55 -> 55
    Rejects anything outside 0..100 after normalization.
    """
    number = parse_required_number(value, field_name, allow_zero=True)

    if 0 < number <= 1:
        number *= 100

    if number < 0 or number > 100:
        raise ValueError(f"{field_name} must be between 0 and 100.")

    return number


def parse_tcs_percent(value, field_name="TCS 194Q"):
    """
    TCS is entered literally.

    Examples:
        0.1 = 0.1%
        1   = 1%
        5   = 5%
    """

    number = parse_required_number(
        value,
        field_name,
        allow_zero=True,
    )

    if number < 0 or number > 100:
        raise ValueError(
            f"{field_name} must be between 0 and 100."
        )

    return number


def parse_sales_args(args):
    if len(args) == 8:
        customer_id = safe_string(args[0])
        contract_id = ""
        values = args[1:]
    elif len(args) == 9:
        if looks_like_contract_id(args[0]) and not looks_like_contract_id(args[1]):
            contract_id = safe_string(args[0])
            customer_id = safe_string(args[1])
        else:
            customer_id = safe_string(args[0])
            contract_id = safe_string(args[1])

        values = args[2:]
    else:
        raise ValueError(
            "Expected 8 legacy arguments or 9 arguments including Contract ID."
        )

    (
        invoice_number,
        sales_order_date,
        dispatch_date,
        weight,
        gst_percent,
        tcs_percent,
        loading_charge,
    ) = values

    return {
        "customer_id": customer_id,
        "contract_id": contract_id,
        "invoice_number": safe_string(invoice_number),
        "sales_order_date": sales_order_date,
        "dispatch_date": dispatch_date,
        "weight": weight,
        "gst_percent": gst_percent,
        "tcs_percent": tcs_percent,
        "loading_charge": loading_charge,
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

    if customer_key == "":
        return None

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


def invoice_exists(sales_records, invoice_number):
    invoice_key = safe_string(invoice_number).casefold()

    if invoice_key == "":
        return False

    return any(
        safe_string(record.get("invoice_number")).casefold() == invoice_key
        for record in sales_records
    )


def validate_contract(contract, sales_records, sales_order_date):
    if contract is None:
        raise ValueError("No active contract found. Please select a valid contract.")

    contract_id = safe_string(contract.get("contract_id"))
    if contract_id == "":
        raise ValueError("Selected contract is missing Contract ID.")

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
            "Please renew the contract before entering sales data."
        )

    summary = build_contract_summary(contract, sales_records)

    if summary["remaining_qty"] <= 0:
        raise ValueError("The selected contract is already completed. No further sales can be entered.")

    if summary["breach_detected"]:
        raise ValueError("The selected contract is in breach. Resolve the breach before entering sales.")

    return summary


def create_sales_data(args):
    data = parse_sales_args(args)

    if data["invoice_number"] == "":
        raise ValueError("Invoice Number is required.")

    sales_order_date = parse_date_text(
        data["sales_order_date"],
        "Sales Order Date",
        required=True,
    )

    dispatch_date = parse_date_text(
        data["dispatch_date"],
        "Dispatch Date",
        required=False,
    )

    if dispatch_date is not None and dispatch_date < sales_order_date:
        raise ValueError("Dispatch Date cannot be before the Sales Order Date.")

    weight = parse_required_number(data["weight"], "Weight", allow_zero=False)
    gst_percent = parse_percent_number(data["gst_percent"], "GST")
    tcs_percent = parse_tcs_percent(data["tcs_percent"], "TCS 194Q")
    loading_charge = parse_optional_number(data["loading_charge"], "Loading Charge")

    contracts = read_scon()
    sales_records = read_sales()

    contract = find_contract_by_id(contracts, data["contract_id"])
    if contract is None:
        contract = find_contract_for_customer(contracts, data["customer_id"], sales_order_date)

    summary = validate_contract(contract, sales_records, sales_order_date)

    contract_customer_id = safe_string(contract.get("customer_id"))

    if data["customer_id"] and data["customer_id"].casefold() != contract_customer_id.casefold():
        raise ValueError("Customer ID does not match the selected contract.")

    if invoice_exists(sales_records, data["invoice_number"]):
        raise ValueError("Invoice Number already exists in the sales sheet.")

    sales_record = {
        "customer_id": contract_customer_id,
        "contract_id": safe_string(contract.get("contract_id")),
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
        base_price=summary["base_price"],
    )

    final_weight = float(sales_summary["weight"])
    remaining_qty = float(summary["remaining_qty"])

    if final_weight <= 0:
        raise ValueError("Weight must be greater than zero.")

    if final_weight > remaining_qty:
        raise ValueError(
            f"Weight ({final_weight:,.2f}) exceeds the remaining contract quantity "
            f"({remaining_qty:,.2f})."
        )

    append_sales(sales_record)

    return (
        f"Sales entry saved successfully. "
        f"Contract ID: {sales_record['contract_id']} "
        f"| Invoice: {data['invoice_number']} | SO Date: {sales_order_date.strftime(DATE_FORMAT)}"
    )


try:
    print(create_sales_data(sys.argv[1:]))
except Exception as error:
    fail(str(error))