// server.js
// Local UI backend: parse an uploaded/pasted XML document into editable
// fields, rebuild the XML from edited values, and append it as a row
// (ScenarioId, Version, Date, Xml) to data/scenarios.csv.

const express = require('express');
const path = require('path');
const fs = require('fs');
const { XMLParser, XMLBuilder } = require('fast-xml-parser');

const app = express();
const PORT = process.env.PORT || 4300;

const DATA_DIR = path.join(__dirname, 'data');
const CSV_PATH = path.join(DATA_DIR, 'scenarios.csv');
const CSV_HEADER = 'ScenarioId,Version,Date,Xml';

app.use(express.json({ limit: '20mb' }));
app.use(express.static(path.join(__dirname, 'public')));

// Keep everything as strings (no auto number/boolean coercion) so a
// round-trip parse -> edit -> rebuild doesn't silently change types.
const parserOptions = {
  ignoreAttributes: false,
  attributeNamePrefix: '@_',
  textNodeName: '#text',
  parseTagValue: false,
  parseAttributeValue: false,
  trimValues: false,
  allowBooleanAttributes: true,
};

const builderOptions = {
  ...parserOptions,
  format: true,
  indentBy: '  ',
  suppressEmptyNode: false,
  // Without this, the builder renders any attribute whose value is
  // exactly the string "true" as a bare, valueless attribute (e.g.
  // `taxIncluded` instead of `taxIncluded="true"`) — invalid XML, and a
  // real trap here since every value in this tool is kept as a string.
  suppressBooleanAttributes: false,
};

/**
 * Recursively walks a parsed-XML object and collects every leaf
 * (text node or attribute value) as { path, value }. `path` is an
 * array of string keys / numeric array-indices that can be used to
 * read/write the same location again later.
 */
function collectFields(node, currentPath, fields) {
  if (node === null || node === undefined) return;

  if (Array.isArray(node)) {
    node.forEach((item, idx) => collectFields(item, [...currentPath, idx], fields));
    return;
  }

  if (typeof node === 'object') {
    for (const key of Object.keys(node)) {
      collectFields(node[key], [...currentPath, key], fields);
    }
    return;
  }

  // Primitive leaf value (string, since parseTagValue/parseAttributeValue are off).
  fields.push({ path: currentPath, value: String(node) });
}

/** Turns a field path into a human-readable label for the UI. */
function labelForPath(pathSegments) {
  const parts = [];
  for (const seg of pathSegments) {
    if (typeof seg === 'number') {
      const lastIdx = parts.length - 1;
      parts[lastIdx] = `${parts[lastIdx]} #${seg + 1}`;
    } else if (seg.startsWith('@_')) {
      parts.push(`@${seg.slice(2)}`);
    } else if (seg === '#text') {
      parts.push('(text)');
    } else {
      parts.push(seg);
    }
  }
  return parts.join(' › ');
}

/**
 * Removes whitespace-only "#text" nodes from a parsed-XML object tree.
 * These arise from the indentation/newlines between sibling child
 * elements in pretty-printed XML (e.g. `<Customer>\n  <Name/>\n</Customer>`)
 * and aren't real content — left in place they'd show up as junk
 * editable fields and, once the builder re-indents, as stray blank
 * lines in the rebuilt XML.
 */
function pruneBlankText(node) {
  if (Array.isArray(node)) {
    node.forEach(pruneBlankText);
    return;
  }
  if (node && typeof node === 'object') {
    for (const key of Object.keys(node)) {
      if (key === '#text' && typeof node[key] === 'string' && node[key].trim() === '') {
        delete node[key];
      } else {
        pruneBlankText(node[key]);
      }
    }
  }
}

function setAtPath(obj, pathSegments, value) {
  let cur = obj;
  for (let i = 0; i < pathSegments.length - 1; i++) {
    cur = cur[pathSegments[i]];
  }
  cur[pathSegments[pathSegments.length - 1]] = value;
}

/**
 * Like setAtPath, but creates any missing intermediate objects/arrays
 * along the way instead of assuming they already exist. Used to add a
 * brand-new field into documents that don't have it yet (e.g. backfilling
 * an older row's XML with a field only the current template has).
 */
function setAtPathCreate(obj, pathSegments, value) {
  let cur = obj;
  for (let i = 0; i < pathSegments.length - 1; i++) {
    const seg = pathSegments[i];
    const nextSeg = pathSegments[i + 1];
    if (cur[seg] === undefined || cur[seg] === null || typeof cur[seg] !== 'object') {
      cur[seg] = typeof nextSeg === 'number' ? [] : {};
    }
    cur = cur[seg];
  }
  cur[pathSegments[pathSegments.length - 1]] = value;
}

function ensureCsv() {
  if (!fs.existsSync(DATA_DIR)) fs.mkdirSync(DATA_DIR, { recursive: true });
  if (!fs.existsSync(CSV_PATH)) {
    fs.writeFileSync(CSV_PATH, CSV_HEADER + '\r\n', 'utf8');
  }
}

