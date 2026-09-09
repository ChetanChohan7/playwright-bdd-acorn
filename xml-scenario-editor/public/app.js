// public/app.js
// Wires up the load -> parse -> edit -> save -> history flow, plus adding
// a field to just the currently loaded document and backfilling a field
// into already-saved rows, against the server's /api/parse, /api/save,
// /api/add-field, /api/backfill-field and /api/rows endpoints.

const xmlFileInput = document.getElementById('xml-file');
const xmlTextArea = document.getElementById('xml-text');
const parseBtn = document.getElementById('parse-btn');
const parseStatus = document.getElementById('parse-status');

const editPanel = document.getElementById('edit-panel');
const scenarioIdInput = document.getElementById('scenario-id');
const versionInput = document.getElementById('version');
const fieldsContainer = document.getElementById('fields-container');
const previewBtn = document.getElementById('preview-btn');
const saveBtn = document.getElementById('save-btn');
const saveStatus = document.getElementById('save-status');
const previewDetails = document.getElementById('preview-details');
const xmlPreview = document.getElementById('xml-preview');

const addFieldPathInput = document.getElementById('add-field-path');
const addFieldValueInput = document.getElementById('add-field-value');
const addFieldBtn = document.getElementById('add-field-btn');
const addFieldStatus = document.getElementById('add-field-status');

const backfillPathInput = document.getElementById('backfill-path');
const backfillSuggestions = document.getElementById('backfill-path-suggestions');
const backfillValueInput = document.getElementById('backfill-value');
const backfillBtn = document.getElementById('backfill-btn');
const backfillStatus = document.getElementById('backfill-status');

const refreshBtn = document.getElementById('refresh-btn');
const rowsTableBody = document.querySelector('#rows-table tbody');

// Holds the last successful /api/parse response so we can rebuild XML on save.
let currentStructure = null;
let currentFields = [];

function setStatus(el, message, kind) {
  el.textContent = message || '';
  el.className = 'status' + (kind ? ` ${kind}` : '');
}

xmlFileInput.addEventListener('change', () => {
  const file = xmlFileInput.files && xmlFileInput.files[0];
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    xmlTextArea.value = String(reader.result || '');
  };
  reader.readAsText(file);
});

parseBtn.addEventListener('click', async () => {
  const xml = xmlTextArea.value;
  setStatus(parseStatus, '', null);

  if (!xml.trim()) {
    setStatus(parseStatus, 'Choose a file or paste XML first.', 'error');
    return;
  }

  parseBtn.disabled = true;
  try {
    const res = await fetch('/api/parse', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ xml }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Failed to parse XML.');

    currentStructure = data.structure;
    currentFields = data.fields;
    renderFields(currentFields);
    populatePathSuggestions(currentFields);

    editPanel.hidden = false;
    previewDetails.hidden = true;
    setStatus(parseStatus, `Parsed ${currentFields.length} field(s).`, 'success');
    setStatus(saveStatus, '', null);
  } catch (err) {
    setStatus(parseStatus, err.message, 'error');
  } finally {
    parseBtn.disabled = false;
  }
});

function renderFields(fields) {
  fieldsContainer.innerHTML = '';

  if (fields.length === 0) {
    fieldsContainer.innerHTML = '<p class="status">No editable text fields were found in this document.</p>';
    return;
  }

  for (const field of fields) {
    const wrap = document.createElement('div');
    wrap.className = 'field-item';

    const label = document.createElement('label');
    label.textContent = field.label;
    label.setAttribute('for', `field-${field.id}`);

    const input = document.createElement('input');
    input.type = 'text';
    input.id = `field-${field.id}`;
    input.value = field.value;
    input.dataset.fieldId = String(field.id);

    wrap.appendChild(label);
    wrap.appendChild(input);
    fieldsContainer.appendChild(wrap);
  }
}

/** Reads the current values out of the rendered inputs into currentFields. */
function collectEditedFields() {
  const inputs = fieldsContainer.querySelectorAll('input[data-field-id]');
  const byId = new Map(currentFields.map((f) => [f.id, f]));
  inputs.forEach((input) => {
    const id = Number(input.dataset.fieldId);
    const field = byId.get(id);
    if (field) field.value = input.value;
  });
  return currentFields;
}

addFieldBtn.addEventListener('click', async () => {
  if (!currentStructure) return;
  setStatus(addFieldStatus, '', null);

  const path = addFieldPathInput.value.trim();
  const value = addFieldValueInput.value;

  if (!path) {
    setStatus(addFieldStatus, 'Enter the field path to add, e.g. Plan.TaxRate.', 'error');
    return;
  }

  // Apply whatever's already been typed into the other fields first, so
  // adding a field doesn't lose in-progress edits.
  const fields = collectEditedFields();

  addFieldBtn.disabled = true;
  try {
    const res = await fetch('/api/add-field', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ structure: currentStructure, fields, path, value }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Failed to add field.');

    currentStructure = data.structure;
    currentFields = data.fields;
    renderFields(currentFields);
    populatePathSuggestions(currentFields);

    addFieldPathInput.value = '';
    addFieldValueInput.value = '';
    setStatus(addFieldStatus, `Added "${path}". Remember to Save to write it to the CSV.`, 'success');
  } catch (err) {
    setStatus(addFieldStatus, err.message, 'error');
  } finally {
    addFieldBtn.disabled = false;
  }
});

