from datetime import date, datetime, timedelta


# =========================================================
# CONSTANTS
# =========================================================

ITEM_NAME = "Natural Rubber-TSNR (ISNR-20)"
ITEM_CODE = "ISNR20"


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


def safe_bool(value, default=False):
    if value is None:
        return default

    if isinstance(value, bool):
        return value

    if isinstance(value, (int, float)):
        return value != 0

    text = safe_string(value).casefold()

    if text in {"1", "true", "yes", "y", "on"}:
        return True

    if text in {"0", "false", "no", "n", "off", ""}:
        return False

    return default


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

def generate_contract_id(customer_id, existing_contracts):
    """
    Brand new contract:
        743268-S0

    Renewals:
        743268-S1
        743268-S2
        743268-S3
    """
    customer_id = normalize_contract_id(customer_id)

    if not customer_id:
        raise ValueError("customer_id cannot be empty.")

    highest_revision = -1
    prefix = f"{customer_id}-S"

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
    return f"{customer_id}-S{next_revision}"


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
# SALES / DISPATCH HELPERS
# =========================================================

def calculate_sales_so_far(contract_id, sales_records):
    contract_id = normalize_contract_id(contract_id)
    if not contract_id:
        return 0

    count = 0
    for record in sales_records or []:
        if normalize_contract_id(record.get("contract_id")) == contract_id:
            count += 1
    return count


def calculate_recent_dispatch_date(contract_id, sales_records):
    contract_id = normalize_contract_id(contract_id)
    if not contract_id:
        return ""

    dispatch_dates = []

    for record in sales_records or []:
        if normalize_contract_id(record.get("contract_id")) != contract_id:
            continue

        dispatch_date = parse_date(record.get("dispatch_date"))
        if dispatch_date is not None:
            dispatch_dates.append(dispatch_date)

    if not dispatch_dates:
        return ""

    return max(dispatch_dates).strftime("%d-%m-%Y")


def calculate_qty_sold_so_far(contract_id, sales_records):
    """
    Qty Sold So Far = SUM(Weight)
    for matching contract_id.
    """
    contract_id = normalize_contract_id(contract_id)
    if not contract_id:
        return 0.0

    total = 0.0

    for record in sales_records or []:
        if normalize_contract_id(record.get("contract_id")) != contract_id:
            continue

        weight = max(safe_float(record.get("weight"), 0.0), 0.0)
        total += weight

    return round(total, 2)


# =========================================================
# CONTRACT QUANTITY LOGIC
# =========================================================

def calculate_remaining_qty(agreed_qty, qty_sold_so_far):
    agreed_qty = max(safe_float(agreed_qty, 0.0), 0.0)
    qty_sold_so_far = max(safe_float(qty_sold_so_far, 0.0), 0.0)

    remaining = agreed_qty - qty_sold_so_far
    return round(max(remaining, 0.0), 2)


def calculate_completion_percent(agreed_qty, qty_sold_so_far):
    agreed_qty = max(safe_float(agreed_qty, 0.0), 0.0)
    qty_sold_so_far = max(safe_float(qty_sold_so_far, 0.0), 0.0)

    if agreed_qty <= 0:
        return 0.0

    completion = (qty_sold_so_far / agreed_qty) * 100.0
    return round(min(max(completion, 0.0), 100.0), 2)


# =========================================================
# BREACH / RATE / REMEDY
# =========================================================

def calculate_breach_detected(end_date, remaining_qty, ignore_remaining_qty=False):
    """
    Breach = YES if:
        today > end_date AND remaining_qty > 0

    If ignore_remaining_qty is enabled, breach is suppressed entirely.
    """
    if safe_bool(ignore_remaining_qty, False):
        return False

    end_date = parse_date(end_date)
    if end_date is None:
        return False

    remaining_qty = max(safe_float(remaining_qty, 0.0), 0.0)
    today = date.today()

    return today > end_date and remaining_qty > 0


