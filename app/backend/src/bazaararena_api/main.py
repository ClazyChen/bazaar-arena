from __future__ import annotations

from flask import Flask


def create_app() -> Flask:
    app = Flask(__name__)

    @app.get("/health")
    def health():
        return {"ok": True}

    return app


app = create_app()

