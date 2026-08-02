"""Deterministically convert the approved regulations workbook to packaged JSON."""
from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, date
from pathlib import Path

import openpyxl

REG_HEADERS = [
    "ID", "地区", "国家/层级", "专题", "文件层级", "文件原名", "中文名称",
    "编号/年份", "适用范围与用途", "效力/采用方式", "官方全文或检索页", "PDF/下载入口",
    "下载与版权说明", "核验日期", "备注/检索关键词",
]
PORTAL_HEADERS = ["地区", "官方平台", "覆盖内容", "网址", "使用说明"]
REG_KEYS = [
    "Id", "Region", "JurisdictionLevel", "Topic", "DocumentLevel", "OriginalTitle",
    "ChineseTitle", "IdentifierOrYear", "ScopeAndPurpose", "EffectOrAdoption",
    "OfficialUrl", "DownloadUrl", "DownloadAndCopyrightNote", "VerifiedDate", "SearchKeywords",
]
PORTAL_KEYS = ["PortalId", "Region", "PlatformName", "Coverage", "Url", "UsageNote"]


def clean(value):
    if value is None:
        return None
    if isinstance(value, (datetime, date)):
        return value.strftime("%Y-%m-%d")
    value = str(value).replace("\r\n", "\n").replace("\r", "\n").strip()
    return value or None


def url(value):
    value = clean(value)
    if value is None:
        return None
    value = re.sub(r"\s+", "", value)
    if not re.fullmatch(r"https?://[^\s]+", value, re.IGNORECASE):
        raise ValueError(f"Invalid URL: {value}")
    return value


def rows(ws):
    return list(ws.iter_rows(values_only=True))


def convert(source: Path) -> dict:
    book = openpyxl.load_workbook(source, read_only=True, data_only=True)
    reg_rows = rows(book["法规总表"])
    portal_rows = rows(book["官方入口"])
    if list(reg_rows[0]) != REG_HEADERS or list(portal_rows[0]) != PORTAL_HEADERS:
        raise ValueError("Workbook headers do not match the approved schema")

    entries = []
    for raw in reg_rows[1:]:
        values = [clean(v) for v in raw]
        item = dict(zip(REG_KEYS, values))
        item["Id"] = int(raw[0])
        item["OfficialUrl"] = url(raw[10])
        item["DownloadUrl"] = url(raw[11])
        item["VerifiedDate"] = clean(raw[13])
        entries.append(item)

    portals = []
    for index, raw in enumerate(portal_rows[1:], start=1):
        values = [clean(v) for v in raw]
        item = dict(zip(PORTAL_KEYS[1:], values))
        item = {"PortalId": f"portal-{index:02d}", **item}
        item["Url"] = url(raw[3])
        portals.append(item)

    notes = []
    for raw in rows(book["字段说明"])[1:]:
        key, value = clean(raw[0]), clean(raw[1])
        if key and value:
            notes.append({"Topic": key, "Note": value})

    result = {
        "DataVersion": 1,
        "SourceName": source.name,
        "SourceVerifiedDate": "2026-07-31",
        "GeneratedAt": "2026-07-31T00:00:00Z",
        "Entries": entries,
        "OfficialPortals": portals,
        "FieldNotes": notes,
    }
    validate(result)
    return result


def validate(data: dict) -> None:
    entries = data["Entries"]
    if len(entries) != 221 or len(data["OfficialPortals"]) != 20:
        raise ValueError(f"Unexpected counts: entries={len(entries)}, portals={len(data['OfficialPortals'])}")
    if len({x["Id"] for x in entries}) != len(entries):
        raise ValueError("Duplicate regulation IDs")
    expected = {"中国": 82, "日本": 42, "美国": 53, "欧盟/欧洲": 44}
    actual = {}
    for item in entries:
        actual[item["Region"]] = actual.get(item["Region"], 0) + 1
        if not item["OriginalTitle"] or not item["ScopeAndPurpose"]:
            raise ValueError(f"Missing required field for ID {item['Id']}")
        if not item["OfficialUrl"] and not item["DownloadUrl"]:
            raise ValueError(f"Missing official link for ID {item['Id']}")
        if item["VerifiedDate"] != "2026-07-31":
            raise ValueError(f"Unexpected verification date for ID {item['Id']}")
    if actual != expected:
        raise ValueError(f"Unexpected region counts: {actual}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    data = convert(args.source)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"entries": len(data["Entries"]), "portals": len(data["OfficialPortals"]), "output": str(args.output)}, ensure_ascii=False))


if __name__ == "__main__":
    main()
