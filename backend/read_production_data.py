import json
import sys
from typing import Any, Dict, List

from database import (
    read_production,
    get_purchase_by_invoice,
    safe_string,
    safe_number
)
from production_engine import (
    calculate_production_loss,
    calculate_actual_drc,
    calculate_drc_variance,
    format_date
)


def fail(message: str) -> None:
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def build_live_production_snapshot(prod_record: Dict[str, Any]) -> Dict[str, Any]:
    """
    Merges a raw production record with its parent purchase record and calculates
    all factory efficiency metrics on the fly.
    """
    # 1. Base Facts from Production Sheet
    invoice_number = safe_string(prod_record.get("invoice_number"))
    output_qty = safe_number(prod_record.get("output"))

    # 2. Look up the parent Purchase Source
    purchase = get_purchase_by_invoice(invoice_number)
    
    if not purchase:
        input_qty = 0.0
        initial_drc = 0.0
        vendor_name = "Unknown Vendor"
        contract_id = "-"
        purchase_date = "-"
    else:
        # We define input specifically as the Net Weight from the purchase engine logic
        # Net Weight = Received Weight - Number of Bags
        before_unloading = safe_number(purchase.get("before_unloading"))
        carrier_weight = safe_number(purchase.get("carrier_weight"))
        bags = safe_number(purchase.get("number_of_bags"))
        
        received_weight = max(before_unloading - carrier_weight, 0.0)
        input_qty = max(received_weight - bags, 0.0)
        
        # If formula yields 0, fallback to raw invoice weight
        if input_qty <= 0:
            input_qty = safe_number(purchase.get("invoice_weight"))

        initial_drc = safe_number(purchase.get("calculated_drc_percent"))
        vendor_name = safe_string(purchase.get("vendor_id"))  # Replace with actual name mapping if needed
        contract_id = safe_string(purchase.get("contract_id"))
        purchase_date = format_date(purchase.get("purchase_order_date"))

    # 3. Compute Real-Time Physics / Mathematics
    production_loss = calculate_production_loss(input_qty, output_qty)
    actual_drc = calculate_actual_drc(input_qty, output_qty)
    drc_variance = calculate_drc_variance(actual_drc, initial_drc)

    # 4. Construct the C# Bound JSON Layout
    return {
        "row": prod_record.get("row_number", 0),  # Will be populated safely if row exists
        "sl_no": prod_record.get("sl_no", 0),
        "batch_id": safe_string(prod_record.get("batch_id")),
        "invoice_number": invoice_number,
        "contract_id": contract_id,
        "vendor_name": vendor_name,
        "purchase_order_date": purchase_date,
        "production_date": safe_string(prod_record.get("production_date")),
        
        "input_quantity": input_qty,
        "output": output_qty,
        "initial_drc": initial_drc,
        
        # Derived metrics (Sent for UI sanity checks, though C# recalculates on edit)
        "production_loss": production_loss,
        "actual_drc": actual_drc,
        "drc_variance": drc_variance
    }


def main() -> None:
    try:
        raw_production = read_production()
        enriched_batches: List[Dict[str, Any]] = []

        for record in raw_production:
            enriched_batches.append(build_live_production_snapshot(record))

        print(json.dumps(enriched_batches, ensure_ascii=False))

    except Exception as error:
        fail(f"Failed to fetch production timeline: {str(error)}")


if __name__ == "__main__":
    main()