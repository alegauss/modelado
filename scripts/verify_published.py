"""Read back a published model repository and check every file against its manifest.

Downloads manifest.json from the repository at a revision, then asks Hugging Face for each file
the manifest lists without downloading it: a HEAD on its resolve URL answers, before the
redirect, X-Linked-Size and X-Linked-ETag, which for a file stored in LFS is its SHA-256. A file
whose answer lacks them (a meta.json, kept in git) is downloaded and hashed instead. Nothing is
written: a failure names each file and what differed (MOD26).

    python scripts\\verify_published.py
    python scripts\\verify_published.py --repo me/my-models --revision 4faae170814766b06b4d4b7eafa3cef466f7efad

publish_models.py runs the same check after its second commit, before it copies the manifest
into the app. Standard library only; --token-file or HF_TOKEN for a private repository.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import urllib.error
import urllib.request
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent
RECORD = SCRIPTS / "published-models.json"
SHA256 = re.compile(r"^[0-9a-f]{64}$")
REDIRECTS = (301, 302, 303, 307, 308)


class VerifyError(Exception):
    """The repository could not be read, or does not hold what its manifest says."""


class _NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def _request(url: str, token: str | None, method: str = "GET") -> urllib.request.Request:
    headers = {"User-Agent": "modelado-verify"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return urllib.request.Request(url, headers=headers, method=method)


def head(url: str, token: str | None = None) -> dict[str, str]:
    """The first answer's headers, lowercased, without following its redirect."""
    opener = urllib.request.build_opener(_NoRedirect)
    try:
        with opener.open(_request(url, token, "HEAD")) as response:
            return {k.lower(): v for k, v in response.headers.items()}
    except urllib.error.HTTPError as error:
        if error.code in REDIRECTS:
            return {k.lower(): v for k, v in error.headers.items()}
        raise VerifyError(f"{url} answered {error.code}") from error


def get(url: str, token: str | None = None) -> bytes:
    try:
        with urllib.request.urlopen(_request(url, token)) as response:
            return response.read()
    except urllib.error.HTTPError as error:
        raise VerifyError(f"{url} answered {error.code}") from error


def resolve_base(repo_id: str, revision: str) -> str:
    return f"https://huggingface.co/{repo_id}/resolve/{revision}"


def files_of(manifest: dict) -> list[tuple[str, dict]]:
    """Every file the manifest lists, as (label, entry with url, bytes and sha256)."""
    listed = []
    for model in manifest["models"]:
        listed.append((f"{model['name']} meta.json", model["meta"]))
        listed += [(f"{model['name']} {variant}", entry) for variant, entry in model["variants"].items()]
    return listed


def check_file(label: str, entry: dict, head=head, get=get) -> str | None:
    """What differs about one published file, or None when it is what the manifest says."""
    answered = head(entry["url"])
    etag = answered.get("x-linked-etag", "").strip('"').lower()
    size = answered.get("x-linked-size")
    if SHA256.match(etag) and size is not None:
        if int(size) != entry["bytes"]:
            return f"{label}: {entry['url']} is {size} bytes; the manifest says {entry['bytes']}"
        if etag != entry["sha256"]:
            return f"{label}: {entry['url']} has SHA-256 {etag}; the manifest says {entry['sha256']}"
        return None
    body = get(entry["url"])
    if len(body) != entry["bytes"]:
        return f"{label}: {entry['url']} is {len(body)} bytes; the manifest says {entry['bytes']}"
    digest = hashlib.sha256(body).hexdigest()
    if digest != entry["sha256"]:
        return f"{label}: {entry['url']} has SHA-256 {digest}; the manifest says {entry['sha256']}"
    return None


def verify(manifest: dict, head=head, get=get) -> list[str]:
    """Every file that differs from the manifest, named; empty when all of them match."""
    return [problem for label, entry in files_of(manifest) if (problem := check_file(label, entry, head, get))]


def verify_repo(repo_id: str, revision: str, token: str | None = None) -> tuple[dict, list[str]]:
    """The repository's manifest at `revision`, and what differs in it."""
    manifest = json.loads(get(f"{resolve_base(repo_id, revision)}/manifest.json", token))
    return manifest, verify(manifest, lambda url: head(url, token), lambda url: get(url, token))


def recorded() -> tuple[str, str]:
    """The repository and manifest commit publish_models.py last recorded."""
    record = json.loads(RECORD.read_text(encoding="utf-8"))
    return record["repo"], record["manifest_commit"]


def main(argv: list[str] | None = None) -> int:
    repo, commit = recorded()
    parser = argparse.ArgumentParser(description="Check a published model repository against its own manifest.")
    parser.add_argument("--repo", default=repo, help=f"the model repository (default: {repo}, from {RECORD.name})")
    parser.add_argument("--revision", help=f"the commit or branch whose manifest.json is read (default: {commit[:12]} for {repo}, else main)")
    parser.add_argument("--token-file", type=Path, help="a read token, for a private repository; HF_TOKEN otherwise")
    args = parser.parse_args(argv)
    revision = args.revision or (commit if args.repo == repo else "main")
    token = args.token_file.read_text(encoding="utf-8").strip() if args.token_file else os.environ.get("HF_TOKEN") or None
    try:
        manifest, problems = verify_repo(args.repo, revision, token)
    except VerifyError as error:
        print(f"ERROR {error}", file=sys.stderr)
        return 1
    for problem in problems:
        print(f"ERROR {problem}", file=sys.stderr)
    checked = len(files_of(manifest))
    if problems:
        print(f"{len(problems)} of {checked} files in {args.repo}@{revision[:12]} differ from its manifest", file=sys.stderr)
        return 1
    print(f"{args.repo}@{revision[:12]}: all {checked} files match manifest {manifest['version']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
