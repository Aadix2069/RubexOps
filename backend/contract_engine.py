from datetime import date, datetime, timedelta


# =========================================================
# CONSTANTS
# =========================================================

ITEM_NAME = "Natural Rubber Field Coagulum"
ITEM_CODE = "NRFC"


# =========================================================
# SAFE HELPERS
# =========================================================

def safe_string(value):
    if value is None:
        return ""
    return str(value).strip()


def safe_float(value, default=0.0):
    try:
        text = safe_string(value)
        if text == "":
            return float(default)
        return float(text.replace(",", ""))
    except Exception:
        return float(default)


def safe_percent(value):
    """
    Accepts either 10 or 0.10 and returns 10.0.
    Also clamps to 0..100.
    """
    number = safe_float(value, 0.0)

    if number < 0:
        number = abs(number)

    if 0 < number <= 1:
        number = number * 100

    return min(number, 100.0)


def parse_date(value):
    if not value:
        return None

    if isinstance(value, datetime):
        return value.date()

    if isinstance(value, date):
        return value

    text = safe_string(value)
    if text == "":
        return None

    for fmt in ("%d-%m-%Y", "%d/%m/%Y", "%Y-%m-%d", "%m/%d/%Y"):
        try:
            return datetime.strptime(text, fmt).date()
        except ValueError:
            pass

    return None


def format_date(value):
    parsed = parse_date(value)
    if parsed is None:
        return ""
    return parsed.strftime("%d-%m-%Y")


def normalize_contract_id(value):
    return safe_string(value)


# =========================================================
# CONTRACT ID GENERATION
# =========================================================

def generate_contract_id(vendor_id, existing_contracts):
    """
    Brand new contract:
        743268-R0

    Renewals:
        743268-R1
        743268-R2
        743268-R3
    """
    vendor_id = normalize_contract_id(vendor_id)

    if not vendor_id:
        raise ValueError("vendor_id cannot be empty.")

    highest_revision = -1
    prefix = f"{vendor_id}-R"

    for contract in existing_contracts or []:
        existing_contract_id = normalize_contract_id(
            contract.get("contract_id")
        )

        if not existing_contract_id.startswith(prefix):
            continue

        suffix = existing_contract_id[len(prefix):].strip()
        if suffix == "":
            continue

        try:
            revision = int(suffix)
            if revision > highest_revision:
                highest_revision = revision
        except ValueError:
            continue

    next_revision = highest_revision + 1
    return f"{vendor_id}-R{next_revision}"


# =========================================================
# DAYS REMAINING
# =========================================================

def calculate_days_remaining(end_date):
    """
    Freezes at 0 once the contract has ended.
    """
    end_date = parse_date(end_date)
    if end_date is None:
        return 0

    today = date.today()
    remaining = (end_date - today).days
    return max(remaining, 0)


# =========================================================
# PURCHASE / DELIVERY HELPERS
# =========================================================

def calculate_deliveries_so_far(contract_id, purchase_records):
    contract_id = normalize_contract_id(contract_id)
    if not contract_id:
        return 0

    count = 0
    for record in purchase_records or []:
        if normalize_contract_id(record.get("contract_id")) == contract_id:
            count += 1
    return count


def calculate_recent_delivery_date(contract_id, purchase_records):
    contract_id = normalize_contract_id(contract_id)
    if not contract_id:
        return ""

    delivery_dates = []

    for record in purchase_records or []:
        if normalize_contract_id(record.get("contract_id")) != contract_id:
            continue

        delivery_date = parse_date(record.get("delivery_date"))
        if delivery_date is not None:
            delivery_dates.append(delivery_date)

    if not delivery_dates:
        return ""

    return max(delivery_dates).strftime("%d-%m-%Y")


def calculate_received_weight(purchase_record):
    """
    Received Weight = Before Unloading - Carrier Weight
    """
    before_unloading = safe_float(purchase_record.get("before_unloading"), 0.0)
    carrier_weight = safe_float(purchase_record.get("carrier_weight"), 0.0)

    received_weight = before_unloading - carrier_weight
    return max(received_weight, 0.0)


def calculate_net_weight(purchase_record):
    """
    Net Weight = Received Weight - Number Of Bags
    """
    received_weight = calculate_received_weight(purchase_record)
    number_of_bags = safe_float(purchase_record.get("number_of_bags"), 0.0)

    net_weight = received_weight - number_of_bags
    return max(net_weight, 0.0)


def calculate_qty_delivered_so_far(contract_id, purchase_records):
    """
    Qty Delivered So Far = SUM(Net Weight)
    for matching contract_id.
    """
    contract_id = normalize_contract_id(contract_id)
    if not contract_id:
        return 0.0

    total = 0.0

    for record in purchase_records or []:
        if normalize_contract_id(record.get("contract_id")) != contract_id:
            continue

        if "net_weight" in record and record.get("net_weight") is not None:
            net_weight = max(safe_float(record.get("net_weight"), 0.0), 0.0)
        else:
            net_weight = calculate_net_weight(record)

        total += net_weight

    return round(total, 2)


# =========================================================
# CONTRACT QUANTITY LOGIC
# =========================================================

def calculate_remaining_qty(agreed_qty, qty_delivered_so_far):
    agreed_qty = max(safe_float(agreed_qty, 0.0), 0.0)
    qty_delivered_so_far = max(safe_float(qty_delivered_so_far, 0.0), 0.0)

    remaining = agreed_qty - qty_delivered_so_far
    return round(max(remaining, 0.0), 2)