function csvEscape(value) {
  const str = String(value ?? '');
  if (/[",\n\r]/.test(str)) {
    return '"' + str.replace(/"/g, '""') + '"';
  }
  return str;
}

/** Minimal RFC-4180 CSV parser, used only to preview recent rows. */
function parseCsv(content) {
  const rows = [];
  let row = [];
  let field = '';
  let inQuotes = false;

  for (let i = 0; i < content.length; i++) {
    const c = content[i];
    if (inQuotes) {
      if (c === '"') {
        if (content[i + 1] === '"') {
          field += '"';
          i++;
        } else {
          inQuotes = false;
        }
      } else {
        field += c;
      }
    } else if (c === '"') {
      inQuotes = true;
    } else if (c === ',') {
      row.push(field);
      field = '';
    } else if (c === '\r') {
      // ignore, \n handles the line break
    } else if (c === '\n') {
      row.push(field);
      rows.push(row);
      row = [];
      field = '';
    } else {
      field += c;
    }
  }
  if (field.length || row.length) {
    row.push(field);
    rows.push(row);
  }
  return rows.filter((r) => !(r.length === 1 && r[0] === ''));
}

app.post('/api/parse', (req, res) => {
  const { xml } = req.body || {};
  if (!xml || typeof xml !== 'string' || !xml.trim()) {
    return res.status(400).json({ error: 'No XML content provided.' });
  }

  try {
    const parser = new XMLParser(parserOptions);
    const structure = parser.parse(xml);
    pruneBlankText(structure);

    res.json({ fields: fieldsFromStructure(structure), structure });
  } catch (err) {
    res.status(400).json({ error: `Failed to parse XML: ${err.message}` });
  }
});

app.post('/api/save', (req, res) => {
  const { scenarioId, version, structure, fields, dryRun } = req.body || {};

  if (!dryRun) {
    if (!scenarioId || !String(scenarioId).trim()) {
      return res.status(400).json({ error: 'Scenario ID is required.' });
    }
    if (!version || !String(version).trim()) {
      return res.status(400).json({ error: 'Version is required.' });
    }
  }
  if (!structure || !Array.isArray(fields)) {
    return res.status(400).json({ error: 'Missing parsed XML structure/fields — parse an XML document first.' });
  }

  try {
    const xmlOut = buildXml(structure, fields);

    // Dry run (used for the "Preview updated XML" button): build the XML
    // but don't touch the CSV.
    if (dryRun) {
      return res.json({ xml: xmlOut, date: null });
    }

    ensureCsv();
    const date = new Date().toISOString();
    const row = [scenarioId, version, date, xmlOut].map(csvEscape).join(',') + '\r\n';
    fs.appendFileSync(CSV_PATH, row, 'utf8');

    res.json({ xml: xmlOut, date });
  } catch (err) {
    res.status(500).json({ error: `Failed to save: ${err.message}` });
  }
});

/** Builds one XML string from a structure + edited field list. */
function buildXml(structure, fields) {
  const updated = JSON.parse(JSON.stringify(structure));
  for (const f of fields) {
    setAtPath(updated, f.path, f.value);
  }
  const builder = new XMLBuilder(builderOptions);
  let xmlOut = builder.build(updated);
  if (!/^\s*<\?xml/.test(xmlOut)) {
    xmlOut = '<?xml version="1.0" encoding="UTF-8"?>\n' + xmlOut;
  }
  return xmlOut;
}

/**
 * Parses the UI's dot/@ path syntax (e.g. "Plan.TaxRate" or
 * "Items.Item.0.@sku") into the same path-segment array format used
 * internally (numbers for array indices, "@_x" for attributes, plain
 * strings for element names) — the exact inverse of the frontend's
 * pathToString().
 */
function parsePathString(str) {
  return String(str)
    .split('.')
    .map((seg) => seg.trim())
    .filter(Boolean)
    .map((seg) => {
      if (/^\d+$/.test(seg)) return Number(seg);
      if (seg.startsWith('@')) return '@_' + seg.slice(1);
      return seg;
    });
}

/**
 * Inserts a new field into a parsed-XML structure at a dot/@ path string,
 * mutating it in place. Shared by /api/add-field (one document) and
 * /api/backfill-field (every saved row), so both get the same root
 * handling and the same guard against ever producing invalid XML.
 *
 * A valid XML document has exactly one top-level element (plus an
 * optional "?xml" declaration node). The path is relative to that root,
 * so if it's typed without the root (e.g. "Plan.TaxRate" instead of
 * "PricingScenario.Plan.TaxRate"), it's prefixed automatically. If the
 * result would somehow end up with more than one top-level element —
 * i.e. a mistyped path tried to add a second root — this throws instead
 * of letting invalid XML be built.
 */
function addFieldAtPath(structure, pathStr, value) {
  const fieldPath = parsePathString(pathStr);
  if (fieldPath.length === 0) {
    throw new Error('Could not make sense of that field path.');
  }

  const rootKeys = Object.keys(structure).filter((k) => k !== '?xml');
  if (rootKeys.length !== 1) {
    throw new Error(`Document doesn't have exactly one root element (found: ${rootKeys.join(', ') || 'none'}).`);
  }
  const [rootKey] = rootKeys;
  const effectivePath = fieldPath[0] === rootKey ? fieldPath : [rootKey, ...fieldPath];

  setAtPathCreate(structure, effectivePath, String(value));

  const afterKeys = Object.keys(structure).filter((k) => k !== '?xml');
  if (afterKeys.length !== 1) {
    throw new Error(`Adding "${pathStr}" would create a second root element (${afterKeys.join(', ')}) — check the path.`);
  }
}

/** Re-derives the editable field list (fresh ids) from a structure. */
function fieldsFromStructure(structure) {
  const rawFields = [];
  collectFields(structure, [], rawFields);
  return rawFields
    .filter((f) => f.path[0] !== '?xml')
    .map((f, i) => ({ id: i, path: f.path, label: labelForPath(f.path) || '(root)', value: f.value }));
}

/**
 * Adds a field to a single, currently-loaded document (not yet saved) —
 * used by the "Add a field to this document" control in the edit panel.
 * Any edits already made to other fields are applied first so they
 * aren't lost, then the new field is added and the full field list is
 * recomputed so the UI can re-render it.
 */
app.post('/api/add-field', (req, res) => {
  const { structure, fields, path: pathStr, value } = req.body || {};

  if (!structure) {
    return res.status(400).json({ error: 'Parse an XML document first.' });
  }
  if (!pathStr || typeof pathStr !== 'string' || !pathStr.trim()) {
    return res.status(400).json({ error: 'Missing the field path to add, e.g. "Plan.TaxRate".' });
  }
  if (value === undefined || value === null) {
    return res.status(400).json({ error: 'A value for the new field is required.' });
  }

  try {
    const updated = JSON.parse(JSON.stringify(structure));
    if (Array.isArray(fields)) {
      for (const f of fields) {
        setAtPath(updated, f.path, f.value);
      }
    }

    addFieldAtPath(updated, pathStr, value);

    res.json({ structure: updated, fields: fieldsFromStructure(updated) });
  } catch (err) {
    res.status(400).json({ error: `Couldn't add field: ${err.message}` });
  }
});

/**
 * Backfills a field into every row already saved in the CSV — used when
 * the XML template gains a new field after some rows were already
 * generated (including a field that has never existed in any document
 * before), so those older rows can be brought up to date. Each row's
 * Xml column is re-parsed, the field is added (or overwritten) at
 * `path`, and the row is rebuilt; ScenarioId/Version/Date are left as-is.
 */
app.post('/api/backfill-field', (req, res) => {
  const { path: pathStr, value } = req.body || {};

  if (!pathStr || typeof pathStr !== 'string' || !pathStr.trim()) {
    return res.status(400).json({ error: 'Missing the field path to add, e.g. "Plan.TaxRate".' });
  }
  if (value === undefined || value === null) {
    return res.status(400).json({ error: 'A value for the new field is required.' });
  }

  ensureCsv();
  const content = fs.readFileSync(CSV_PATH, 'utf8');
  const [header, ...dataRows] = parseCsv(content);
  const parser = new XMLParser(parserOptions);

  let updated = 0;
  const errors = [];
  const outRows = dataRows.map((r) => {
    if (r.length < 4) return r;
    const [scenarioId, version, date, xml] = r;
    try {
      const structure = parser.parse(xml);
      pruneBlankText(structure);
      addFieldAtPath(structure, pathStr, value);

      const xmlOut = buildXml(structure, []);
      updated++;
      return [scenarioId, version, date, xmlOut];
    } catch (err) {
      errors.push({ scenarioId, error: err.message });
      return r;
    }
  });

  const csvText =
    header.map(csvEscape).join(',') +
    '\r\n' +
    outRows.map((r) => r.map(csvEscape).join(',') + '\r\n').join('');
  fs.writeFileSync(CSV_PATH, csvText, 'utf8');

  res.json({ total: dataRows.length, updated, skipped: errors.length, errors });
});

app.get('/api/rows', (req, res) => {
  ensureCsv();
  const content = fs.readFileSync(CSV_PATH, 'utf8');
  const [, ...body] = parseCsv(content);
  const items = body
    .filter((r) => r.length >= 4)
    .map((r) => ({ scenarioId: r[0], version: r[1], date: r[2], xml: r[3] }));
  res.json({ items: items.slice(-20).reverse() });
});

app.get('/api/csv/download', (req, res) => {
  ensureCsv();
  res.download(CSV_PATH, 'scenarios.csv');
});

ensureCsv();
app.listen(PORT, () => {
  console.log(`XML Scenario Editor running at http://localhost:${PORT}`);
});
