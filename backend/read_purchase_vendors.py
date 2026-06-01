import json
import sys
from datetime import date

from purchase_contract_engine import ITEM_CODE, ITEM_NAME, build_contract_summary, parse_date, safe_string
from database import read_pcon, read_purchase


def fail(message):
    print(f"ERROR: {message}", file=sys.stderr)
    sys.exit(1)


def successor_lookup(contracts):
    successors = set()

    for contract in contracts:
        previous_contract_id = safe_string(contract.get("renewal_reference"))
        if previous_contract_id:
            successors.add(previous_contract_id.casefold())

    return successors


def is_active_for_purchase(summary, renewed_contract_ids):
    contract_id = safe_string(summary.get("contract_id"))
    if contract_id == "" or contract_id.casefold() in renewed_contract_ids:
        return False

    start_date = parse_date(summary.get("start_date"))
    end_date = parse_date(summary.get("end_date"))

    if start_date is None or end_date is None:
        return False

    today = date.today()

    if not (start_date <= today <= end_date):
        return False

    if summary.get("remaining_qty", 0) <= 0:
        return False

    if summary.get("breach_detected"):
        return False

    return True


def read_active_contracts():
    contracts = read_pcon()
    purchases = read_purchase()
    renewed_contract_ids = successor_lookup(contracts)
    active_contracts = []

    for contract in contracts:
        summary = build_contract_summary(contract, purchases)

        if not is_active_for_purchase(summary, renewed_contract_ids):
            continue

        active_contracts.append(
            {
                "VendorName": summary["vendor_name"],
                "VendorID": summary["vendor_id"],
                "ContractID": summary["contract_id"],
                "ItemName": ITEM_NAME,
                "ItemCode": ITEM_CODE,
                "BaseRate": str(summary["base_price"]),
                "StartDate": summary["start_date"],
                "EndDate": summary["end_date"],
                "RemainingQty": summary["remaining_qty"],
                "CompletionPercent": summary["completion_percent"],
                "vendor_name": summary["vendor_name"],
                "vendor_id": summary["vendor_id"],
                "contract_id": summary["contract_id"],
                "item_name": ITEM_NAME,
                "item_code": ITEM_CODE,
                "base_rate": summary["base_price"],
                "start_date": summary["start_date"],
                "end_date": summary["end_date"],
                "remaining_qty": summary["remaining_qty"],
                "completion_percent": summary["completion_percent"],
            }
        )

    active_contracts.sort(
        key=lambda item: (
            item["VendorName"].lower(),
            item["ContractID"].lower(),
        )
    )

    return active_contracts


try:
    print(json.dumps(read_active_contracts(), ensure_ascii=False))
except Exception as error:
    fail(str(error))
