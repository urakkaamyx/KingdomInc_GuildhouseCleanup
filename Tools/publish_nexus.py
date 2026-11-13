from pathlib import Path
import base64, hashlib, html, json, os, re, time, urllib.request, urllib.error

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Nexus"
RELEASES = ROOT / "Releases"
RECEIPT = OUT / "PUBLICATION.json"

PAGE = 17
MOD_ID = "46785078755345"
CHAIN_ID = "8312469"
VERSION = "1.1.0"
ARCHIVE_NAME = f"KingdomInc-GuildhouseCleanup-{VERSION}.zip"
ARCHIVE_PATH = RELEASES / ARCHIVE_NAME

KEY = os.environ.get("NEXUSMODS_API_KEY")
if not KEY:
    raise RuntimeError("NEXUSMODS_API_KEY environment variable is missing.")

HEADERS = {
    "apikey": KEY,
    "User-Agent": f"KingdomInc-GuildhouseCleanup-Release/{VERSION}",
    "Accept": "application/json",
    "Content-Type": "application/json",
}

def api(path, method="GET", body=None):
    req = urllib.request.Request(
        "https://api.nexusmods.com/v3" + path,
        method=method,
        headers=HEADERS,
        data=json.dumps(body).encode() if body is not None else None,
    )
    try:
        with urllib.request.urlopen(req, timeout=45) as res:
            raw = res.read()
            return json.loads(raw) if raw else {"status": res.status}
    except urllib.error.HTTPError as ex:
        raise RuntimeError(f"{method} {path}: HTTP {ex.code}: {ex.read().decode()}") from None

def norm(text):
    return re.sub(r"<br\s*/?>", "", html.unescape(text or "")).replace("\r\n", "\n").strip()