def calculate_revised_rate(
    base_price,
    breach_responsibility,
    penalty_discount_percent,
    breach_detected=True
):
    """
    Customer fault -> penalty added.
    Company fault -> discount subtracted.
    Otherwise -> base price.
    """
    base_price = max(safe_float(base_price, 0.0), 0.0)
    percent = safe_percent(penalty_discount_percent)

    if not breach_detected:
        return round(base_price, 2)

    responsibility = safe_string(breach_responsibility).strip().lower()

    if responsibility == "customer":
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


def calculate_remedy_status(remedy_deadline, breach_detected=True):
    if not breach_detected:
        return ""

    deadline = parse_date(remedy_deadline)
    if deadline is None:
        return ""

    today = date.today()
    return "Within Time" if today <= deadline else "Overdue"


# =========================================================
# MASTER CONTRACT SUMMARY
# =========================================================

def build_contract_summary(contract, sales_records):
    """
    Returns a complete contract object with calculated values added.

    Important behavior:
    - ignore_remaining_qty = True means the contract is treated as completed
      for breach/completion purposes.
    - remaining_qty becomes 0 in that case.
    - completion_percent becomes 100 in that case.
    - breach_detected becomes False in that case.
    """
    customer_name = safe_string(contract.get("customer_name"))
    customer_id = safe_string(contract.get("customer_id"))
    contract_id = safe_string(contract.get("contract_id"))

    start_date = parse_date(contract.get("start_date"))
    end_date = parse_date(contract.get("end_date"))

    base_price = safe_float(contract.get("base_price"), 0.0)
    agreed_qty = safe_float(contract.get("agreed_qty"), 0.0)

    breach_responsibility = safe_string(contract.get("breach_responsibility"))
    penalty_discount_percent = safe_percent(contract.get("penalty_discount_percent"))
    remedy_days = int(max(safe_float(contract.get("remedy_days"), 0.0), 0.0))
    renewal_reference = safe_string(contract.get("renewal_reference"))
    ignore_remaining_qty = safe_bool(contract.get("ignore_remaining_qty"), False)

    days_remaining = calculate_days_remaining(end_date)
    sales_so_far = calculate_sales_so_far(contract_id, sales_records)
    recent_dispatch_date = calculate_recent_dispatch_date(contract_id, sales_records)
    qty_sold_so_far = calculate_qty_sold_so_far(contract_id, sales_records)

    actual_remaining_qty = calculate_remaining_qty(agreed_qty, qty_sold_so_far)
    actual_completion_percent = calculate_completion_percent(agreed_qty, qty_sold_so_far)

    if ignore_remaining_qty:
        remaining_qty = 0.0
        completion_percent = 100.0 if agreed_qty > 0 else 0.0
    else:
        remaining_qty = actual_remaining_qty
        completion_percent = actual_completion_percent

    breach_detected = calculate_breach_detected(
        end_date,
        remaining_qty,
        ignore_remaining_qty=ignore_remaining_qty
    )

    revised_rate = calculate_revised_rate(
        base_price,
        breach_responsibility,
        penalty_discount_percent,
        breach_detected=breach_detected
    )

    remedy_deadline = calculate_remedy_deadline(end_date, remedy_days)
    remedy_status = calculate_remedy_status(
        remedy_deadline,
        breach_detected=breach_detected
    )

    return {
        "customer_name": customer_name,
        "customer_id": customer_id,
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
        "ignore_remaining_qty": ignore_remaining_qty,
        "days_remaining": days_remaining,
        "sales_so_far": sales_so_far,
        "recent_dispatch_date": recent_dispatch_date,
        "qty_sold_so_far": round(qty_sold_so_far, 2),
        "actual_remaining_qty": round(actual_remaining_qty, 2),
        "remaining_qty": round(remaining_qty, 2),
        "actual_completion_percent": round(actual_completion_percent, 2),
        "completion_percent": round(completion_percent, 2),
        "breach_detected": breach_detected,
        "revised_rate": revised_rate,
        "remedy_deadline": remedy_deadline,
        "remedy_status": remedy_status,
    }


def build_contract_summaries(contracts, sales_records):
    return [build_contract_summary(contract, sales_records) for contract in (contracts or [])]