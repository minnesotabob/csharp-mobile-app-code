# Chapter 31 — native capabilities and optional references

Home → Native capabilities and attachment lab. Use synthetic references only. This
is a transient local scope, not customer upload, account storage, an order attachment
endpoint or a migration of earlier request drafts. No camera capture or location
permission is added. Android compilation is not a picker/decoder/share observation;
iOS compilation and every native acceptance row remain pending.

## Implemented boundaries

- Text: FilePicker filtered to text/plain/public.plain-text; read through OpenReadAsync,
  never FileResult.FullPath or the selected filename. Strict UTF-8, nonempty <=64 KiB;
  disallow controls except tab/CR/LF. Text is not treated as HTML/executable content.
- Photos: PickPhotosAsync with SelectionLimit=1, requested 1600x1600/rotation and no
  metadata preservation. Enforce at most one result again. Picker processing is native
  and precedes app validation; its actual limits/orientation/cache behavior need device
  evidence. Cancel/empty result keeps an earlier selected attachment.
- App image input: <=4 MiB, PNG/JPEG header preflight, max edge 4096 and <=4 million
  declared pixels before this app's native decoder. This is resource preflight, not
  complete image validation, antivirus or a guarantee against decoder defects.
- NativeImageNormalizer uses PlatformImage on a worker task, disposes decoded/resized
  wrappers, downsizes to 800 and encodes PNG to a 2 MiB bounded output stream. Cancellation
  is checked before/after native work and cannot preempt a synchronous codec call.
- Core independently checks normalized PNG framing/CRC, 8-bit noninterlaced RGB/RGBA
  dimensions and removes ancillary chunks. It keeps IHDR/IDAT/IEND only; palette/tRNS
  output is rejected. Metadata stripping does not remove private information in pixels,
  steganography or details in the selected text. Dropping profiles can change colors.
- A fresh generated folder per local lab workspace holds one staged attachment.
  Generic generated IDs/extensions, hash verification and .part→final promotion avoid
  trusting selected paths. Failure/cancel before replacement preserves the prior entry.
  A generation captured BEFORE picker launch fences late results after scope reset.
- One workspace serializes its operations; a process-wide commit gate serializes the
  16 MiB aggregate staging budget across workspace folders. Abandoned scope bytes count.
  No cross-process lock/durable private inventory is supplied. Process-death leftovers
  may require developer/OS cache cleanup; production needs an inventory/retention policy.
- Clear invalidates current use/generation immediately and then attempts file cleanup.
  Canceled/failed cleanup stays unconfirmed. Leaving the Shell route/window destruction
  cancels and clears; native picker visibility alone is not treated as route departure.
  Window stop cancels preparation unless the picker is active. An uncooperative provider
  may finish late; its result must still fail the original scope/token checks.
- Cached staged content is rehashed before preview/export. Cache eviction/mutation can
  make a receipt unusable. No receipt alone proves current availability or authorization.

## File sharing and export lifetime

An explicit Share button copies verified staged content into CacheDirectory/sharing-root,
with a new generic generated filename and fixed media type. Export validation rejects
hash/type/metadata mismatches. A singleton AttachmentExportStore serializes that root:
at most four files/8 MiB, with owned copies older than 24 hours pruned on a later export.
It is not a background expiry timer. Unknown files consume budget but are not deleted
by selective cleanup. Remove local exported copies is an explicit action.

Android's MAUI FileProvider override contains ONE internal cache root, sharing-root,
not the whole cache/AppData tree. CI checks this source XML and the merged provider
resource reference/permissions; real content-URI root and grant behavior remain native
checks. Share sheet return proves no recipient, delivery or complete read; exported
copies are not immediately deleted on return because receivers may read asynchronously.
Removing local copies cannot revoke copies already retained by another application.
A canceled/failed share attempt may also leave a bounded export copy.
An iPad presentation rectangle is supplied; actual positioning needs device review.

The workspace, export store and provider/OS copies have different ownership and lifetimes.
Clearing the local scope does not claim to erase the original photo, picker cache,
recovered OS results, a receiver's copy or physical storage. Cache and staging are
unencrypted and not account-authorized; do not use customer content in this lab.

## Capability fallbacks and permission scope

Connectivity is a reported hint, not proof the API is reachable; staging stays local
and available offline. Subscribe for this page's lifetime and unsubscribe on departure.
Unavailable connectivity/haptics do not make a staged reference invalid. Haptics default
off and request only a brief Click after success; a successful call does not prove a
physical sensation. Android adds ACCESS_NETWORK_STATE and VIBRATE only. The pinned
11.0.10 Android media picker uses a system picker/GET_CONTENT fallback for selection;
camera permissions in broad capture examples are not imported into this pick-only lab.
iOS purpose strings describe photo selection/share-sheet saving; no camera/microphone
or location usage string is added. Inspect actual prompts and limited-library behavior.

The Image view uses its normal MAUI handler. This chapter adds a stream/image adapter,
not an unnecessary replacement view handler. See Chapter 28 for a justified handler
extension; any platform-only hook must own and remove its native events/resources.
The optional location exercise is a design extension only: explicit when-in-use action,
manual-town fallback, bounded accuracy/freshness and no background tracking. No current
app feature acquires or uploads location.

## Native acceptance (all pending)

Record SHA, model/OS, provider, permission/library state, source format/size/dimensions,
steps and evidence. Never use actual customer photos to produce a teaching screenshot.

| Scenario | Contract to inspect | Android | iPhone/iPad |
| --- | --- | --- | --- |
| Text/photo picker, cancel/deny/unavailable | Optional flow; previous entry preserved on cancel/failure | Pending | Pending |
| Provider URI/cloud file | OpenReadAsync works without treating FullPath as local | Pending | Pending |
| JPEG/PNG/HEIC/unsupported input | Supported inputs normalize; unsupported/oversized fail visibly | Pending | Pending |
| Large/rotated/metadata-rich image | Bounds, orientation, alpha/colors and actual resulting PNG chunks | Pending | Pending |
| Leave/reset while picker or codec pending | Original generation cannot publish late into new scope | Pending | Pending |
| Cache eviction/write failure/process death | Receipt recheck fails safely; cleanup uncertainty stays visible | Pending | Pending |
| Share on phone/tablet | Narrow content URI, correct type, usable presentation, cancel/return semantics | Pending | Pending |
| Receiver reads after sheet returns | Local retention helps; no delivery/revocation claim | Pending | Pending |
| Offline/unknown connectivity | Local staging works; no false service reachability | Pending | Pending |
| Haptics off/unsupported/system-disabled | Optional request; no workflow dependency or repeated vibration | Pending | Pending |

Portable tests use real temporary files, independently generated exact PNG fixtures
and a simulated normalizer. They validate policy/framing/chunk removal, not native
pixel decoding/resizing. Metadata fixtures are synthetic. Exact source/build results
are recorded in verification.md; no attachment upload endpoint is added to the API.
