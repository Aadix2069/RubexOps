import sys
from datetime import datetime

from database import (
    append_production,
    generate_production_batch_id,
    get_purchase_by_invoice,
    get_purchase_initial_drc,
    get_purchase_quantity,
    production_invoice_exists,
    safe_date,
    safe_number,
    safe_string,
)


DATE_FORMAT = "%d-%m-%Y"


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def parse_date_text(value: str, field_name: str) -> datetime.date:
    text = safe_string(value)
    if text == "":
        raise ValueError(f"{field_name} is required.")

    for fmt in (DATE_FORMAT, "%Y-%m-%d", "%d/%m/%Y"):
        try:
            return datetime.strptime(text, fmt).date()
        except ValueError:
            pass

    raise ValueError(f"{field_name} must be in dd-MM-yyyy format.")


def parse_number(value: str, field_name: str, allow_zero: bool = False) -> float:
    number = safe_number(value)
    if allow_zero:
        if number < 0:
            raise ValueError(f"{field_name} cannot be negative.")
    else:
        if number <= 0:
            raise ValueError(f"{field_name} must be greater than zero.")
    return number


def build_production_record(args: list[str]) -> dict:
    # Expected arguments from WPF:
    # 0: production_date
    # 1: invoice_number
    # 2: output
    # Optional:
    # 3: batch_id
    # 4: input_quantity (override)
    # 5: initial_drc (override)
    if len(args) < 3:
        raise ValueError(
            "Expected at least 3 arguments: production_date, invoice_number, output."
        )

    production_date_text = safe_string(args[0])
    invoice_number = safe_string(args[1])
    output_qty = parse_number(args[2], "Output")
    batch_id_override = safe_string(args[3]) if len(args) >= 4 else ""
    input_quantity_override = safe_string(args[4]) if len(args) >= 5 else ""
    initial_drc_override = safe_string(args[5]) if len(args) >= 6 else ""

    production_date = parse_date_text(production_date_text, "Production Date")

    if invoice_number == "":
        raise ValueError("Invoice Number is required.")

    purchase = get_purchase_by_invoice(invoice_number)
    if purchase is None:
        raise ValueError(
            "No matching purchase invoice found. Please select a valid invoice."
        )

    if production_invoice_exists(invoice_number):
        raise ValueError(
            "This purchase invoice has already been used for a production batch."
        )

    purchase_date_text = safe_string(purchase.get("purchase_order_date"))
    if purchase_date_text:
        try:
            purchase_date = parse_date_text(purchase_date_text, "Purchase Order Date")
            if production_date < purchase_date:
                raise ValueError(
                    "Production Date cannot be earlier than the Purchase Order Date."
                )
        except ValueError:
            # If the purchase date is not parseable, ignore the comparison and continue.
            pass

    contract_id = safe_string(purchase.get("contract_id"))
    if contract_id == "":
        raise ValueError("Contract ID could not be resolved from the selected invoice.")

    batch_id = batch_id_override or generate_production_batch_id(contract_id)

    input_quantity = safe_number(input_quantity_override)
    if input_quantity <= 0:
        input_quantity = get_purchase_quantity(purchase)

    if input_quantity <= 0:
        raise ValueError(
            "Input Quantity could not be determined from the selected purchase invoice."
        )

    if output_qty > input_quantity:
        raise ValueError("Output cannot be greater than Input Quantity.")

    production_loss = input_quantity - output_qty
    if production_loss < 0:
        raise ValueError("Production Loss cannot be negative.")

    initial_drc = safe_number(initial_drc_override)
    if initial_drc <= 0:
        initial_drc = get_purchase_initial_drc(purchase)

    if initial_drc < 0:
        raise ValueError("Initial DRC cannot be negative.")

    actual_drc = (output_qty / input_quantity) * 100
    drc_variance = actual_drc - initial_drc

    return {
        "batch_id": batch_id,
        "production_date": production_date,
        "invoice_number": invoice_number,
        "input_quantity": input_quantity,
        "output": output_qty,
        "production_loss": production_loss,
        "initial_drc": initial_drc,
        "actual_drc": actual_drc,
        "drc_variance": drc_variance,
    }


def create_production(args: list[str]) -> str:
    record = build_production_record(args)

    sl_no = append_production(record)

    return (
        "Production batch saved successfully. "
        f"Sl.No: {sl_no} | Batch ID: {record['batch_id']} | "
        f"Invoice: {record['invoice_number']} | "
        f"Input: {record['input_quantity']:.2f} | "
        f"Output: {record['output']:.2f} | "
        f"Loss: {record['production_loss']:.2f} | "
        f"Initial DRC: {record['initial_drc']:.2f}% | "
        f"Actual DRC: {record['actual_drc']:.2f}% | "
        f"Variance: {record['drc_variance']:+.2f}%"
    )


if __name__ == "__main__":
    try:
        print(create_production(sys.argv[1:]))
    except Exception as error:
        fail(str(error))
