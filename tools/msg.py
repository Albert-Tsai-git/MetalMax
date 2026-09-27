# -*- coding: utf-8 -*-
"""
Claude ↔ Codex 通讯与资源锁（本机共享工作目录，数据在 comms/，不入 Git）。

消息：
  python tools/msg.py send <我> <对方> <类型> "内容" [--re 消息号]   类型：info / request / question / reply / handoff
  python tools/msg.py inbox <我>            未读消息 + 我发出但对方未回复的 request/question
  python tools/msg.py ack <我> <消息号>...   标记已读（all = 全部）
  python tools/msg.py log [条数]            最近消息（默认 20）
资源锁（目前只有 unity）：
  python tools/msg.py lock <我> unity "用途"
  python tools/msg.py unlock <我> unity
  python tools/msg.py locks
"""
import datetime
import json
import os
import sys

ROOT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "comms")
LOG = os.path.join(ROOT, "messages.jsonl")
AGENTS = ("Claude", "Codex", "User")
TYPES = ("info", "request", "question", "reply", "handoff")
NEEDS_REPLY = ("request", "question")
RESOURCES = ("unity",)


def now() -> str:
    return datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")


def agent(name: str) -> str:
    for a in AGENTS:
        if a.lower() == name.lower():
            return a
    sys.exit(f"[msg] 未知身份 {name}，可选：{'/'.join(AGENTS)}")


def load() -> list[dict]:
    if not os.path.exists(LOG):
        return []
    with open(LOG, encoding="utf-8") as f:
        return [json.loads(line) for line in f if line.strip()]


def read_set(me: str) -> set[int]:
    path = os.path.join(ROOT, f"read_{me}.json")
    if not os.path.exists(path):
        return set()
    with open(path, encoding="utf-8") as f:
        return set(json.load(f))


def save_read(me: str, ids: set[int]) -> None:
    with open(os.path.join(ROOT, f"read_{me}.json"), "w", encoding="utf-8") as f:
        json.dump(sorted(ids), f)


def fmt(m: dict) -> str:
    re = f" re#{m['re']}" if m.get("re") else ""
    return f"#{m['id']} [{m['time']}] {m['from']}→{m['to']} {m['type']}{re}\n    {m['text']}"


def send(me: str, to: str, typ: str, text: str, re: int | None) -> None:
    if typ not in TYPES:
        sys.exit(f"[msg] 未知类型 {typ}，可选：{'/'.join(TYPES)}")
    # 以独占锁文件串行化编号分配，防止双方同时发送时编号重复
    guard = os.path.join(ROOT, ".send.lock")
    for _ in range(200):
        try:
            fd = os.open(guard, os.O_CREAT | os.O_EXCL)
            break
        except FileExistsError:
            import time
            time.sleep(0.05)
    else:
        sys.exit(f"[msg] 发送锁被占用，若确认无人发送可删除 {guard}")
    try:
        msgs = load()
        m = {"id": (msgs[-1]["id"] + 1) if msgs else 1, "time": now(),
             "from": me, "to": to, "type": typ, "text": text}
        if re:
            m["re"] = re
        with open(LOG, "a", encoding="utf-8") as f:
            f.write(json.dumps(m, ensure_ascii=False) + "\n")
    finally:
        os.close(fd)
        os.remove(guard)
    print(f"[msg] 已发送 #{m['id']}")


def inbox(me: str) -> None:
    msgs = load()
    seen = read_set(me)
    unread = [m for m in msgs if m["to"] == me and m["id"] not in seen]
    replied = {m["re"] for m in msgs if m.get("re")}
    waiting = [m for m in msgs if m["from"] == me and m["type"] in NEEDS_REPLY and m["id"] not in replied]
    print(f"[msg] {me} 未读 {len(unread)} 条")
    for m in unread:
        print(fmt(m))
    if waiting:
        print(f"[msg] 等待对方回复 {len(waiting)} 条")
        for m in waiting:
            print(fmt(m))
    locks()


def ack(me: str, ids: list[str]) -> None:
    seen = read_set(me)
    if ids == ["all"]:
        seen |= {m["id"] for m in load() if m["to"] == me}
    else:
        seen |= {int(i.lstrip("#")) for i in ids}
    save_read(me, seen)
    print("[msg] 已标记已读")


def lock_path(res: str) -> str:
    if res not in RESOURCES:
        sys.exit(f"[msg] 未知资源 {res}，可选：{'/'.join(RESOURCES)}")
    return os.path.join(ROOT, f"{res}.lock")


def lock(me: str, res: str, purpose: str) -> None:
    path = lock_path(res)
    try:
        fd = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY)
    except FileExistsError:
        with open(path, encoding="utf-8") as f:
            sys.exit(f"[msg] {res} 已被占用：{f.read()}")
    with os.fdopen(fd, "w", encoding="utf-8") as f:
        f.write(f"{me}（{purpose}，{now()}）")
    print(f"[msg] {me} 已占用 {res}")


def unlock(me: str, res: str) -> None:
    path = lock_path(res)
    if not os.path.exists(path):
        sys.exit(f"[msg] {res} 当前未被占用")
    with open(path, encoding="utf-8") as f:
        holder = f.read()
    if not holder.startswith(me) and me != "User":
        sys.exit(f"[msg] {res} 由 {holder} 占用，不能代为释放")
    os.remove(path)
    print(f"[msg] {me} 已释放 {res}")


def locks() -> None:
    for res in RESOURCES:
        path = os.path.join(ROOT, f"{res}.lock")
        if os.path.exists(path):
            with open(path, encoding="utf-8") as f:
                print(f"[msg] 锁 {res}：{f.read()}")
        else:
            print(f"[msg] 锁 {res}：空闲")


def main(argv: list[str]) -> None:
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
    os.makedirs(ROOT, exist_ok=True)
    if not argv:
        sys.exit(__doc__)
    cmd, args = argv[0], argv[1:]
    re = None
    if "--re" in args:
        i = args.index("--re")
        re = int(args[i + 1].lstrip("#"))
        args = args[:i] + args[i + 2:]
    if cmd == "send" and len(args) == 4:
        send(agent(args[0]), agent(args[1]), args[2], args[3], re)
    elif cmd == "inbox" and len(args) == 1:
        inbox(agent(args[0]))
    elif cmd == "ack" and len(args) >= 2:
        ack(agent(args[0]), args[1:])
    elif cmd == "log":
        for m in load()[-int(args[0] if args else 20):]:
            print(fmt(m))
    elif cmd == "lock" and len(args) == 3:
        lock(agent(args[0]), args[1], args[2])
    elif cmd == "unlock" and len(args) == 2:
        unlock(agent(args[0]), args[1])
    elif cmd == "locks":
        locks()
    else:
        sys.exit(__doc__)


if __name__ == "__main__":
    main(sys.argv[1:])
