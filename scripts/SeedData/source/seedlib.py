"""Helpers for authoring DevInterview seed content in Python and emitting the JSON packs the API seeder reads."""
import json
import os

OUT = os.path.join(os.path.dirname(__file__), "..")


def opt(text, correct=False, why=None):
    return {"t": text, "c": correct, "e": why}


def q(key, tech, category, qtype, difficulty, title, body, options=None, answer="", explanation="",
      tags=None, code=None, lang=None, seconds=75, reference=None, premium=False):
    return {
        "key": key, "tech": tech, "category": category, "type": qtype, "difficulty": difficulty,
        "title": title, "body": body, "code": code, "lang": lang,
        "options": [opt(*o) if isinstance(o, tuple) else o for o in (options or [])],
        "answer": answer, "explanation": explanation, "reference": reference,
        "tags": tags or [], "seconds": seconds, "premium": premium,
    }


def mc(key, tech, category, difficulty, title, body, options, answer, explanation, **kw):
    return q(key, tech, category, "MultipleChoice", difficulty, title, body, options, answer, explanation, **kw)


def write(name, data):
    path = os.path.join(OUT, name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    keys = [d["key"] for d in data if isinstance(d, dict) and "key" in d]
    assert len(keys) == len(set(keys)), f"duplicate keys in {name}"
    with open(path, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    print(f"wrote {name}: {len(data)} items")
