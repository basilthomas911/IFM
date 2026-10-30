"""Targeted, compare-and-swap repair of historical position currency P&L.

Dry-run by default. Uses the local PostgreSQL container's socket authentication;
no password is read or stored. Never rewrites source events or active positions.
Requires msgpack and lz4. Backups contain financial data and belong outside Git.
"""
import argparse
import datetime as dt
import hashlib
import json
import pathlib
import re
import subprocess
from decimal import Decimal
from zoneinfo import ZoneInfo

import lz4.block
import msgpack


def unpack(payload):
    value = msgpack.unpackb(bytes.fromhex(payload), strict_map_key=False)
    if isinstance(value, list) and isinstance(value[0], msgpack.ExtType) and value[0].code == 98:
        lengths = msgpack.Unpacker()
        lengths.feed(value[0].data)
        value = msgpack.unpackb(b"".join(lz4.block.decompress(block, uncompressed_size=size)
                                       for block, size in zip(value[1:], lengths)), strict_map_key=False)
    return value


def day(number):
    return dt.date.fromordinal(number + 1)


def value_date(timestamp):
    local = timestamp.to_datetime().astimezone(ZoneInfo("America/New_York"))
    if local.hour == 17 or local.weekday() == 5 or local.weekday() == 6 and local.hour < 18 or local.weekday() == 4 and local.hour >= 18:
        raise ValueError("Execution timestamp lies outside a supported futures session")
    return local.date() + dt.timedelta(days=local.hour >= 18)


