# Data provenance

`listings.csv.gz` in this directory is a trimmed subset of real Inside Airbnb data for
New York City. It is committed to the repository on purpose — see [Why it is committed](#why-it-is-committed).

## Attribution

> Source: [Inside Airbnb](https://insideairbnb.com/), by Murray Cox.
> Licensed under a [Creative Commons Attribution 4.0 International License](http://creativecommons.org/licenses/by/4.0/) (CC BY 4.0).
> **Modified** — see [What was changed](#what-was-changed).

CC BY 4.0 permits redistribution, including commercially, provided attribution is given and
changes are indicated. Both are done here and in the project README.

## Upstream snapshot

| | |
|---|---|
| Region | New York City, United States |
| Snapshot date | **2026-08-10** |
| Source URL | `https://data.insideairbnb.com/united-states/ny/new-york-city/2026-08-10/data/listings.csv.gz` |
| Upstream size | ~15 MB gzipped, 30,234 rows, 90 columns |
| Committed subset | 1.2 MB gzipped, 5,000 rows, 17 columns |

Inside Airbnb publishes quarterly and its download URLs are date-stamped, so the URL above will
eventually stop resolving. That is precisely why the subset is committed.

## Why it is committed

Inside Airbnb's download URLs are date-stamped and rotate quarterly, so a repository that
fetched one at seed time would work today and 404 for whoever clones it next spring. The subset
is 1.2 MB, small enough to live in git, and committing it means `docker compose up` needs no
network access for data and produces the same index for everyone, forever.

## What was changed

Reproduce all of it with:

```sh
dotnet run --project tools/Nestly.DataTrimmer
```

The sample seed is fixed, rows are sorted by `id`, and the archive is written with its timestamp
zeroed, so re-running produces a **byte-identical** file rather than a spurious 1.2 MB diff. The
tool needs the upstream snapshot in `data/raw/`, which is gitignored; run it without one and it
prints the `curl` command that fetches it.

1. **Columns reduced from 90 to 17.** Only the fields Nestly indexes are kept. This is most of
   the size reduction, and it drops host contact details and other personal data the project has
   no use for.
2. **Rows required to be complete.** Any row missing a name, description, coordinates,
   `bedrooms`, `bathrooms_text`, `amenities`, price, or neighbourhood is dropped (18,586 rows).
   Notably 41% of upstream rows have no `bedrooms` value; mapping those to `0` would have
   invented a studio population that does not exist in the data, so they are excluded instead.
3. **Price range restricted to $20–$2,000/night** (133 rows dropped). The upstream tail reaches
   $31,211/night, which stretches the price facet far enough to make the slider useless for the
   99% of listings below $2,000.
4. **Sampled to 5,000 rows** from the 11,515 eligible, seeded with `20260810`. The sample is
   uniform, so the natural borough distribution is preserved — Manhattan and Brooklyn dominate,
   Staten Island is sparse, exactly as in the source.

## The one derived field

Everything indexed comes from the source as published, with a single exception:

**`monthlyRent = pricePerNight × 30`.**

Inside Airbnb is short-term rental data, priced per night; there is no monthly rent in the
source to read. Nestly presents itself as an apartment finder, so a monthly figure is derived.
It is flagged in the domain model, called out in the project README, and is the only fabricated
value in the index.

Be aware this yields higher numbers than real NYC long-term rents — the median works out around
$6,500/month, because nightly short-let rates bake in turnover, cleaning and margin. A smaller
multiplier would produce prettier figures, but it would be a fudge factor chosen to look good,
which is worse than an obvious ×30. Treat the rents as demo data, not market data.

## Not fabricated

For the avoidance of doubt, the project does **not** invent square footage, no-fee status, or
listing dates, despite all three being natural facets for an apartment search. The source has no
such fields, and presenting invented values alongside real ones is the kind of thing a reviewer
is right to distrust. Facets use only what the data genuinely contains.
