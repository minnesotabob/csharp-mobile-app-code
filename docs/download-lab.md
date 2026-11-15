# Chapter 29 — public downloads and media storage

This is an explicit foreground public-fixture lab. It is not a customer-delivery
service, background transfer scheduler, download library browser or playback integration.
The endpoint serves the same original 10-second WAV as Chapter 27, with a committed
length/hash and strong ETag. No third-party music or customer files are included.

## Run the lab

1. Use the pinned SDK. Run the API with its HTTPS development profile; trust the
   workstation certificate through supported OS tooling. See local-api.md.
2. Home → Public media download lab. Configure your trusted HTTPS API origin. Device
   localhost is the device, not the workstation. Reachability, TLS trust and physical
   device setup are separate work; never install a certificate-validation bypass.
3. Download / resume. Byte progress is not verified availability. The app reports a
   complete download only after exact length/hash verification and local promotion.
4. Cancel, leave the page or stop the window: cancellation retains useful partial
   bytes. Tap Download again to request Range plus If-Range. Small local fixtures may
   finish too quickly to cancel manually; portable/API tests interrupt deterministically.
5. Verify stored fixture reads and hashes the complete file again. Delete removes
   only this descriptor's final and partial files. Account changes do not select a
   different owner for this anonymous public cache.

CacheDirectory/public-media-v1 has a 4 MiB logical quota including complete and partial
files. The default available-space check requires remaining bytes plus a 64 KiB reserve.
The OS may evict cache entries at any time. Progress is foreground/best effort; app
resume does not restart a transfer. Device checks must establish actual filesystem,
TLS, cancellation timing and eviction behavior. iOS compilation remains pending.

## Wire and storage contract

- Trusted descriptor: public kind, bounded ID/revision, fixed HTTPS URI without
  credentials/query/fragment, length <=8 MiB and lowercase SHA-256. This lab does not
  discover descriptors over the network. A hash supplied by an attacker is not authority.
- The filename is SHA-256 over descriptor identity (including origin, revision, length
  and content hash) plus a fixed .part or .wav extension. Server filenames and
  Content-Disposition are not used. Hash-based naming is not encryption.
- New downloader instances reuse a partial only for the same complete descriptor.
  A full-length partial left before promotion is rehashed and may be promoted without
  another request. Corrupt partials are discarded. Cache hits are rehashed too.
- For resume: Range bytes=N- and If-Range with the pinned strong ETag. Accept 206 only
  with exact start/end/total and remaining Content-Length. Require audio/wav, matching
  strong ETag and identity encoding. Redirects and automatic decompression are disabled
  in production transport. Handler injection is a trusted test/composition seam.
- A supported full 200 response truncates the partial; it never appends a full body.
  A changed ETag fails the trusted descriptor check and requires a new descriptor.
  A 416 discards the partial and requires explicit retry; no automatic loop.
- Reads are bounded even if headers lie. Truncation/transport errors and cancellation
  may retain partial bytes. Invalid framing/hash deletes the partial. No .part path
  is returned as available media. Only verified bytes are renamed within one directory.
- One downloader instance serializes transfer, verification and deletion. Native DI
  registers that instance once. The class has no multi-process/cross-instance locking
  contract; stop/await operations before disposing it. Storage check is not a physical
  capacity reservation: another application can consume space after the check.
- Rename prevents this library from advertising a partly written final file; it is
  not a guarantee of power-loss durability. Revalidate every advertised cache file.

## Private deliveries are a separate boundary

MediaAccess.PrivateDelivery is rejected before a public descriptor exists. The client
supplies no Authorization header. Do not place signed delivery URLs, customer filenames,
tokens or private data in this cache. A future private service needs server-authorized
ownership, bounded renewable delivery access, account-scoped storage and operation
leases, sign-out/revocation behavior, backup/encryption policy and late-result fencing.
A revision hash and a download URL alone cannot implement these requirements.

## Acceptance worksheet (all native rows pending)

Record exact app SHA, model/OS, API origin/TLS setup, remaining capacity, steps and evidence.

| Experiment | Application contract | Android | iPhone |
| --- | --- | --- | --- |
| Fresh download | Verified final only; no autoplay | Pending | Pending |
| Cancel / page departure / window stop | Partial may remain; no automatic restart | Pending | Pending |
| Process restart then explicit resume | Same descriptor requests remaining range or rehashes complete partial | Pending | Pending |
| Cache eviction | Missing entry becomes unavailable; explicit redownload | Pending | Pending |
| Low space / competing storage consumption | Visible failure; no false available file | Pending | Pending |
| Delete and retry | Selected final/partial removed; fresh explicit transfer | Pending | Pending |
| TLS trust / wrong origin / redirect | Fail visibly without credential/certificate bypass | Pending | Pending |
| Account switch | Public cache remains public; private descriptor always rejected | Pending | Pending |

Portable tests use real temporary files with simulated HTTP/space conditions. API tests
exercise real ASP.NET range processing and an actual downloader cancel/resume through
the server pipeline. The Kestrel smoke checks full hash and an exact range over real
loopback HTTP; it does not prove HTTPS or native-device networking. Exact executed
revisions and outcomes are in verification.md.
