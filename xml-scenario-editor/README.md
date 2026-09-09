# XML Scenario Editor

A small local web app for loading an XML document, editing the values it
contains, and logging each save as a row in a CSV file.

## What it does

1. **Load** — upload an `.xml` file or paste XML directly.
2. **Edit** — every leaf value in the document (element text and
   attributes) is shown as its own editable field, labeled with its
   path in the document (e.g. `Order › Customer › @name`). Works with
   any XML structure — nothing is hardcoded to a specific schema.
3. **Save** — enter a **Scenario ID** and **Version**, then Save. The
   edited values are written back into the original XML structure and
   appended as one row to `data/scenarios.csv`:

   | ScenarioId | Version | Date (ISO timestamp, auto-set) | Xml (full updated document) |
   |---|---|---|---|

   Every save appends a new row — the CSV is a running history, it's
   never overwritten.

## Adding a field to just the document you're editing

The **"Add a field to this document"** control (in the edit panel, above
Preview/Save) adds a field to only the XML you currently have loaded —
it doesn't touch the CSV or any other document. Useful when the document
you're about to save is missing something the schema should have.

Type the field's path (same dot/@ syntax as backfilling, below) and a
value, click **Add field**, and it appears in the field list above like
any other — editable, and included when you Preview or Save. Any edits
already made to other fields are kept; nothing is lost by adding one.

## Backfilling a field into already-saved rows

If the XML template gains a field after some rows have already been
saved — including a field that's **never existed in any document
before** — those older rows' `Xml` column won't have it. The **"Add a
field to already-saved rows"** panel fixes that without hand-editing
the CSV:

1. Type the **field path**, dot-separated, e.g. `Plan.TaxRate` for a new
   element or `Plan.@taxIncluded` for a new attribute. You can include
   the document's root element name or leave it off — it's added
   automatically (paths of fields from whatever you last parsed also
   show up as autocomplete suggestions, but typing one that matches
   nothing is exactly how you add a brand-new field).
2. Enter the **value** it should have in every existing row.
3. Click **Add to existing CSV rows**.

The server re-parses each row's `Xml`, inserts the field at that path —
creating any missing parent elements along the way — and rewrites the
row; `ScenarioId`, `Version` and `Date` are left untouched, only `Xml`
changes. It also guards against a mistyped path ever producing invalid
multi-root XML: a row is skipped and reported (not corrupted) if
anything about it can't be made to work.

## Run it

```bash
cd xml-scenario-editor
npm install
npm start
```

Then open http://localhost:4300.

Use `npm run dev` instead to auto-restart the server on file changes.

## Notes

- `data/scenarios.csv` is created on first save and is git-ignored (only
  `data/.gitkeep` is tracked) since it's local output, not project source.
- The CSV is written with standard RFC 4180 quoting, so the multi-line
  XML column opens correctly in Excel, Google Sheets, or any CSV
  library — the "Download full CSV" link in the UI serves that same file.
- The XML declaration (`<?xml version="1.0"?>`) is preserved automatically
  and isn't shown as an editable field, since it isn't scenario content.
- Set `PORT` to run on a different port, e.g. `PORT=5000 npm start`.