def calculate_completion_percent(agreed_qty, qty_delivered_so_far):
    agreed_qty = max(safe_float(agreed_qty, 0.0), 0.0)
    qty_delivered_so_far = max(safe_float(qty_delivered_so_far, 0.0), 0.0)

    if agreed_qty <= 0:
        return 0.0

    completion = (qty_delivered_so_far / agreed_qty) * 100.0
    return round(min(max(completion, 0.0), 100.0), 2)


# =========================================================
# BREACH / RATE / REMEDY
# =========================================================

def calculate_breach_detected(end_date, remaining_qty):
    """
    Breach = YES if:
        today > end_date AND remaining_qty > 0
    """
    end_date = parse_date(end_date)
    if end_date is None:
        return False

    remaining_qty = max(safe_float(remaining_qty, 0.0), 0.0)
    today = date.today()

    return today > end_date and remaining_qty > 0


def calculate_revised_rate(base_price, breach_responsibility, penalty_discount_percent, breach_detected=True):
    """
    Vendor fault -> penalty added.
    Company fault -> discount subtracted.
    Otherwise -> base price.
    """
    base_price = max(safe_float(base_price, 0.0), 0.0)
    percent = safe_percent(penalty_discount_percent)

    if not breach_detected:
        return round(base_price, 2)

    responsibility = safe_string(breach_responsibility).strip().lower()

    if responsibility == "vendor":
        revised = base_price + (base_price * percent / 100.0)
        return round(max(revised, 0.0), 2)

    if responsibility == "company":
        revised = base_price - (base_price * percent / 100.0)
        return round(max(revised, 0.0), 2)

    return round(base_price, 2)


def calculate_remedy_deadline(end_date, remedy_days):
    end_date = parse_date(end_date)
    if end_date is None:
        return ""

    remedy_days = int(max(safe_float(remedy_days, 0.0), 0.0))
    deadline = end_date + timedelta(days=remedy_days)
    return deadline.strftime("%d-%m-%Y")


def calculate_remedy_status(remedy_deadline):
    deadline = parse_date(remedy_deadline)
    if deadline is None:
        return ""

    today = date.today()
    return "Within Time" if today <= deadline else "Overdue"


# =========================================================
# MASTER CONTRACT SUMMARY
# =========================================================

def build_contract_summary(contract, purchase_records):
    """
    Returns a complete contract object with calculated values added.
    Status is intentionally not handled here.
    """
    vendor_name = safe_string(contract.get("vendor_name"))
    vendor_id = safe_string(contract.get("vendor_id"))
    contract_id = safe_string(contract.get("contract_id"))

    start_date = parse_date(contract.get("start_date"))
    end_date = parse_date(contract.get("end_date"))

    base_price = safe_float(contract.get("base_price"), 0.0)
    agreed_qty = safe_float(contract.get("agreed_qty"), 0.0)

    breach_responsibility = safe_string(contract.get("breach_responsibility"))
    penalty_discount_percent = safe_percent(contract.get("penalty_discount_percent"))
    remedy_days = int(max(safe_float(contract.get("remedy_days"), 0.0), 0.0))
    renewal_reference = safe_string(contract.get("renewal_reference"))

    days_remaining = calculate_days_remaining(end_date)
    deliveries_so_far = calculate_deliveries_so_far(contract_id, purchase_records)
    recent_delivery_date = calculate_recent_delivery_date(contract_id, purchase_records)
    qty_delivered_so_far = calculate_qty_delivered_so_far(contract_id, purchase_records)
    remaining_qty = calculate_remaining_qty(agreed_qty, qty_delivered_so_far)
    completion_percent = calculate_completion_percent(agreed_qty, qty_delivered_so_far)
    breach_detected = calculate_breach_detected(end_date, remaining_qty)

    revised_rate = calculate_revised_rate(
        base_price,
        breach_responsibility,
        penalty_discount_percent,
        breach_detected=breach_detected
    )

    remedy_deadline = calculate_remedy_deadline(end_date, remedy_days)
    remedy_status = calculate_remedy_status(remedy_deadline) if breach_detected else ""

    return {
        "vendor_name": vendor_name,
        "vendor_id": vendor_id,
        "contract_id": contract_id,
        "item_name": ITEM_NAME,
        "item_code": ITEM_CODE,
        "start_date": format_date(start_date),
        "end_date": format_date(end_date),
        "base_price": round(base_price, 2),
        "agreed_qty": round(agreed_qty, 2),
        "breach_responsibility": breach_responsibility,
        "penalty_discount_percent": penalty_discount_percent,
        "remedy_days": remedy_days,
        "renewal_reference": renewal_reference,
        "days_remaining": days_remaining,
        "deliveries_so_far": deliveries_so_far,
        "recent_delivery_date": recent_delivery_date,
        "qty_delivered_so_far": qty_delivered_so_far,
        "remaining_qty": remaining_qty,
        "completion_percent": completion_percent,
        "breach_detected": breach_detected,
        "revised_rate": revised_rate,
        "remedy_deadline": remedy_deadline,
        "remedy_status": remedy_status,
    }


def build_contract_summaries(contracts, purchase_records):
    return [build_contract_summary(contract, purchase_records) for contract in (contracts or [])]
