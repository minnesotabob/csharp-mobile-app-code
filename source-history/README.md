# Original editorial checkpoints

GitHub publication of the prepared source uses a snapshot commit on main. The
original local commits remain available in pre-publication.bundle so exact source
identities in the manuscript and verification records are reproducible. Historical
run/source identities have not been rewritten to the publication commit.

Repository: minnesotabob/csharp-mobile-app-code
Bundle prerequisite: 0d81cdc13eda9df405719398a9adffddd46280b4
Original local head: 3f07b25743c00ef0a1ad64347f310673f5ecad7c

From a fresh clone of this repository, inspect and fetch the retained history:

```bash
git bundle verify source-history/pre-publication.bundle
git fetch source-history/pre-publication.bundle HEAD
```

The fetch imports the original commit objects without changing your checked-out
branch. You can inspect an exact checkpoint with git show, or create a separate
exercise checkout from its full SHA. These original commits are not separate
GitHub web-history entries; the bundle preserves their exact Git identities locally.
Use the current main snapshot for the complete latest source.

The bundle contains only the prepared committed source history after the already
public prerequisite above. It contains no ignored PDF/EPUB outputs, build products,
private signing stores or runtime databases. The bundle is source history, not a
release package, test result or signing credential.
