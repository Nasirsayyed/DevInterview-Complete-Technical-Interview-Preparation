"""Cross-file consistency checks for the seed packs."""
import glob
import json
import os

ROOT = os.path.join(os.path.dirname(__file__), "..")
load = lambda name: json.load(open(os.path.join(ROOT, name), encoding="utf-8"))
techs = {t["slug"]: set(t["categories"]) for t in load("technologies.json")}
questions = {}
for f in glob.glob(os.path.join(ROOT, "questions", "*.json")):
    for q in json.load(open(f, encoding="utf-8")):
        assert q["key"] not in questions, f"duplicate {q['key']}"
        assert q["tech"] in techs, f"{q['key']}: unknown tech {q['tech']}"
        assert q["category"] in techs[q["tech"]], f"{q['key']}: unknown category {q['category']}"
        if q["type"] in ("MultipleChoice", "SingleChoice", "TrueFalse"):
            assert sum(o["c"] for o in q["options"]) == 1, f"{q['key']}: needs exactly one correct option"
        assert q["answer"] and q["explanation"], f"{q['key']}: missing answer/explanation"
        questions[q["key"]] = q
challenges = {c["key"] for c in load("challenges.json")}
for c in load("challenges.json"):
    assert any(t["sample"] for t in c["tests"]) and any(not t["sample"] for t in c["tests"]), f"{c['key']}: needs sample and hidden tests"
for p in load("learning-paths.json"):
    for m in p["modules"]:
        for l in m["lessons"]:
            for k in l["questions"]:
                assert k in questions, f"{p['key']}: unknown question {k}"
for co in load("companies.json"):
    for col in co["collections"]:
        for k in col["questions"]:
            assert k in questions, f"{co['name']}: unknown question {k}"
        for k in col["challenges"]:
            assert k in challenges, f"{co['name']}: unknown challenge {k}"
for fc in load("flashcards.json"):
    assert fc["tech"] in techs, fc["key"]
print(f"OK: {len(questions)} questions, {len(challenges)} challenges, {len(load('flashcards.json'))} flashcards, "
      f"{len(load('learning-paths.json'))} learning paths, {len(load('companies.json'))} companies")
