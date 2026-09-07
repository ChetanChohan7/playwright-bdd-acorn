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

## Bulk generate

Once a document is parsed, the **Bulk generate** panel lets you create
many variants of it in one action instead of saving one at a time:

- **Scenario ID pattern** — e.g. `PS-{n}`, where `{n}` is replaced by an
  increasing number for each copy.
- **Start number** / **How many** — e.g. start `1001`, count `10` →
  `PS-1001` … `PS-1010`.
- **Also set this XML field to the generated Scenario ID** — optionally
  pick a field (e.g. the document's `@id` attribute) to sync to the same
  generated value, so the id inside the XML matches the CSV's
  `ScenarioId` column. Leave it on "(none)" to keep the XML content
  identical across copies and only vary the CSV's Scenario ID.

All other field values come from whatever is currently in the edit form,
so you can tweak shared values once and then generate N variants of that
template that differ only by ID. All N rows are appended to
`data/scenarios.csv` in a single click.

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
