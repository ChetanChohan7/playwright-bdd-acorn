// Shared logic used by ui/index.html and the test suite in scenario-loader.test.js.
//
// Loaded two ways: as a plain <script src="scenario-loader.js"> in the browser (where
// these become globals, same as if they'd stayed inline in index.html — deliberately not
// an ES module, so the page keeps its "open the file directly, no build step" property),
// and via require() from Node tests. The module.exports guard at the bottom is a no-op in
// the browser (module is undefined there) and is what makes the second case work.
//
// Everything here is either pure JS or touches only standard DOM APIs (Node, XMLSerializer)
// available via jsdom in the test environment — nothing here reaches for
// File System Access API, IndexedDB, or any other browser-only surface, which is what
// keeps it usable from plain Node.

// ---------- shared element-path helpers (used by section 2's parent & condition dropdowns) ----------
function getRootPath(el) {
  return '/' + el.tagName;
}

function collectPaths(el, path, out) {
  out.push(path);
  Array.from(el.children).forEach((child) => collectPaths(child, path + '/' + child.tagName, out));
}

function isWhitespaceWithNewline(node) {
  return node.nodeType === 3 /* Node.TEXT_NODE */ && node.nodeValue.includes('\n') && /^\s*$/.test(node.nodeValue);
}

// The whitespace text node parent already uses between its existing children,
// e.g. "\n    " — or null if the document has no such indentation to copy
// (e.g. it was originally written on one line).
function findIndentText(parent) {
  const indentNode = Array.from(parent.childNodes).find(isWhitespaceWithNewline);
  return indentNode ? indentNode.nodeValue : null;
}

// Appends newEl to parent while preserving indentation: copies the whitespace
// text node the file already uses between siblings before the new element, and
// inserts both ahead of the trailing whitespace that precedes the closing tag
// (so that whitespace still separates the new element from it). Falls back to
// a plain append if the document has no such whitespace to copy.
function appendChildWithIndent(parent, newEl) {
  const indentText = findIndentText(parent);
  if (!indentText) {
    parent.appendChild(newEl);
    return;
  }

  const lastChild = parent.lastChild;
  const trailingWhitespace = lastChild && isWhitespaceWithNewline(lastChild) ? lastChild : null;

  const doc = parent.ownerDocument || parent;
  if (trailingWhitespace) {
    parent.insertBefore(doc.createTextNode(indentText), trailingWhitespace);
    parent.insertBefore(newEl, trailingWhitespace);
  } else {
    parent.appendChild(doc.createTextNode(indentText));
    parent.appendChild(newEl);
  }
}

function serializeXmlDoc(doc) {
  let text = new XMLSerializer().serializeToString(doc);
  if (!text.startsWith('<?xml')) text = '<?xml version="1.0"?>\n' + text;
  return text;
}

// ---------- CSV parsing / serializing (RFC4180) ----------
function parseCsv(text) {
  const records = [];
  let i = 0;
  const len = text.length;

  function parseField() {
    let field = '';
    if (text[i] === '"') {
      i++;
      while (i < len) {
        if (text[i] === '"') {
          if (text[i + 1] === '"') { field += '"'; i += 2; }
          else { i++; break; }
        } else {
          field += text[i]; i++;
        }
      }
    } else {
      while (i < len && text[i] !== ',' && text[i] !== '\n' && text[i] !== '\r') {
        field += text[i]; i++;
      }
    }
    return field;
  }

  function parseRecord() {
    const record = [parseField()];
    while (text[i] === ',') { i++; record.push(parseField()); }
    if (text[i] === '\r') i++;
    if (text[i] === '\n') i++;
    return record;
  }

  while (i < len) records.push(parseRecord());
  while (records.length && records[records.length - 1].length === 1 && records[records.length - 1][0] === '') {
    records.pop();
  }

  const [, ...body] = records; // drop header row
  return body.map((r) => ({ scenario_id: r[0] ?? '', xml: r[1] ?? '' }));
}

function csvEscape(field) {
  return '"' + String(field).replace(/"/g, '""') + '"';
}

function serializeCsv(rows) {
  const lines = ['scenario_id,xml'];
  rows.forEach((row) => lines.push(`${csvEscape(row.scenario_id)},${csvEscape(row.xml)}`));
  return lines.join('\n') + '\n';
}

// ---------- versioned filenames ----------
// scenarios-20260914-153045-123.csv — sortable, Windows-filename-safe (no colons),
// millisecond suffix to make same-second collisions (e.g. rapid successive edits)
// vanishingly unlikely rather than silently overwriting.
function timestampForFilename(date = new Date()) {
  const pad = (n, len = 2) => String(n).padStart(len, '0');
  return `${date.getFullYear()}${pad(date.getMonth() + 1)}${pad(date.getDate())}` +
    `-${pad(date.getHours())}${pad(date.getMinutes())}${pad(date.getSeconds())}-${pad(date.getMilliseconds(), 3)}`;
}

// Strips ".csv" and any existing "-YYYYMMDD-HHMMSS-mmm" version suffix, so re-saving
// an already-versioned file gets a fresh timestamp instead of a chained one.
function deriveBaseName(filename) {
  return filename.replace(/\.csv$/i, '').replace(/-\d{8}-\d{6}-\d{3}$/, '') || 'scenarios';
}

if (typeof module !== 'undefined' && module.exports) {
  module.exports = {
    getRootPath,
    collectPaths,
    isWhitespaceWithNewline,
    findIndentText,
    appendChildWithIndent,
    serializeXmlDoc,
    parseCsv,
    csvEscape,
    serializeCsv,
    timestampForFilename,
    deriveBaseName,
  };
}
