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

    const rawFields = [];
    collectFields(structure, [], rawFields);

    // The XML declaration (<?xml version="1.0"?>) parses into a "?xml"
    // pseudo-node; it's rebuilt automatically and isn't real scenario
    // content, so don't surface it as an editable field.
    const contentFields = rawFields.filter((f) => f.path[0] !== '?xml');

    const fields = contentFields.map((f, i) => ({
      id: i,
      path: f.path,
      label: labelForPath(f.path) || '(root)',
      value: f.value,
    }));

    res.json({ fields, structure });
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

app.post('/api/bulk-save', (req, res) => {
  const { structure, fields, version, scenarioIdPattern, startNumber, count, syncFieldId } = req.body || {};

  if (!structure || !Array.isArray(fields)) {
    return res.status(400).json({ error: 'Missing parsed XML structure/fields — parse an XML document first.' });
  }
  if (!version || !String(version).trim()) {
    return res.status(400).json({ error: 'Version is required.' });
  }
  if (!scenarioIdPattern || !scenarioIdPattern.includes('{n}')) {
    return res.status(400).json({ error: 'Scenario ID pattern must include {n}, e.g. "PS-{n}".' });
  }
  const n = Number(count);
  if (!Number.isInteger(n) || n < 1 || n > 500) {
    return res.status(400).json({ error: 'Count must be a whole number between 1 and 500.' });
  }
  const start = Number.isFinite(Number(startNumber)) ? Number(startNumber) : 1;

  try {
    ensureCsv();
    const date = new Date().toISOString();
    const rows = [];
    const items = [];

    for (let i = 0; i < n; i++) {
      const seq = start + i;
      const scenarioId = scenarioIdPattern.split('{n}').join(String(seq));

      const fieldsCopy = fields.map((f) => ({ ...f }));
      if (syncFieldId !== null && syncFieldId !== undefined && syncFieldId !== '') {
        const target = fieldsCopy.find((f) => f.id === syncFieldId);
        if (target) target.value = scenarioId;
      }

      const xmlOut = buildXml(structure, fieldsCopy);
      rows.push([scenarioId, version, date, xmlOut].map(csvEscape).join(',') + '\r\n');
      items.push({ scenarioId, xml: xmlOut });
    }

    fs.appendFileSync(CSV_PATH, rows.join(''), 'utf8');
    res.json({ date, items });
  } catch (err) {
    res.status(500).json({ error: `Bulk save failed: ${err.message}` });
  }
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
