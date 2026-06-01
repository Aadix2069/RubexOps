import sys
from datetime import date, datetime

from purchase_contract_engine import build_contract_summary, parse_date, safe_string
from database import append_purchase, read_pcon, read_purchase
from purchase_inventory_engine import build_purchase_summary


DATE_FORMAT = "%d-%m-%Y"


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def looks_like_contract_id(value):
    text = safe_string(value)
    return "-R" in text.upper()


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

def parse_tds_percent(value, field_name="TDS 194Q"):
    """
    TDS is entered literally.

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

def parse_purchase_args(args):
    if len(args) == 12:
        vendor_id = safe_string(args[0])
        contract_id = ""
        values = args[1:]
    elif len(args) == 13:
        if looks_like_contract_id(args[0]) and not looks_like_contract_id(args[1]):
            contract_id = safe_string(args[0])
            vendor_id = safe_string(args[1])
        else:
            vendor_id = safe_string(args[0])
            contract_id = safe_string(args[1])

        values = args[2:]
    else:
        raise ValueError(
            "Expected 12 legacy arguments or 13 arguments including Contract ID."
        )

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
        "vendor_id": vendor_id,
        "contract_id": contract_id,
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

    if vendor_key == "":
        return None

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


def invoice_exists(purchases, invoice_number):
    invoice_key = safe_string(invoice_number).casefold()

    if invoice_key == "":
        return False

    return any(
        safe_string(purchase.get("invoice_number")).casefold() == invoice_key
        for purchase in purchases
    )


def validate_contract(contract, purchases, purchase_order_date):
    if contract is None:
        raise ValueError("No active contract found. Please select a valid contract.")

    contract_id = safe_string(contract.get("contract_id"))
    if contract_id == "":
        raise ValueError("Selected contract is missing Contract ID.")

    start_date = parse_date(contract.get("start_date"))
    end_date = parse_date(contract.get("end_date"))

    if start_date is None or end_date is None:
        raise ValueError(f"Selected contract {contract_id} has invalid dates.")

    if purchase_order_date < start_date or purchase_order_date > end_date:
        raise ValueError(
            "Purchase Order Date is outside the selected contract period "
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
            "Please renew the contract before entering purchases."
        )

    summary = build_contract_summary(contract, purchases)

    if summary["remaining_qty"] <= 0:
        raise ValueError("The selected contract is already completed. No further purchases can be entered.")

    if summary["breach_detected"]:
        raise ValueError("The selected contract is in breach. Resolve the breach before entering purchases.")

    return summary


def create_purchase_data(args):
    data = parse_purchase_args(args)

    if data["invoice_number"] == "":
        raise ValueError("Invoice Number is required.")

    purchase_order_date = parse_date_text(
        data["purchase_order_date"],
        "Purchase Order Date",
        required=True,
    )

    delivery_date = parse_date_text(
        data["delivery_date"],
        "Delivery Date",
        required=False,
    )

    if delivery_date is not None and delivery_date < purchase_order_date:
        raise ValueError("Delivery Date cannot be before the Purchase Order Date.")

    invoice_weight = parse_optional_number(data["invoice_weight"], "Invoice Weight")
    before_unloading = parse_required_number(data["before_unloading"], "Before Unloading", allow_zero=False)
    carrier_weight = parse_required_number(data["carrier_weight"], "Carrier Weight", allow_zero=True)
    number_of_bags = parse_optional_number(data["number_of_bags"], "Number Of Bags")
    calculated_drc_percent = parse_percent_number(data["calculated_drc_percent"], "Calculated DRC")
    gst_percent = parse_percent_number(data["gst_percent"], "GST")
    tds_percent = parse_tds_percent(data["tds_percent"],"TDS 194Q")
    unloading_charge = parse_optional_number(data["unloading_charge"], "Unloading Charge")

    if carrier_weight > before_unloading:
        raise ValueError("Carrier Weight cannot be greater than Before Unloading weight.")

    contracts = read_pcon()
    purchases = read_purchase()

    contract = find_contract_by_id(contracts, data["contract_id"])
    if contract is None:
        contract = find_contract_for_vendor(contracts, data["vendor_id"], purchase_order_date)

    summary = validate_contract(contract, purchases, purchase_order_date)

    contract_vendor_id = safe_string(contract.get("vendor_id"))

    if data["vendor_id"] and data["vendor_id"].casefold() != contract_vendor_id.casefold():
        raise ValueError("Vendor ID does not match the selected contract.")

    if invoice_exists(purchases, data["invoice_number"]):
        raise ValueError("Invoice Number already exists in the purchase sheet.")

    purchase_record = {
        "vendor_id": contract_vendor_id,
        "contract_id": safe_string(contract.get("contract_id")),
        "invoice_number": data["invoice_number"],
        "purchase_order_date": purchase_order_date,
        "delivery_date": delivery_date or "",
        "invoice_weight": invoice_weight,
        "before_unloading": before_unloading,
        "carrier_weight": carrier_weight,
        "number_of_bags": number_of_bags,
        "calculated_drc_percent": calculated_drc_percent,
        "gst_percent": gst_percent,
        "tds_percent": tds_percent,
        "unloading_charge": unloading_charge,
    }

    purchase_summary = build_purchase_summary(
        purchase_record,
        base_price=summary["base_price"],
    )

    received_weight = float(purchase_summary["received_weight"])
    net_weight = float(purchase_summary["net_weight"])
    remaining_qty = float(summary["remaining_qty"])

    if received_weight <= 0:
        raise ValueError("Received Weight must be greater than zero.")

    if net_weight <= 0:
        raise ValueError("Net Weight must be greater than zero.")

    if net_weight > remaining_qty:
        raise ValueError(
            f"Net Weight ({net_weight:,.2f}) exceeds the remaining contract quantity "
            f"({remaining_qty:,.2f})."
        )

    if invoice_weight > 0 and net_weight > 0:
        difference = abs(invoice_weight - net_weight)
        if difference > 2000 and difference > (invoice_weight * 0.25):
            raise ValueError(
                f"Invoice Weight and Net Weight differ significantly.\n\n"
                f"Invoice Weight: {invoice_weight:,.2f} kg\n"
                f"Calculated Net Weight: {net_weight:,.2f} kg\n"
                f"Difference: {difference:,.2f} kg\n\n"
                f"Please verify the values before saving."
            )

    append_purchase(purchase_record)

    return (
        f"Purchase entry saved successfully. "
        f"Contract ID: {purchase_record['contract_id']} "
        f"| Invoice: {data['invoice_number']} | PO Date: {purchase_order_date.strftime(DATE_FORMAT)}"
    )


try:
    print(create_purchase_data(sys.argv[1:]))
except Exception as error:
    fail(str(error))