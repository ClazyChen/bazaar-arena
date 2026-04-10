from __future__ import annotations

import json
import sqlite3
from datetime import datetime, timezone
from urllib.parse import unquote

from flask import Flask, abort, jsonify, request, send_file

from bazaararena_api.db import default_db_path, get_connection, repo_root
from bazaararena_api.deck_rules import max_slots_for_level


def _utc_now_iso() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


def _row_to_dict(row: sqlite3.Row) -> dict:
    return {k: row[k] for k in row.keys()}


def tier_allowed_for_level(tier: int, level: int) -> bool:
    """与 legacy Deck.TierAllowedForLevel 一致（tier 为 0..4）。传奇档位是否可用由物品的 min_tier 另行约束。"""
    if tier == 0:
        return True
    if tier == 1:
        return level >= 3
    if tier == 2:
        return level >= 7
    if tier == 3:
        return level >= 10
    if tier == 4:
        return True
    return False


def create_app() -> Flask:
    app = Flask(__name__)

    @app.get("/health")
    def health():
        return {"ok": True}

    @app.get("/static/pictures/webp/<path:relative>")
    def serve_webp(relative: str):
        rel = unquote(relative)
        if not rel or ".." in rel:
            abort(404)
        root = (repo_root() / "pictures" / "webp").resolve()
        target = (root / rel).resolve()
        try:
            target.relative_to(root)
        except ValueError:
            abort(404)
        if not target.is_file():
            abort(404)
        return send_file(target)

    @app.get("/api/items")
    def list_items():
        hero = request.args.get("hero", "").strip()
        size_q = request.args.get("size", "").strip().lower()
        tier_q = request.args.get("tier", "").strip()

        conn = get_connection()
        try:
            q = "SELECT name, hero, size, min_tier, desc, tags_json, source_yaml, schema_version, tooltip_attrs_json FROM items WHERE 1=1"
            params: list[object] = []
            if hero and hero != "all":
                q += " AND hero = ?"
                params.append(hero)
            if size_q and size_q != "all":
                smap = {"small": 1, "medium": 2, "large": 3}
                if size_q in smap:
                    q += " AND size = ?"
                    params.append(smap[size_q])
            if tier_q != "" and tier_q != "all":
                try:
                    t = int(tier_q)
                    if 0 <= t <= 4:
                        q += " AND min_tier = ?"
                        params.append(t)
                except ValueError:
                    pass
            q += " ORDER BY name"
            cur = conn.execute(q, params)
            rows = [_row_to_dict(r) for r in cur.fetchall()]
            for r in rows:
                try:
                    r["tags"] = json.loads(r.pop("tags_json") or "[]")
                except json.JSONDecodeError:
                    r["tags"] = []
                raw_tip = r.pop("tooltip_attrs_json", None)
                try:
                    r["tooltip_attrs"] = json.loads(raw_tip) if raw_tip else {}
                except json.JSONDecodeError:
                    r["tooltip_attrs"] = {}
            return jsonify({"items": rows})
        finally:
            conn.close()

    @app.get("/api/collections")
    def list_collections():
        conn = get_connection()
        try:
            cur = conn.execute(
                "SELECT id, name, sort_order, created_at FROM deck_collections ORDER BY sort_order, id"
            )
            return jsonify({"collections": [_row_to_dict(r) for r in cur.fetchall()]})
        finally:
            conn.close()

    @app.post("/api/collections")
    def create_collection():
        body = request.get_json(force=True, silent=True) or {}
        name = (body.get("name") or "").strip()
        if not name:
            return jsonify({"error": "name required"}), 400
        conn = get_connection()
        try:
            cur = conn.execute("SELECT COALESCE(MAX(sort_order), -1) + 1 FROM deck_collections")
            sort_order = int(cur.fetchone()[0])
            conn.execute(
                "INSERT INTO deck_collections (name, sort_order, created_at) VALUES (?, ?, ?)",
                (name, sort_order, _utc_now_iso()),
            )
            conn.commit()
            cid = conn.execute("SELECT last_insert_rowid()").fetchone()[0]
            row = conn.execute(
                "SELECT id, name, sort_order, created_at FROM deck_collections WHERE id = ?",
                (cid,),
            ).fetchone()
            return jsonify(_row_to_dict(row)), 201
        finally:
            conn.close()

    @app.patch("/api/collections/<int:cid>")
    def patch_collection(cid: int):
        body = request.get_json(force=True, silent=True) or {}
        conn = get_connection()
        try:
            row = conn.execute(
                "SELECT id, name, sort_order FROM deck_collections WHERE id = ?", (cid,)
            ).fetchone()
            if not row:
                return jsonify({"error": "not found"}), 404
            name = body.get("name")
            sort_order = body.get("sort_order")
            if name is not None:
                conn.execute("UPDATE deck_collections SET name = ? WHERE id = ?", (str(name).strip(), cid))
            if sort_order is not None:
                conn.execute(
                    "UPDATE deck_collections SET sort_order = ? WHERE id = ?", (int(sort_order), cid)
                )
            conn.commit()
            row = conn.execute(
                "SELECT id, name, sort_order, created_at FROM deck_collections WHERE id = ?", (cid,)
            ).fetchone()
            return jsonify(_row_to_dict(row))
        finally:
            conn.close()

    @app.delete("/api/collections/<int:cid>")
    def delete_collection(cid: int):
        conn = get_connection()
        try:
            cur = conn.execute("DELETE FROM deck_collections WHERE id = ?", (cid,))
            conn.commit()
            if cur.rowcount == 0:
                return jsonify({"error": "not found"}), 404
            return ("", 204)
        finally:
            conn.close()

    @app.get("/api/collections/<int:cid>/decks")
    def list_decks(cid: int):
        conn = get_connection()
        try:
            exists = conn.execute(
                "SELECT 1 FROM deck_collections WHERE id = ?", (cid,)
            ).fetchone()
            if not exists:
                return jsonify({"error": "collection not found"}), 404
            cur = conn.execute(
                "SELECT id, collection_id, name, player_level, sort_order FROM decks WHERE collection_id = ? ORDER BY sort_order, id",
                (cid,),
            )
            return jsonify({"decks": [_row_to_dict(r) for r in cur.fetchall()]})
        finally:
            conn.close()

    @app.post("/api/collections/<int:cid>/decks")
    def create_deck(cid: int):
        body = request.get_json(force=True, silent=True) or {}
        name = (body.get("name") or "新卡组").strip() or "新卡组"
        level = body.get("player_level", 5)
        try:
            player_level = int(level)
        except (TypeError, ValueError):
            return jsonify({"error": "invalid player_level"}), 400
        if player_level < 1 or player_level > 20:
            return jsonify({"error": "player_level out of range"}), 400

        conn = get_connection()
        try:
            exists = conn.execute(
                "SELECT 1 FROM deck_collections WHERE id = ?", (cid,)
            ).fetchone()
            if not exists:
                return jsonify({"error": "collection not found"}), 404
            cur = conn.execute(
                "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM decks WHERE collection_id = ?",
                (cid,),
            )
            sort_order = int(cur.fetchone()[0])
            conn.execute(
                "INSERT INTO decks (collection_id, name, player_level, sort_order) VALUES (?, ?, ?, ?)",
                (cid, name, player_level, sort_order),
            )
            conn.commit()
            did = conn.execute("SELECT last_insert_rowid()").fetchone()[0]
            row = conn.execute(
                "SELECT id, collection_id, name, player_level, sort_order FROM decks WHERE id = ?",
                (did,),
            ).fetchone()
            return jsonify(_row_to_dict(row)), 201
        finally:
            conn.close()

    @app.patch("/api/collections/<int:cid>/decks/reorder")
    def reorder_decks(cid: int):
        body = request.get_json(force=True, silent=True) or {}
        order = body.get("order")
        if not isinstance(order, list):
            return jsonify({"error": "order must be a list of deck ids"}), 400
        conn = get_connection()
        try:
            exists = conn.execute(
                "SELECT 1 FROM deck_collections WHERE id = ?", (cid,)
            ).fetchone()
            if not exists:
                return jsonify({"error": "collection not found"}), 404
            cur = conn.execute("SELECT id FROM decks WHERE collection_id = ?", (cid,))
            existing = {int(r[0]) for r in cur.fetchall()}
            ids = [int(x) for x in order]
            if set(ids) != existing:
                return jsonify({"error": "order must contain exactly all deck ids in collection"}), 400
            for i, did in enumerate(ids):
                conn.execute("UPDATE decks SET sort_order = ? WHERE id = ? AND collection_id = ?", (i, did, cid))
            conn.commit()
            return jsonify({"ok": True})
        finally:
            conn.close()

    @app.patch("/api/decks/<int:did>")
    def patch_deck(did: int):
        body = request.get_json(force=True, silent=True) or {}
        conn = get_connection()
        try:
            row = conn.execute(
                "SELECT id, collection_id, name, player_level, sort_order FROM decks WHERE id = ?",
                (did,),
            ).fetchone()
            if not row:
                return jsonify({"error": "not found"}), 404
            name = body.get("name")
            sort_order = body.get("sort_order")
            player_level = body.get("player_level")
            if name is not None:
                conn.execute("UPDATE decks SET name = ? WHERE id = ?", (str(name).strip(), did))
            if sort_order is not None:
                conn.execute("UPDATE decks SET sort_order = ? WHERE id = ?", (int(sort_order), did))
            if player_level is not None:
                pl = int(player_level)
                if pl < 1 or pl > 20:
                    return jsonify({"error": "player_level out of range"}), 400
                conn.execute("UPDATE decks SET player_level = ? WHERE id = ?", (pl, did))
            conn.commit()
            row = conn.execute(
                "SELECT id, collection_id, name, player_level, sort_order FROM decks WHERE id = ?",
                (did,),
            ).fetchone()
            return jsonify(_row_to_dict(row))
        finally:
            conn.close()

    @app.delete("/api/decks/<int:did>")
    def delete_deck(did: int):
        conn = get_connection()
        try:
            cur = conn.execute("DELETE FROM decks WHERE id = ?", (did,))
            conn.commit()
            if cur.rowcount == 0:
                return jsonify({"error": "not found"}), 404
            return ("", 204)
        finally:
            conn.close()

    @app.post("/api/decks/<int:did>/duplicate")
    def duplicate_deck(did: int):
        conn = get_connection()
        try:
            row = conn.execute(
                "SELECT id, collection_id, name, player_level, sort_order FROM decks WHERE id = ?",
                (did,),
            ).fetchone()
            if not row:
                return jsonify({"error": "not found"}), 404
            cid = int(row["collection_id"])
            base_name = str(row["name"])
            cur = conn.execute(
                "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM decks WHERE collection_id = ?", (cid,)
            )
            sort_order = int(cur.fetchone()[0])
            new_name = base_name + " 副本"
            conn.execute(
                "INSERT INTO decks (collection_id, name, player_level, sort_order) VALUES (?, ?, ?, ?)",
                (cid, new_name, int(row["player_level"]), sort_order),
            )
            conn.commit()
            new_id = int(conn.execute("SELECT last_insert_rowid()").fetchone()[0])
            slots = conn.execute(
                "SELECT position, item_name, tier FROM deck_slots WHERE deck_id = ? ORDER BY position",
                (did,),
            ).fetchall()
            for s in slots:
                conn.execute(
                    "INSERT INTO deck_slots (deck_id, position, item_name, tier) VALUES (?, ?, ?, ?)",
                    (new_id, int(s["position"]), str(s["item_name"]), int(s["tier"])),
                )
            conn.commit()
            r = conn.execute(
                "SELECT id, collection_id, name, player_level, sort_order FROM decks WHERE id = ?",
                (new_id,),
            ).fetchone()
            return jsonify(_row_to_dict(r)), 201
        finally:
            conn.close()

    @app.get("/api/decks/<int:did>/slots")
    def get_slots(did: int):
        conn = get_connection()
        try:
            deck = conn.execute(
                "SELECT id, player_level FROM decks WHERE id = ?", (did,)
            ).fetchone()
            if not deck:
                return jsonify({"error": "not found"}), 404
            cur = conn.execute(
                "SELECT position, item_name, tier FROM deck_slots WHERE deck_id = ? ORDER BY position",
                (did,),
            )
            slots = [{"position": int(r["position"]), "item_name": r["item_name"], "tier": int(r["tier"])} for r in cur]
            return jsonify(
                {
                    "deck_id": did,
                    "player_level": int(deck["player_level"]),
                    "max_slots": max_slots_for_level(int(deck["player_level"])),
                    "slots": slots,
                }
            )
        finally:
            conn.close()

    @app.put("/api/decks/<int:did>/slots")
    def put_slots(did: int):
        body = request.get_json(force=True, silent=True) or {}
        slots_in = body.get("slots")
        if not isinstance(slots_in, list):
            return jsonify({"error": "slots must be a list"}), 400

        conn = get_connection()
        try:
            deck = conn.execute(
                "SELECT id, player_level FROM decks WHERE id = ?", (did,)
            ).fetchone()
            if not deck:
                return jsonify({"error": "not found"}), 404
            player_level = int(deck["player_level"])
            budget = max_slots_for_level(player_level)

            # Load item sizes and min_tier
            item_rows = conn.execute("SELECT name, size, min_tier FROM items").fetchall()
            item_by_name = {r["name"]: (int(r["size"]), int(r["min_tier"])) for r in item_rows}

            total = 0
            normalized: list[tuple[str, int]] = []
            for i, entry in enumerate(slots_in):
                if not isinstance(entry, dict):
                    return jsonify({"error": f"slots[{i}] invalid"}), 400
                item_name = entry.get("item_name")
                tier = entry.get("tier")
                if item_name is None or tier is None:
                    return jsonify({"error": f"slots[{i}] needs item_name and tier"}), 400
                item_name = str(item_name)
                try:
                    tier_i = int(tier)
                except (TypeError, ValueError):
                    return jsonify({"error": f"slots[{i}] invalid tier"}), 400
                if item_name not in item_by_name:
                    return jsonify({"error": f"unknown item: {item_name}"}), 400
                size_i, min_tier = item_by_name[item_name]
                if tier_i < min_tier or tier_i > 4:
                    return jsonify({"error": f"tier out of range for {item_name}"}), 400
                if tier_i == 4 and min_tier < 4:
                    return jsonify({"error": f"cannot use legendary tier for {item_name}"}), 400
                if not tier_allowed_for_level(tier_i, player_level):
                    return jsonify({"error": f"tier {tier_i} not allowed for player level {player_level}"}), 400
                total += size_i
                if total > budget:
                    return jsonify({"error": "exceeds slot budget for this level"}), 400
                normalized.append((item_name, tier_i))

            conn.execute("DELETE FROM deck_slots WHERE deck_id = ?", (did,))
            for pos, (item_name, tier_i) in enumerate(normalized):
                conn.execute(
                    "INSERT INTO deck_slots (deck_id, position, item_name, tier) VALUES (?, ?, ?, ?)",
                    (did, pos, item_name, tier_i),
                )
            conn.commit()
            return jsonify({"ok": True, "max_slots": budget, "used_slots": total})
        finally:
            conn.close()

    @app.get("/api/meta")
    def meta():
        return jsonify({"db_path": str(default_db_path())})

    return app


app = create_app()
