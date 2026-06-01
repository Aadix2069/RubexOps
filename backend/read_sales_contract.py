import json
import sys
import warnings
from datetime import date

from sales_contract_engine import (
    ITEM_CODE,
    ITEM_NAME,
    build_contract_summary,
    parse_date,
    safe_float,
    safe_string,
)
from database import (
    SCON_REQUIRED_COLUMNS,
    SCON_SHEET_NAME,
    START_ROW,
    load_database,
    read_sales,
    row_is_empty,
    safe_bool,
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


def read_scon_with_rows():
    workbook = load_database()
    try:
        if SCON_SHEET_NAME not in workbook.sheetnames:
            raise ValueError(f"Sheet not found: {SCON_SHEET_NAME}")

        sheet = workbook[SCON_SHEET_NAME]
        contracts = []

        for row in range(START_ROW, sheet.max_row + 1):
            if row_is_empty(sheet, row, SCON_REQUIRED_COLUMNS):
                continue

            contract_id = safe_string(safe_cell(sheet, row, "D"))
            if contract_id == "":
                continue

            contracts.append(
                {
                    "row": row,
                    "sl_no": safe_int(safe_cell(sheet, row, "A")),
                    "customer_name": safe_string(safe_cell(sheet, row, "B")),
                    "customer_id": safe_string(safe_cell(sheet, row, "C")),
                    "contract_id": contract_id,
                    "start_date": safe_cell(sheet, row, "E"),
                    "end_date": safe_cell(sheet, row, "F"),
                    "base_price": safe_number(safe_cell(sheet, row, "G")),
                    "agreed_qty": safe_number(safe_cell(sheet, row, "H")),
                    "breach_responsibility": safe_string(safe_cell(sheet, row, "I")),
                    "penalty_discount_percent": safe_number(safe_cell(sheet, row, "J")),
                    "remedy_days": safe_number(safe_cell(sheet, row, "K")),
                    "renewal_reference": safe_string(safe_cell(sheet, row, "L")),
                    "ignore_remaining_qty": safe_bool(safe_cell(sheet, row, "M")),
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
    breach_detected = bool(summary.get("breach_detected"))
    base_price = safe_float(summary.get("base_price"), 0.0)
    revised_rate = safe_float(summary.get("revised_rate"), 0.0)
    penalty_value = round(abs(revised_rate - base_price), 2) if breach_detected else 0.0

    compat = {
        "row": contract["row"],
        "sl_no": contract.get("sl_no", 0),
        "customer_name": summary.get("customer_name", ""),
        "customer_id": summary.get("customer_id", ""),
        "contract_id": summary.get("contract_id", ""),
        "ContractID": summary.get("contract_id", ""),
        "item_name": ITEM_NAME,
        "item_code": ITEM_CODE,
        "start_date": summary.get("start_date", ""),
        "end_date": summary.get("end_date", ""),
        "days_remaining": str(summary.get("days_remaining", 0)),
        "days_remaining_value": summary.get("days_remaining", 0),
        "sales_so_far": str(summary.get("sales_so_far", 0)),
        "sales_so_far_value": summary.get("sales_so_far", 0),
        "recent_dispatch": summary.get("recent_dispatch_date", ""),
        "recent_dispatch_date": summary.get("recent_dispatch_date", ""),
        "base_price": summary.get("base_price", 0.0),
        "agreed_qty": summary.get("agreed_qty", 0.0),
        "sold_qty": summary.get("qty_sold_so_far", 0.0),
        "qty_sold_so_far": summary.get("qty_sold_so_far", 0.0),
        "remaining_qty": summary.get("remaining_qty", 0.0),
        "completion_percent": summary.get("completion_percent", 0.0),
        "ignore_remaining_qty": bool(summary.get("ignore_remaining_qty", False)),
        "breach": "YES" if breach_detected else "NO",
        "breach_detected": breach_detected,
        "breach_responsibility": summary.get("breach_responsibility", ""),
        "penalty_percent": summary.get("penalty_discount_percent", 0.0),
        "penalty_discount_percent": summary.get("penalty_discount_percent", 0.0),
        "revised_rate": summary.get("revised_rate", 0.0),
        "penalty_value": penalty_value,
        "remedy_days": summary.get("remedy_days", 0.0),
        "remedy_deadline": summary.get("remedy_deadline", ""),
        "remedy_status": summary.get("remedy_status", ""),
        "renewal_reference": summary.get("renewal_reference", ""),
        "renewed_contract_id": successor_contract_id,
        "status": dynamic_status,
        "dynamic_status": dynamic_status,
        "status_color": status_color,
        "is_expired": is_expired,
        "can_edit": not is_expired,
        "can_renew": (
            not has_successor
            and dynamic_status in {"Completed", "Expired"}
        ),
        "is_breach_detected": breach_detected,
        "show_responsibility": breach_detected,
        "show_breach_panel": breach_detected,
    }

    return compat


def read_sales_contracts():
    contracts = read_scon_with_rows()
    sales_data = read_sales()
    successors = successor_lookup(contracts)
    output = []

    for contract in contracts:
        summary = build_contract_summary(contract, sales_data)
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
            item["customer_name"].lower(),
            item["contract_id"].lower(),
        )
    )

    return output


try:
    print(json.dumps(read_sales_contracts(), indent=4, ensure_ascii=False))
except Exception as error:
    fail(f"Failed to read sales contracts.\n{str(error)}")