def update_sql(row, before, after):
    # All interpolated keys come from PostgreSQL and are validated as typed values.
    position = str(__import__("uuid").UUID(row["positionid"]))
    sequence = int(row["positionsequence"])
    at = dt.datetime.fromisoformat(row["asofutc"]).isoformat()
    return f"""DO $repair$ DECLARE affected integer; BEGIN
UPDATE trade.strategy_position_history SET payload=decode('{after}','hex')
WHERE positionid='{position}' AND asofutc='{at}'::timestamptz
AND positionsequence={sequence} AND payload=decode('{before}','hex');
GET DIAGNOSTICS affected = ROW_COUNT;
IF affected <> 1 THEN RAISE EXCEPTION 'Historical row changed during repair'; END IF;
END $repair$;
"""


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("portfolio", "fund", "order", "trade"):
        parser.add_argument("--" + name, required=True, type=int)
    parser.add_argument("--before", required=True, type=dt.date.fromisoformat,
                        help="Repair only value dates strictly earlier than this date")
    parser.add_argument("--container", default="ifm_db")
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--backup", type=pathlib.Path)
    args = parser.parse_args()
    if min(args.portfolio, args.fund, args.order, args.trade) <= 0 or not re.fullmatch(r"[A-Za-z0-9_-]+", args.container):
        parser.error("Positive identities and a valid container name are required")
    if args.apply and args.backup is None:
        parser.error("--apply requires an explicit --backup directory")

    def sql(text):
        return subprocess.run(["docker", "exec", "-i", args.container, "psql", "-X", "-U", "postgres",
                               "-d", "ifm_application_development", "-v", "ON_ERROR_STOP=1", "-At"],
                              input=text, capture_output=True, text=True, check=True).stdout.strip()

    scope = f"portfolioid={args.portfolio} AND fundid={args.fund} AND orderid={args.order} AND tradeid={args.trade}"
    source = json.loads(sql(f"""BEGIN READ ONLY;
SELECT json_build_object(
'definition',(SELECT encode(payload,'hex') FROM trade.established_trade WHERE {scope}),
'history',(SELECT json_agg(row_to_json(h)) FROM (
SELECT positionid::text,asofutc,positionsequence,phase,encode(payload,'hex') AS payload
FROM trade.strategy_position_history WHERE positionid IN
(SELECT positionid FROM trade.strategy_position_current WHERE {scope}) ORDER BY asofutc,positionsequence) h),
'plans',(SELECT json_object_agg(valuedate::text,json_build_object('dailyPnl',dailypnl::text,'multiplier',contractmultiplier::text)) FROM (
SELECT DISTINCT ON (valuedate) valuedate,dailypnl,contractmultiplier FROM trade_plan.iron_condor_trade_plan
WHERE {scope} ORDER BY valuedate,planrevision DESC) p));
ROLLBACK;""").removeprefix("BEGIN\n").removesuffix("\nROLLBACK"))
    if not source["definition"]:
        raise ValueError("Established trade evidence is missing")
    definition = unpack(source["definition"])
    contracts = {leg[0] for leg in definition[7]}
    captured = {Decimal(plan["multiplier"]) for plan in (source["plans"] or {}).values() if plan["multiplier"] is not None}
    if not contracts or len(captured) != 1 or min(captured) <= 0:
        raise ValueError("Stored pricing plans must confirm a common positive contract multiplier")
    multiplier = next(iter(captured))
    opened = value_date(definition[11])
    opening_commission = Decimal(definition[10])
    changes = []
    for row in source["history"] or []:
        position = unpack(row["payload"])
        date = day(position[13])
        if date >= args.before:
            continue
        points = Decimal(position[18])
        commission = opening_commission if date == opened else Decimal(0)
        commission += sum((Decimal(fill[7]) for fill in position[12] if value_date(fill[8]) == date), Decimal(0))
        currency = points * multiplier - commission
        plan = (source["plans"] or {}).get(str(date))
        if row["phase"] != "Open" and (plan is None or plan["dailyPnl"] is None or Decimal(plan["dailyPnl"]) != currency):
            raise ValueError(f"{date}: independently stored plan does not confirm currency P&L {currency}")
        # Preserve the legacy point field. Append independently named currency evidence.
        while len(position) < 26:
            position.append(None)
        for leg in position[6]:
            if leg[0] not in contracts:
                raise ValueError("Historical leg is missing from established trade evidence")
            while len(leg) < 17:
                leg.append(None)
            leg[16] = str(multiplier)
        position[22:26] = [str(currency), str(multiplier), str(opening_commission), opened.toordinal() - 1]
        after = msgpack.packb(position, use_bin_type=True).hex()
        if unpack(row["payload"]) == position:
            continue
        changes.append(dict(row, before=row["payload"], after=after, valueDate=str(date),
                            points=str(points), currency=str(currency), commission=str(commission),
                            originalSha256=hashlib.sha256(bytes.fromhex(row["payload"])).hexdigest()))
        print(f"{date} {row['phase']:12} points={points:>8} multiplier={multiplier} commission={commission} currency={currency}")
    print(f"{'Apply' if args.apply else 'Dry run'}: {len(changes)} historical rows; active/current positions and source events excluded")
    if not args.apply or not changes:
        return
    args.backup.mkdir(parents=True, exist_ok=True)
    backup = args.backup / "position-currency-backup.json"
    with backup.open("x", encoding="utf-8") as output:
        json.dump(dict(scope=scope, createdUtc=dt.datetime.now(dt.timezone.utc).isoformat(),
                       definition=source["definition"], rows=changes), output, indent=2)
    (args.backup / "restore.sql").write_text("BEGIN;\n" + "".join(update_sql(row, row["after"], row["before"]) for row in changes) + "COMMIT;\n", encoding="utf-8")
    sql("BEGIN; SET LOCAL lock_timeout='2s'; SET LOCAL statement_timeout='15s';\n" +
        "".join(update_sql(row, row["before"], row["after"]) for row in changes) + "COMMIT;")
    # Check exact post-images; do not infer success from affected-row counts alone.
    for row in changes:
        position = str(__import__("uuid").UUID(row["positionid"]))
        observed = sql(f"SELECT encode(payload,'hex') FROM trade.strategy_position_history WHERE positionid='{position}' AND asofutc='{dt.datetime.fromisoformat(row['asofutc']).isoformat()}'::timestamptz AND positionsequence={int(row['positionsequence'])};")
        if observed != row["after"]:
            raise ValueError("Post-write verification failed; inspect the guarded backup before further changes")
    print(f"Verified {len(changes)} exact post-images. Backup and guarded restore: {args.backup.resolve()}")


if __name__ == "__main__":
    main()
