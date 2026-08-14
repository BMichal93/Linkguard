# LinkGuard

A standalone .NET CLI that crawls a deployed site and fails the build if any
internal page returns 4xx or 5xx. It hits the deployed origin as an external
HTTP client, so it catches edge-level failures - a bad TLS cert renewal, a
middleware corrupting responses, a geo-redirect bouncing requests - that are
invisible from inside the app itself.

On a clean run it prints one line and exits 0. On a broken one it exits
non-zero with a report that names the referring page for every broken link,
not just the broken URL.

## Quick start

```
dotnet tool install --global linkguard
linkguard --url https://staging.example.com
```

## Flags

| Flag | Default | Purpose |
|---|---|---|
| `--url` | required | Base URL, e.g. `https://staging.example.com` |
| `--timeout` | `20` | Per-request timeout, seconds |
| `--max-concurrency` | `8` | Cap on in-flight requests |
| `--ignore` | none | Repeatable substring or regex to skip |
| `--header` | none | Repeatable `Name: Value`, passed on every request |
| `--format` | `console` | `console` \| `json` |
| `--junit` | none | Path to write a JUnit XML file |
| `--no-external` | false | Skip external link checks entirely |

## How it works

1. **Discover.** `GET /robots.txt` for `Sitemap:` directives, else
   `GET /sitemap.xml` directly, recursing into sitemap indexes. If neither
   yields URLs, fall back to a BFS crawl from the root, following internal
   links only.
2. **Check.** Internal URLs are `GET` (the body is needed to find outbound
   links); external URLs are `HEAD` with a `GET` fallback on 405. Redirects
   are followed manually - up to 5 hops, full chain recorded - rather than
   auto-followed, so a page that 301s to a 404 gets caught. Timeouts and 5xx
   get up to 2 retries with jittered backoff, since Umbraco Cloud apps
   cold-start after a deploy. External links are checked one level deep and
   never recursed into.
3. **Classify.** A broken internal link fails the build. A broken external
   link only warns - a third-party site being down at 2am must not block a
   release.
4. **Report.** Console output, `--format json` for the full result set, or
   `--junit <path>` for one test case per checked URL so broken links show
   up in the CI Tests tab.

## Exit codes

| Code | Meaning |
|---|---|
| `0` | Clean - no internal failures |
| `1` | At least one internal URL failed |
| `2` | Usage or configuration error |

## Azure Pipelines

Run it after the staging deploy and before the push to live:

```yaml
- script: |
    dotnet tool install --global linkguard --add-source $(Pipeline.Workspace)/nuget
    linkguard \
      --url $(StagingUrl) \
      --max-concurrency 8 \
      --junit $(Agent.TempDirectory)/linkguard.xml \
      --header "CF-IPCountry: PL"
  displayName: Link check staging
- task: PublishTestResults@2
  condition: always()
  inputs:
    testResultsFormat: JUnit
    testResultsFiles: $(Agent.TempDirectory)/linkguard.xml
```

`CF-IPCountry` pins the geo-redirect middleware to one country, so the
crawler is not bounced between locale roots. `condition: always()` on the
publish step matters - otherwise the report is discarded on exactly the
runs where it is needed. See `azure-pipelines-snippet.yml` for the same
snippet on its own.
