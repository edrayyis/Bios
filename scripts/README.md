# scripts

Helper scripts for the BIOS Recovery Manager repo.

## promote-to-remote.bat (Windows)

Stages all local changes, commits them, and pushes to `origin` on the current
branch. Streamlit Cloud watches the remote and redeploys automatically once the
push lands.

```bat
:: Prompt for a commit message
scripts\promote-to-remote.bat

:: Or pass the commit message directly
scripts\promote-to-remote.bat "Fix Dell service tag algorithm"
```

The script can be run from anywhere — it resolves the repo root relative to its
own location. If there is nothing to commit it still pushes, in case the remote
is behind. Exit codes: `0` success, `1` not a git repo / git missing,
`2` commit failed, `3` push failed.
