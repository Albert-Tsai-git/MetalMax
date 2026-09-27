# -*- coding: utf-8 -*-
"""
数值调整小工具：按“表:ID:列=值”修改 UNITY/Data 下的 CSV（保留注释行与列顺序）。
用法：python tools/tune.py enemies:ENM_Bounty_IronCrab:hp=450 parts:WPN_Flamethrower:attack=24
表名为 Data 下的文件名（不含 .csv）；第一列为 ID。
"""
import csv
import io
import os
import sys

DATA = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "UNITY", "Data")


def main(argv: list[str]) -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    edits: dict[str, list[tuple[str, str, str]]] = {}
    for a in argv:
        table, rest = a.split(":", 1)
        rid, kv = rest.rsplit(":", 1)
        col, val = kv.split("=", 1)
        edits.setdefault(table, []).append((rid, col, val))
    for table, items in edits.items():
        path = os.path.join(DATA, f"{table}.csv")
        rows = list(csv.reader(open(path, encoding="utf-8-sig", newline="")))
        header = rows[0]
        for rid, col, val in items:
            if col not in header:
                sys.exit(f"[tune] {table} 没有列 {col}")
            ci = header.index(col)
            hit = [r for r in rows[1:] if r and r[0] == rid]
            if not hit:
                sys.exit(f"[tune] {table} 没有 ID {rid}")
            old = hit[0][ci]
            hit[0][ci] = val
            print(f"[tune] {table}.{rid}.{col}: {old} → {val}")
        out = io.StringIO()
        csv.writer(out, lineterminator="\n").writerows(rows)
        open(path, "w", encoding="utf-8", newline="").write(out.getvalue())


if __name__ == "__main__":
    main(sys.argv[1:])
