// Control panel page: starts steps through the local API and follows their output.
const $ = (id) => document.getElementById(id);
const forms = ['csvForm', 'importForm', 'seedForm', 'radarForm', 'iceForm', 'statusForm'];
const stepNames = {
  ValidateCsv: '1. Check CSV', Import: '2. Import', Seed: '3. Seed baselines',
  Radar: '4. Radar compare', Ice: '5. ICE check', SetStatus: '6. Set status'
};
let jobId = null;
let nextLine = 0;
let pollTimer = null;

// Every request carries this header; the server refuses changes without it (see Program.cs).
async function api(path, options = {}) {
  const response = await fetch(path, { ...options, headers: { 'X-Control-Panel': '1', ...(options.headers || {}) } });
  const body = response.headers.get('content-type')?.includes('json') ? await response.json() : null;
  if (!response.ok) {
    throw new Error(body?.error || `The request failed (${response.status}).`);
  }
  return body;
}

function showError(message) {
  $('formError').textContent = message;
  $('formError').hidden = !message;
}

function setBusy(busy) {
  for (const id of forms) {
    $(id).querySelector('button[type="submit"]').disabled = busy;
  }
  $('cancelButton').hidden = !busy;
}

async function refreshState() {
  try {
    const state = await api('/api/state');
    $('settingsPath').textContent = state.localSettings;
    const lines = [
      state.localSettingsFound
        ? '<span class="ok">Settings file found</span>'
        : '<span class="bad">Settings file missing: copy data/import/examples/local-settings.example.json to data/import/local-settings.json</span>',
      state.selectedCsv ? `CSV: ${escapeHtml(state.selectedCsv.name)} (saved as ${escapeHtml(state.selectedCsv.savedAs)})` : 'CSV: none chosen yet',
      state.lastRadarReport ? `Latest Radar report: ${escapeHtml(state.lastRadarReport)}` : 'Latest Radar report: none this session'
    ];
    $('state').innerHTML = lines.map((line) => `<span>${line}</span>`).join('');
    $('useLatestRadarReport').disabled = !state.lastRadarReport;
    if (state.current && state.current !== jobId) {
      follow(state.current);
    }
  } catch (error) {
    $('state').textContent = error.message;
  }
}

function escapeHtml(text) {
  return String(text).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]);
}

function follow(id) {
  jobId = id;
  nextLine = 0;
  $('log').textContent = '';
  $('summary').replaceChildren();
  $('files').replaceChildren();
  setBusy(true);
  clearTimeout(pollTimer);
  poll();
}

async function poll() {
  try {
    const job = await api(`/api/jobs/${encodeURIComponent(jobId)}?from=${nextLine}`);
    $('jobTitle').textContent = `${stepNames[job.kind] || job.kind} · ${job.id}`;
    $('jobStatus').hidden = false;
    $('jobStatus').textContent = job.status;
    $('jobStatus').className = `badge ${job.status}`;
    if (job.lines.length) {
      const log = $('log');
      const atBottom = log.scrollTop + log.clientHeight >= log.scrollHeight - 20;
      log.textContent += job.lines.join('\n') + '\n';
      if (atBottom) log.scrollTop = log.scrollHeight;
    }
    nextLine = job.next;

    if (job.status === 'Running') {
      pollTimer = setTimeout(poll, 1000);
      return;
    }

    $('summary').replaceChildren(...job.summary.map((line) => Object.assign(document.createElement('li'), { textContent: line })));
    $('files').replaceChildren(...job.outputs.filter((output) => output.exists).map((output) => {
      const item = document.createElement('li');
      const link = Object.assign(document.createElement('a'), {
        href: `/api/jobs/${encodeURIComponent(job.id)}/files/${output.index}`, textContent: `${output.label}: ${output.name}`, target: '_blank', rel: 'noopener'
      });
      item.append(link);
      return item;
    }));
    setBusy(false);
    refreshState();
  } catch (error) {
    showError(error.message);
    setBusy(false);
  }
}

async function start(path, body) {
  showError('');
  try {
    const isForm = body instanceof FormData;
    const result = await api(path, {
      method: 'POST',
      body: isForm ? body : JSON.stringify(body),
      headers: isForm ? {} : { 'Content-Type': 'application/json' }
    });
    follow(result.id);
  } catch (error) {
    showError(error.message);
  }
}

const values = (form) => Object.fromEntries(new FormData(form).entries());

$('csvForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/csv', new FormData(event.target));
});
$('importForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/import', values(event.target));
});
$('seedForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/seed', values(event.target));
});
$('radarForm').addEventListener('submit', (event) => {
  event.preventDefault();
  start('/api/radar', values(event.target));
});
$('iceForm').addEventListener('submit', (event) => {
  event.preventDefault();
  const input = values(event.target);
  start('/api/ice', { productCode: input.productCode, scenarioTag: input.scenarioTag, useLatestRadarReport: $('useLatestRadarReport').checked });
});
$('statusForm').addEventListener('submit', (event) => {
  event.preventDefault();
  const form = new FormData(event.target);
  const count = (form.get('scenarioIds') || '').split(/[,;\n]/).filter((id) => id.trim()).length;
  const file = form.get('file');
  if (!file || !file.size) form.delete('file');
  const what = count ? `${count} scenario ID(s)` : 'the IDs in the chosen file';
  const confirmPanel = $('statusForm').querySelector('.confirm');
  if (!confirmPanel) {
    // Ask once on the page before changing the database (browser confirm() boxes aren't used).
    const panel = document.createElement('p');
    panel.className = 'error confirm';
    panel.textContent = `Set ${what} to ${form.get('status')}? Click "Set status" again to confirm.`;
    $('statusForm').querySelector('button[type="submit"]').before(panel);
    return;
  }
  confirmPanel.remove();
  start('/api/set-status', form);
});
$('statusForm').addEventListener('input', () => $('statusForm').querySelector('.confirm')?.remove());

$('cancelButton').addEventListener('click', async () => {
  try {
    await api('/api/jobs/cancel', { method: 'POST' });
  } catch (error) {
    showError(error.message);
  }
});

refreshState();