def main():
    print(f"Resolving Nexus Mod {PAGE}...")
    page_data = api(f"/games/kingdominc/mods/{PAGE}")["data"]
    assert str(page_data["game_scoped_id"]) == str(PAGE)
    print(f"Mod ID: {page_data['id']}, Name: {page_data['name']}, Status: {page_data['status']}, Version: {page_data['version']}")

    verification_path = RELEASES / "RELEASE_VERIFICATION.json"
    verification = json.loads(verification_path.read_text(encoding="utf-8-sig"))
    assert verification["version"] == VERSION

    assert ARCHIVE_PATH.exists(), f"Archive not found: {ARCHIVE_PATH}"
    payload = ARCHIVE_PATH.read_bytes()
    sha256 = hashlib.sha256(payload).hexdigest().lower()
    md5 = hashlib.md5(payload).digest()
    assert sha256 == verification["sha256"].lower()

    record = {
        "name": "Guild House & Armory Cleanup",
        "version": VERSION,
        "date": time.strftime("%Y-%m-%d"),
        "pageUrl": f"https://www.nexusmods.com/kingdominc/mods/{PAGE}",
        "status": "Published",
        "gameDomain": "kingdominc",
        "nexusGameId": 10893,
        "gameScopedModId": PAGE,
        "archive": str(ARCHIVE_PATH),
        "archiveSha256": sha256,
        "bytes": len(payload),
        "dllSha256": verification["dllSha256"],
        "runtimeChecksPassed": verification["runtimeChecksPassed"],
        "cleanExtractionPassed": True,
    }

    # 1. Check existing versions
    print("Checking versions on file chain...")
    versions_res = api(f"/mod-files/{CHAIN_ID}/versions")
    versions = versions_res.get("data", {}).get("versions", [])
    matching = [v for v in versions if v["version"] == VERSION]

    if matching:
        print(f"Version {VERSION} already exists on Nexus file chain!")
        latest_ver = matching[0]
    else:
        previous = next(v for v in versions if v["category"] == "main")
        print(f"Previous main version: {previous['version']} (id: {previous['id']})")
        print(f"Requesting upload ticket for {ARCHIVE_NAME} ({len(payload)} bytes)...")
        upload_ticket = api(
            "/uploads",
            "POST",
            {
                "filename": ARCHIVE_NAME,
                "size_bytes": len(payload),
                "md5": md5.hex(),
            },
        )["data"]

        upload_id = upload_ticket["id"]
        presigned_url = upload_ticket["presigned_url"]
        record["uploadId"] = upload_id

        print("Uploading binary to presigned storage URL...")
        put_req = urllib.request.Request(
            presigned_url,
            method="PUT",
            data=payload,
            headers={
                "Content-Type": "application/octet-stream",
                "Content-Disposition": f'attachment; filename="{ARCHIVE_NAME}"',
                "Content-MD5": base64.b64encode(md5).decode(),
            },
        )
        with urllib.request.urlopen(put_req, timeout=90) as put_res:
            assert put_res.status in (200, 201, 204)
        print("Binary upload complete.")

        print("Finalising upload...")
        api(f"/uploads/{upload_id}/finalise", "POST")

        print("Waiting for upload processing...")
        for attempt in range(25):
            state = api(f"/uploads/{upload_id}")["data"]["state"]
            print(f"  Upload state: {state} (poll {attempt + 1})")
            if state == "available":
                break
            if state in ("failed", "rejected", "error"):
                raise RuntimeError(f"Nexus upload processing failed with state: {state}")
            time.sleep(2)
        else:
            raise RuntimeError("Upload processing timed out.")

        file_desc = (
            "Hides empty troop and equipment slots inside Guild House and Armory color channels.\n"
            "Slots return automatically when items are stored in that channel again.\n"
            "Requires BepInEx 5 (Windows x64 Mono), installed separately.\n"
            "Close the game and extract into the Kingdom Inc folder, replacing GuildhouseCleanup.dll."
        )

        print("Creating mod-files version...")
        create_res = api(
            f"/mod-files/{CHAIN_ID}/versions",
            "POST",
            {
                "upload_id": upload_id,
                "name": f"Guild House and Armory Cleanup {VERSION}",
                "version": VERSION,
                "file_category": "main",
                "description": file_desc,
                "update_mod_version": True,
                "archive_existing_file": False,
                "previous_version_id": previous["id"],
                "primary_mod_manager_download": False,
                "allow_mod_manager_download": False,
                "show_requirements_pop_up": True,
            },
        )
        print("Mod-files version created!")

        # Refresh versions list
        for attempt in range(10):
            versions_res = api(f"/mod-files/{CHAIN_ID}/versions")
            versions = versions_res.get("data", {}).get("versions", [])
            matching = [v for v in versions if v["version"] == VERSION]
            if matching:
                break
            time.sleep(2)
        assert matching, "Failed to find created version!"
        latest_ver = matching[0]

    record["gameScopedFileId"] = latest_ver.get("game_scoped_id")
    record["fileVersionId"] = latest_ver["id"]
    print(f"Active version: {latest_ver['version']} (id: {latest_ver['id']}, gameScopedId: {record['gameScopedFileId']})")

    # 2. Verify Manifest Digest
    if "manifest_url" in latest_ver and latest_ver["manifest_url"]:
        print("Verifying manifest digest...")
        manifest_req = urllib.request.Request(
            latest_ver["manifest_url"], headers={"User-Agent": HEADERS["User-Agent"]}
        )
        for attempt in range(20):
            try:
                with urllib.request.urlopen(manifest_req, timeout=45) as m_res:
                    manifest = json.loads(m_res.read().decode("utf-8"))
                    archive_sha = manifest.get("archive", {}).get("hashes", {}).get("SHA256")
                    print(f"Nexus manifest SHA256: {archive_sha}")
                    assert archive_sha.lower() == sha256, "Manifest digest mismatch!"
                    record["nexusServedDownloadVerified"] = True
                    break
            except urllib.error.HTTPError as m_err:
                if m_err.code == 404 and attempt < 19:
                    time.sleep(2)
                    continue
                raise

    # 3. Update Description & Summary
    print("Updating description and summary...")
    target_desc = (OUT / "NEXUS_DESCRIPTION.bbcode.txt").read_text(encoding="utf-8").strip()
    target_summary = (OUT / "NEXUS_SUMMARY.txt").read_text(encoding="utf-8").strip()

    api(
        f"/mods/{MOD_ID}",
        "PATCH",
        {
            "name": "Guild House and Armory Cleanup",
            "summary": target_summary,
            "description": target_desc,
        },
    )
    print("Listing patched.")

    # 4. Post Changelog
    print("Posting changelog entry...")
    try:
        changelog_text = (
            "1.1.0 - Armory & Storage Expansion\n"
            "- Extend empty slot cleanup to Armouries and all shared channel storage buildings (Libraries, Pantries, Treasuries, Hoards).\n"
            "- Equipment slots hide automatically when their stored count drops to zero and return when restocked.\n"
            "- Open Armory menus automatically refresh when equipment availability changes."
        )
        api(
            f"/mods/{MOD_ID}/changelogs",
            "POST",
            {"version": VERSION, "changelog": changelog_text},
        )
        print("Changelog posted.")
    except Exception as ex:
        print(f"Changelog note: {ex}")

    # 5. Save Receipt
    record["github"] = {
        "repository": "https://github.com/KingdomIncModding/KingdomInc_GuildhouseCleanup",
        "release": f"https://github.com/KingdomIncModding/KingdomInc_GuildhouseCleanup/releases/tag/v{VERSION}",
        "tag": f"v{VERSION}",
    }
    RECEIPT.write_text(json.dumps(record, indent=2) + "\n", encoding="utf-8")
    print(f"Receipt written to {RECEIPT}")
    print(f"\nSUCCESS: Guild House & Armory Cleanup {VERSION} updated on Nexus Mods!")

if __name__ == "__main__":
    main()
