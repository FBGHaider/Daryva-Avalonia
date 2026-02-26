# Releasing a new desktop version so users get the update

When you make changes and want users to see a new patch, you need to run the **Release desktop to Daryva-Updates** workflow. You can do it in two ways.

**Tip:** To avoid “burning” version numbers on small fixes, use a **fourth number** for tiny updates: e.g. **1.0.12.1**, **1.0.12.2** for fixes, and reserve **1.0.13** for your next bigger release. The workflow and build support this.

---

## Option 1: Push a version tag (simplest)

From your repo on your PC:

```powershell
cd "C:\Users\Abbas Haider\Repo\Daryva-Avalonia"
git tag v1.0.13
git push origin v1.0.13
```

- Use your **next version number** (e.g. `1.0.12` → `1.0.13` for a patch).
- The workflow runs automatically, builds the app, and publishes to **fbg-engineering/Daryva-Updates**.
- Users who have “Check for updates” will see the new version.

---

## Option 2: Run the workflow manually

1. On GitHub: **Actions** → **Release desktop to Daryva-Updates** → **Run workflow**.
2. Enter the version (e.g. `1.0.13`) in the **version** input.
3. Click **Run workflow**.

No tag is created; the release is built and uploaded with that version.

---

## Checklist

- [ ] Code is committed and pushed to `master` (or your main branch).
- [ ] You know the next version number (e.g. patch: 1.0.12 → 1.0.13).
- [ ] Secret **DARYVA_UPDATES_GITHUB_TOKEN** is set in this repo (PAT with access to **fbg-engineering/Daryva-Updates**).

After the workflow finishes, the new build is on Daryva-Updates and clients can update.
