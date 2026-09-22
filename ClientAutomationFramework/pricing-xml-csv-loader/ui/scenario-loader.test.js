const { test, describe, before } = require('node:test');
const assert = require('node:assert/strict');
const { JSDOM } = require('jsdom');

const {
  getRootPath,
  collectPaths,
  appendChildWithIndent,
  serializeXmlDoc,
  parseCsv,
  csvEscape,
  serializeCsv,
  timestampForFilename,
  deriveBaseName,
} = require('./scenario-loader.js');

// scenario-loader.js expects DOMParser/XMLSerializer as globals, same as it gets them from
// the browser's own window when loaded via <script src>.
before(() => {
  const dom = new JSDOM();
  global.DOMParser = dom.window.DOMParser;
  global.XMLSerializer = dom.window.XMLSerializer;
});

describe('parseCsv', () => {
  test('parses a simple CSV and drops the header row', () => {
    const rows = parseCsv('scenario_id,xml\na,<a/>\nb,<b/>\n');
    assert.deepEqual(rows, [
      { scenario_id: 'a', xml: '<a/>' },
      { scenario_id: 'b', xml: '<b/>' },
    ]);
  });

  test('returns an empty array for a header-only CSV', () => {
    assert.deepEqual(parseCsv('scenario_id,xml\n'), []);
  });

  test('unescapes doubled quotes inside a quoted field', () => {
    const rows = parseCsv('scenario_id,xml\na,"<x y=""1""/>"\n');
    assert.equal(rows[0].xml, '<x y="1"/>');
  });

  test('handles a quoted field spanning multiple lines', () => {
    const rows = parseCsv('scenario_id,xml\na,"line one\nline two"\n');
    assert.equal(rows[0].xml, 'line one\nline two');
  });

  test('tolerates a missing trailing newline', () => {
    const rows = parseCsv('scenario_id,xml\na,<a/>');
    assert.deepEqual(rows, [{ scenario_id: 'a', xml: '<a/>' }]);
  });
});

describe('csvEscape', () => {
  test('always wraps the value in quotes', () => {
    assert.equal(csvEscape('plain'), '"plain"');
  });

  test('doubles embedded quotes', () => {
    assert.equal(csvEscape('a"b'), '"a""b"');
  });
});

describe('serializeCsv + parseCsv round-trip', () => {
  test('rows with commas, quotes and newlines survive a full round-trip', () => {
    const original = [
      { scenario_id: 'scenario-001', xml: '<a b="1,2"/>' },
      { scenario_id: 'scenario-002', xml: '<a>\n  <b>quote:"here"</b>\n</a>' },
    ];
    const roundTripped = parseCsv(serializeCsv(original));
    assert.deepEqual(roundTripped, original);
  });

  test('serializeCsv writes the expected header', () => {
    const csv = serializeCsv([{ scenario_id: 'a', xml: '<a/>' }]);
    assert.ok(csv.startsWith('scenario_id,xml\n'));
  });
});

describe('timestampForFilename', () => {
  test('formats and zero-pads a known date', () => {
    // Sep 15 2026, 09:05:03.007 — month is 0-indexed in the Date constructor.
    const d = new Date(2026, 8, 15, 9, 5, 3, 7);
    assert.equal(timestampForFilename(d), '20260915-090503-007');
  });

  test('zero-pads single-digit month and day too', () => {
    const d = new Date(2026, 0, 5, 0, 0, 0, 0);
    assert.equal(timestampForFilename(d), '20260105-000000-000');
  });
});

describe('deriveBaseName', () => {
  test('strips the .csv extension', () => {
    assert.equal(deriveBaseName('scenarios.csv'), 'scenarios');
  });

  test('strips an existing version suffix so re-saves do not chain', () => {
    assert.equal(deriveBaseName('scenarios-20260915-090503-007.csv'), 'scenarios');
  });

  test('preserves a custom base name', () => {
    assert.equal(deriveBaseName('my-export-20260915-090503-007.csv'), 'my-export');
  });

  test('falls back to "scenarios" when nothing is left', () => {
    assert.equal(deriveBaseName('.csv'), 'scenarios');
  });
});

describe('getRootPath + collectPaths (XML structure discovery)', () => {
  test('collects every element path in document order', () => {
    const doc = new DOMParser().parseFromString(
      '<catalog><book><title>T</title></book></catalog>',
      'application/xml',
    );
    const paths = [];
    collectPaths(doc.documentElement, getRootPath(doc.documentElement), paths);
    assert.deepEqual(paths, ['/catalog', '/catalog/book', '/catalog/book/title']);
  });
});

describe('appendChildWithIndent', () => {
  test('matches the existing indentation when the document already has some', () => {
    const doc = new DOMParser().parseFromString(
      '<?xml version="1.0"?>\n<catalog>\n  <owner>Chetan</owner>\n</catalog>\n',
      'application/xml',
    );
    const parent = doc.documentElement;
    const book = doc.createElement('book');
    book.textContent = 'x';
    appendChildWithIndent(parent, book);

    const xml = serializeXmlDoc(doc);
    assert.equal(
      xml,
      '<?xml version="1.0"?>\n<catalog>\n  <owner>Chetan</owner>\n  <book>x</book>\n</catalog>',
    );
  });

  test('falls back to a plain append when the document has no indentation to copy', () => {
    const doc = new DOMParser().parseFromString('<catalog><owner>Chetan</owner></catalog>', 'application/xml');
    const parent = doc.documentElement;
    const book = doc.createElement('book');
    appendChildWithIndent(parent, book);

    assert.equal(parent.lastElementChild.tagName, 'book');
    assert.equal(parent.children.length, 2);
  });
});

describe('serializeXmlDoc', () => {
  test('output always starts with an XML declaration', () => {
    const doc = new DOMParser().parseFromString('<a/>', 'application/xml');
    const xml = serializeXmlDoc(doc);
    assert.ok(xml.startsWith('<?xml'));
    assert.ok(xml.includes('<a/>'));
  });
});
