import json
import sys
import warnings
from datetime import date

from contract_engine import (
    ITEM_CODE,
    ITEM_NAME,
    build_contract_summary,
    parse_date,
    safe_float,
    safe_string,
)
from database import (
    PCON_REQUIRED_COLUMNS,
    PCON_SHEET_NAME,
    START_ROW,
    load_database,
    read_purchase,
    row_is_empty,
    safe_cell,
    safe_int,
    safe_number,
)

warnings.filterwarnings("ignore")


STATUS_COLORS = {
    "Active": "#10B981",
    "Upcoming": "#3B82F6",
    "Violated": "#EF4444",
    "Completed": "#8B5CF6",
    "Expired": "#F59E0B",
}


def fail(message):
    sys.stderr.write(f"ERROR: {message}\n")
    sys.exit(1)


def read_pcon_with_rows():
    workbook = load_database()
    try:
        if PCON_SHEET_NAME not in workbook.sheetnames:
            raise ValueError(f"Sheet not found: {PCON_SHEET_NAME}")

        sheet = workbook[PCON_SHEET_NAME]
        contracts = []

        for row in range(START_ROW, sheet.max_row + 1):
            if row_is_empty(sheet, row, PCON_REQUIRED_COLUMNS):
                continue

            contract_id = safe_string(safe_cell(sheet, row, "D"))
            if contract_id == "":
                continue

            contracts.append(
                {
                    "row": row,
                    "sl_no": safe_int(safe_cell(sheet, row, "A")),
                    "vendor_name": safe_string(safe_cell(sheet, row, "B")),
                    "vendor_id": safe_string(safe_cell(sheet, row, "C")),
                    "contract_id": contract_id,
                    "start_date": safe_cell(sheet, row, "E"),
                    "end_date": safe_cell(sheet, row, "F"),
                    "base_price": safe_number(safe_cell(sheet, row, "G")),
                    "agreed_qty": safe_number(safe_cell(sheet, row, "H")),
                    "breach_responsibility": safe_string(safe_cell(sheet, row, "I")),
                    "penalty_discount_percent": safe_number(safe_cell(sheet, row, "J")),
                    "remedy_days": safe_number(safe_cell(sheet, row, "K")),
                    "renewal_reference": safe_string(safe_cell(sheet, row, "L")),
                }
            )

        return contracts
    finally:
        workbook.close()


def successor_lookup(contracts):
    successors = {}

    for contract in contracts:
        previous_contract_id = safe_string(contract.get("renewal_reference"))
        current_contract_id = safe_string(contract.get("contract_id"))

        if previous_contract_id and current_contract_id:
            successors[previous_contract_id.casefold()] = current_contract_id

    return successors


def calculate_status(summary, has_successor):
    today = date.today()

    if has_successor:
        return "Expired"

    start_date = parse_date(summary.get("start_date"))
    end_date = parse_date(summary.get("end_date"))
    remaining_qty = max(safe_float(summary.get("remaining_qty"), 0.0), 0.0)
    breach_detected = bool(summary.get("breach_detected"))

    if remaining_qty <= 0:
        if end_date is not None and today > end_date:
            return "Expired"
        return "Completed"

    if start_date is not None and today < start_date:
        return "Upcoming"

    if end_date is not None and today > end_date and remaining_qty > 0:
        return "Violated"

    if breach_detected and remaining_qty > 0:
        return "Violated"

    return "Active"


def build_compat_contract(contract, summary, has_successor, successor_contract_id):
    dynamic_status = calculate_status(summary, has_successor)
    status_color = STATUS_COLORS.get(dynamic_status, "#64748B")
    is_expired = dynamic_status == "Expired"
    breach_detected = bool(summary["breach_detected"])
    base_price = safe_float(summary["base_price"], 0.0)
    revised_rate = safe_float(summary["revised_rate"], 0.0)
    penalty_value = round(abs(revised_rate - base_price), 2) if breach_detected else 0.0

    compat = {
        "row": contract["row"],
        "sl_no": contract.get("sl_no", 0),
        "vendor_name": summary["vendor_name"],
        "vendor_id": summary["vendor_id"],
        "contract_id": summary["contract_id"],
        "ContractID": summary["contract_id"],
        "item_name": ITEM_NAME,
        "item_code": ITEM_CODE,
        "start_date": summary["start_date"],
        "end_date": summary["end_date"],
        "days_remaining": str(summary["days_remaining"]),
        "days_remaining_value": summary["days_remaining"],
        "deliveries_so_far": str(summary["deliveries_so_far"]),
        "deliveries_so_far_value": summary["deliveries_so_far"],
        "recent_delivery": summary["recent_delivery_date"],
        "recent_delivery_date": summary["recent_delivery_date"],
        "base_price": summary["base_price"],
        "agreed_qty": summary["agreed_qty"],
        "delivered_qty": summary["qty_delivered_so_far"],
        "qty_delivered_so_far": summary["qty_delivered_so_far"],
        "remaining_qty": summary["remaining_qty"],
        "completion_percent": summary["completion_percent"],
        "breach": "YES" if breach_detected else "NO",
        "breach_detected": breach_detected,
        "breach_responsibility": summary["breach_responsibility"],
        "penalty_percent": summary["penalty_discount_percent"],
        "penalty_discount_percent": summary["penalty_discount_percent"],
        "revised_rate": summary["revised_rate"],
        "penalty_value": penalty_value,
        "remedy_days": summary["remedy_days"],
        "remedy_deadline": summary["remedy_deadline"],
        "remedy_status": summary["remedy_status"],
        "renewal_reference": summary["renewal_reference"],
        "renewed_contract_id": successor_contract_id,
        "status": dynamic_status,
        "dynamic_status": dynamic_status,
        "status_color": status_color,
        "is_expired": is_expired,
        "can_edit": not is_expired,
        "can_renew": (
            not has_successor
            and dynamic_status in {"Completed", "Violated"}
        ),
        "is_breach_detected": breach_detected,
        "show_responsibility": breach_detected,
        "show_breach_panel": breach_detected,
    }

    return compat


def read_purchase_contracts():
    contracts = read_pcon_with_rows()
    purchases = read_purchase()
    successors = successor_lookup(contracts)
    output = []

    for contract in contracts:
        summary = build_contract_summary(contract, purchases)
        contract_id = safe_string(summary.get("contract_id"))
        successor_contract_id = successors.get(contract_id.casefold(), "")
        has_successor = successor_contract_id != ""

        output.append(
            build_compat_contract(
                contract,
                summary,
                has_successor,
                successor_contract_id,
            )
        )

    output.sort(
        key=lambda item: (
            item["is_expired"],
            {
                "Active": 0,
                "Upcoming": 1,
                "Violated": 2,
                "Completed": 3,
                "Expired": 4,
            }.get(item["dynamic_status"], 5),
            item["vendor_name"].lower(),
            item["contract_id"].lower(),
        )
    )

    return output


try:
    print(json.dumps(read_purchase_contracts(), indent=4, ensure_ascii=False))
except Exception as error:
    fail(f"Failed to read contracts.\n{str(error)}")