previewBtn.addEventListener('click', async () => {
  if (!currentStructure) return;
  const fields = collectEditedFields();

  // Rebuilding XML needs the same parser/builder the server uses, so the
  // preview asks /api/save to build it with dryRun (no CSV row written).
  try {
    previewBtn.disabled = true;
    const res = await fetch('/api/save', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        scenarioId: scenarioIdInput.value || '(preview)',
        version: versionInput.value || '(preview)',
        structure: currentStructure,
        fields,
        dryRun: true,
      }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Failed to build preview.');
    xmlPreview.textContent = data.xml;
    previewDetails.hidden = false;
  } catch (err) {
    setStatus(saveStatus, err.message, 'error');
  } finally {
    previewBtn.disabled = false;
  }
});

saveBtn.addEventListener('click', async () => {
  if (!currentStructure) return;
  const fields = collectEditedFields();
  const scenarioId = scenarioIdInput.value.trim();
  const version = versionInput.value.trim();

  setStatus(saveStatus, '', null);

  if (!scenarioId || !version) {
    setStatus(saveStatus, 'Scenario ID and Version are both required.', 'error');
    return;
  }

  saveBtn.disabled = true;
  try {
    const res = await fetch('/api/save', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ scenarioId, version, structure: currentStructure, fields }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Failed to save.');

    xmlPreview.textContent = data.xml;
    previewDetails.hidden = false;
    setStatus(saveStatus, `Saved as ${scenarioId} v${version} at ${data.date}.`, 'success');
    loadRows();
  } catch (err) {
    setStatus(saveStatus, err.message, 'error');
  } finally {
    saveBtn.disabled = false;
  }
});

/** Converts an internal field path (array) to the dot/@ string syntax. */
function pathToString(pathSegments) {
  return pathSegments
    .map((seg) => {
      if (typeof seg === 'number') return String(seg);
      if (seg.startsWith('@_')) return '@' + seg.slice(2);
      return seg;
    })
    .join('.');
}

// Offers existing field paths as autocomplete suggestions, purely as a
// convenience — the path input works fine for a path that matches
// nothing currently parsed (i.e. a brand new field).
function populatePathSuggestions(fields) {
  backfillSuggestions.innerHTML = '';
  for (const field of fields) {
    const opt = document.createElement('option');
    opt.value = pathToString(field.path);
    backfillSuggestions.appendChild(opt);
  }
}

// If the typed path matches a field from the currently parsed document,
// prefill the value from it (without clobbering something the user
// already typed).
backfillPathInput.addEventListener('input', () => {
  if (backfillValueInput.value.trim() !== '') return;
  const match = currentFields.find((f) => pathToString(f.path) === backfillPathInput.value.trim());
  if (match) backfillValueInput.value = match.value;
});

backfillBtn.addEventListener('click', async () => {
  setStatus(backfillStatus, '', null);

  const path = backfillPathInput.value.trim();
  const value = backfillValueInput.value;

  if (!path) {
    setStatus(backfillStatus, 'Enter the field path to add, e.g. Plan.TaxRate.', 'error');
    return;
  }

  backfillBtn.disabled = true;
  try {
    const res = await fetch('/api/backfill-field', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ path, value }),
    });
    const data = await res.json();
    if (!res.ok) throw new Error(data.error || 'Backfill failed.');

    let message = `Updated ${data.updated} of ${data.total} existing row(s).`;
    if (data.skipped > 0) {
      message += ` ${data.skipped} skipped (couldn't parse their XML).`;
    }
    setStatus(backfillStatus, message, data.skipped > 0 ? 'error' : 'success');
    loadRows();
  } catch (err) {
    setStatus(backfillStatus, err.message, 'error');
  } finally {
    backfillBtn.disabled = false;
  }
});

refreshBtn.addEventListener('click', loadRows);

async function loadRows() {
  try {
    const res = await fetch('/api/rows');
    const data = await res.json();
    rowsTableBody.innerHTML = '';
    for (const row of data.items || []) {
      const tr = document.createElement('tr');
      tr.innerHTML = `
        <td>${escapeHtml(row.scenarioId)}</td>
        <td>${escapeHtml(row.version)}</td>
        <td>${escapeHtml(row.date)}</td>
        <td class="xml-cell" title="${escapeHtml(row.xml)}">${escapeHtml(row.xml)}</td>
      `;
      rowsTableBody.appendChild(tr);
    }
  } catch {
    // Non-fatal: history panel just stays empty/stale.
  }
}

function escapeHtml(str) {
  return String(str ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

loadRows();
