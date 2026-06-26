(function () {
  initializeSubjectPicker();
  initializeFilePicker();
  initializeChunkingControls();

  if (!window.signalR) {
    return;
  }

  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/document-processing')
    .withAutomaticReconnect()
    .build();

  connection.on('documentProcessingUpdated', (message) => {
    const documentId = message?.documentId || message?.DocumentId;
    if (!documentId) {
      return;
    }

    const row = document.querySelector(`[data-document-row="${documentId}"]`);
    if (!row) {
      return;
    }

    const previousStatus = row.dataset.documentStatus || '';
    const nextStatus = (message.status || message.Status || '').toLowerCase();
    row.dataset.documentStatus = nextStatus;

    updateQueueCount(previousStatus, nextStatus);
    updateStatusBadge(row, message);
    updateViewLink(row, nextStatus);
  });

  connection.on('subjectDeleted', (message) => {
    const subjectId = message?.subjectId || message?.SubjectId;
    if (!subjectId) {
      return;
    }

    removeDeletedSubjectOption(subjectId);
    removeDeletedSubjectRows(subjectId);
    showSubjectSyncMessage(message);
  });

  connection.on('subjectAssigned', (message) => {
    const subjectId = message?.subjectId || message?.SubjectId;
    if (!subjectId) {
      return;
    }

    upsertSubjectOption(message);
    showSubjectAssignedMessage(message);
  });

  connection.on('subjectUpdated', (message) => {
    const subjectId = message?.subjectId || message?.SubjectId;
    if (!subjectId) {
      return;
    }

    upsertSubjectOption(message);
    updateSubjectRows(message);
    showSubjectUpdatedMessage(message);
  });

  connection.start().catch(() => {
    // Server-rendered status remains the fallback when realtime updates are unavailable.
  });

  function updateQueueCount(previousStatus, nextStatus) {
    const counter = document.querySelector('[data-teacher-queue-count]');
    if (!counter || previousStatus === nextStatus) {
      return;
    }

    const current = Number.parseInt(counter.textContent.replace(/,/g, ''), 10);
    if (Number.isNaN(current)) {
      return;
    }

    let next = current;
    if (previousStatus === 'queued' && nextStatus !== 'queued') {
      next = Math.max(0, current - 1);
    } else if (previousStatus !== 'queued' && nextStatus === 'queued') {
      next = current + 1;
    }

    counter.textContent = next.toLocaleString();
  }

  function updateStatusBadge(row, message) {
    const badge = row.querySelector('[data-document-status-badge]');
    const label = row.querySelector('[data-document-status-label]');
    const icon = row.querySelector('[data-document-status-icon]');
    if (!badge || !label) {
      return;
    }

    badge.classList.remove('processed', 'indexing', 'error');
    const statusClass = message.statusClass || message.StatusClass || 'indexing';
    badge.classList.add(statusClass);
    label.textContent = message.statusLabel || message.StatusLabel || message.status || message.Status || 'Pending';

    if (!icon) {
      return;
    }

    if (statusClass === 'indexing') {
      if (icon.tagName.toLowerCase() !== 'span') {
        const replacement = document.createElement('span');
        replacement.className = 'material-symbols-outlined';
        replacement.setAttribute('aria-hidden', 'true');
        replacement.setAttribute('data-document-status-icon', '');
        replacement.textContent = 'sync';
        icon.replaceWith(replacement);
      } else {
        icon.textContent = 'sync';
      }
    } else if (icon.tagName.toLowerCase() !== 'i') {
      const replacement = document.createElement('i');
      replacement.setAttribute('data-document-status-icon', '');
      icon.replaceWith(replacement);
    }
  }

  function updateViewLink(row, status) {
    const link = row.querySelector('[data-document-view-link]');
    if (!link) {
      return;
    }

    const isCompleted = status === 'completed';
    link.classList.toggle('disabled', !isCompleted);
    link.setAttribute('aria-disabled', String(!isCompleted));
  }

  function removeDeletedSubjectOption(subjectId) {
    const subjectInput = document.querySelector('[data-upload-subject-input]');
    const pickerLabel = document.querySelector('[data-subject-picker-label]');
    const option = document.querySelector(`[data-subject-option][data-subject-id="${subjectId}"]`);
    if (!option) {
      return;
    }

    const wasSelected = subjectInput && subjectInput.value === subjectId;
    option.remove();

    if (wasSelected) {
      if (subjectInput) {
        subjectInput.value = '';
      }

      if (pickerLabel) {
        pickerLabel.textContent = 'Choose subject';
      }
    }

    const hasSubjects = document.querySelectorAll('[data-subject-option]').length > 0;
    toggleUploadAvailability(hasSubjects);
  }

  function upsertSubjectOption(message) {
    const list = document.querySelector('[data-subject-picker-list]');
    if (!list) {
      return;
    }

    const subjectId = message?.subjectId || message?.SubjectId;
    if (!subjectId) {
      return;
    }

    const existingOption = list.querySelector(`[data-subject-option][data-subject-id="${subjectId}"]`);
    if (existingOption) {
      existingOption.dataset.subjectLabel = formatSubjectLabel(message);
      existingOption.dataset.subjectCode = message?.subjectCode || message?.SubjectCode || '';
      existingOption.dataset.subjectName = message?.subjectName || message?.SubjectName || '';
      existingOption.innerHTML = `<strong>${escapeHtml(existingOption.dataset.subjectCode)}</strong><span>${escapeHtml(existingOption.dataset.subjectName)}</span>`;
      syncSelectedSubjectLabel(subjectId);
      sortSubjectOptions(list);
      return;
    }

    const option = document.createElement('button');
    option.type = 'button';
    option.className = 'teacher-stitch-subject-option';
    option.setAttribute('data-subject-option', '');
    option.setAttribute('data-subject-id', subjectId);
    option.setAttribute('data-subject-label', formatSubjectLabel(message));
    option.setAttribute('data-subject-code', message?.subjectCode || message?.SubjectCode || '');
    option.setAttribute('data-subject-name', message?.subjectName || message?.SubjectName || '');
    option.innerHTML = `<strong>${escapeHtml(option.dataset.subjectCode)}</strong><span>${escapeHtml(option.dataset.subjectName)}</span>`;
    list.appendChild(option);

    sortSubjectOptions(list);
    toggleUploadAvailability(true);
  }

  function updateSubjectRows(message) {
    const subjectId = message?.subjectId || message?.SubjectId;
    if (!subjectId) {
      return;
    }

    const label = formatSubjectLabel(message);
    const rows = Array.from(document.querySelectorAll(`[data-subject-id="${subjectId}"]`));
    rows.forEach((row) => {
      const subjectCell = row.querySelector('[data-subject-label]');
      if (subjectCell) {
        subjectCell.textContent = label;
      }
    });
  }

  function removeDeletedSubjectRows(subjectId) {
    const rows = Array.from(document.querySelectorAll(`[data-subject-id="${subjectId}"]`));
    rows.forEach((row) => {
      updateQueueCount(row.dataset.documentStatus || '', '');
      row.remove();
    });

    refreshDocumentCounters();
    ensureEmptyStateRows();
  }

  function showSubjectSyncMessage(message) {
    const alert = document.querySelector('[data-subject-sync-message]');
    if (!alert) {
      return;
    }

    const subjectCode = message?.subjectCode || message?.SubjectCode || 'Subject';
    const subjectName = message?.subjectName || message?.SubjectName || '';
    alert.textContent = subjectName
      ? `${subjectCode}: ${subjectName} was removed by an administrator.`
      : `${subjectCode} was removed by an administrator.`;
    alert.hidden = false;
  }

  function showSubjectAssignedMessage(message) {
    const alert = document.querySelector('[data-subject-sync-message]');
    if (!alert) {
      return;
    }

    const subjectCode = message?.subjectCode || message?.SubjectCode || 'Subject';
    const subjectName = message?.subjectName || message?.SubjectName || '';
    alert.textContent = subjectName
      ? `${subjectCode}: ${subjectName} was assigned by an administrator.`
      : `${subjectCode} was assigned by an administrator.`;
    alert.hidden = false;
  }

  function showSubjectUpdatedMessage(message) {
    const alert = document.querySelector('[data-subject-sync-message]');
    if (!alert) {
      return;
    }

    const subjectCode = message?.subjectCode || message?.SubjectCode || 'Subject';
    const subjectName = message?.subjectName || message?.SubjectName || '';
    alert.textContent = subjectName
      ? `${subjectCode}: ${subjectName} was updated by an administrator.`
      : `${subjectCode} was updated by an administrator.`;
    alert.hidden = false;
  }

  function toggleUploadAvailability(hasSubjects) {
    const warning = document.querySelector('[data-no-subject-warning]');
    if (warning) {
      warning.hidden = hasSubjects;
    }

    const subjectPicker = document.querySelector('[data-subject-picker]');
    if (subjectPicker) {
      subjectPicker.classList.toggle('is-disabled', !hasSubjects);
      if (hasSubjects) {
        subjectPicker.removeAttribute('data-disabled');
      } else {
        subjectPicker.setAttribute('data-disabled', 'true');
        subjectPicker.removeAttribute('open');
      }
    }

    document.querySelectorAll('[data-upload-field], [data-upload-submit]').forEach((element) => {
      element.disabled = !hasSubjects;
    });

    if (!hasSubjects) {
      const fileName = document.querySelector('[data-upload-file-name]');
      if (fileName) {
        fileName.textContent = 'No file selected';
      }
    }
  }

  function refreshDocumentCounters() {
    const count = document.querySelectorAll('[data-document-row]').length;
    const formattedCount = count.toLocaleString();

    const totalDocuments = document.querySelector('[data-teacher-total-documents]');
    if (totalDocuments) {
      totalDocuments.textContent = formattedCount;
    }

    document.querySelectorAll('[data-teacher-visible-documents]').forEach((element) => {
      element.textContent = formattedCount;
    });
  }

  function ensureEmptyStateRows() {
    document.querySelectorAll('table').forEach((table) => {
      const tbody = table.querySelector('tbody');
      if (!tbody) {
        return;
      }

      const documentRows = tbody.querySelectorAll('[data-document-row]');
      const existingEmptyRow = tbody.querySelector('[data-generated-empty-row]');
      if (documentRows.length > 0) {
        if (existingEmptyRow) {
          existingEmptyRow.remove();
        }

        return;
      }

      if (existingEmptyRow) {
        return;
      }

      const row = document.createElement('tr');
      row.setAttribute('data-generated-empty-row', '');

      const cell = document.createElement('td');
      cell.colSpan = table.querySelectorAll('thead th').length || 1;
      cell.className = 'teacher-stitch-empty';
      cell.textContent = 'No documents have been uploaded yet.';

      row.appendChild(cell);
      tbody.appendChild(row);
    });
  }

  function sortSubjectOptions(container) {
    const options = Array.from(container.querySelectorAll('[data-subject-option]'));

    options.sort((left, right) => {
      const leftLabel = left.dataset.subjectLabel || '';
      const rightLabel = right.dataset.subjectLabel || '';
      return leftLabel.localeCompare(rightLabel);
    });
    options.forEach((option) => {
      container.appendChild(option);
    });
  }

  function formatSubjectLabel(message) {
    const subjectCode = message?.subjectCode || message?.SubjectCode || '';
    const subjectName = message?.subjectName || message?.SubjectName || '';
    return subjectName ? `${subjectCode}: ${subjectName}` : subjectCode;
  }

  function initializeSubjectPicker() {
    const picker = document.querySelector('[data-subject-picker]');
    const input = document.querySelector('[data-upload-subject-input]');
    const label = document.querySelector('[data-subject-picker-label]');
    if (!picker || !input || !label) {
      return;
    }

    picker.addEventListener('toggle', () => {
      if (picker.dataset.disabled === 'true' && picker.open) {
        picker.removeAttribute('open');
      }
    });

    picker.addEventListener('click', (event) => {
      const option = event.target.closest('[data-subject-option]');
      if (!option) {
        return;
      }

      input.value = option.dataset.subjectId || '';
      label.textContent = option.dataset.subjectLabel || 'Choose subject';
      picker.querySelectorAll('[data-subject-option]').forEach((candidate) => {
        candidate.classList.toggle('is-selected', candidate === option);
      });
      picker.removeAttribute('open');
    });
  }

  function syncSelectedSubjectLabel(subjectId) {
    const input = document.querySelector('[data-upload-subject-input]');
    const label = document.querySelector('[data-subject-picker-label]');
    if (!input || !label || input.value !== subjectId) {
      return;
    }

    const selectedOption = document.querySelector(`[data-subject-option][data-subject-id="${subjectId}"]`);
    if (selectedOption) {
      label.textContent = selectedOption.dataset.subjectLabel || 'Choose subject';
    }
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }

  function initializeFilePicker() {
    const fileInput = document.querySelector('[data-upload-file-input]');
    const fileName = document.querySelector('[data-upload-file-name]');
    const fileTrigger = document.querySelector('[data-upload-file-trigger]');
    if (!fileInput || !fileName || !fileTrigger) {
      return;
    }

    const setFileName = () => {
      const selectedFile = fileInput.files?.[0];
      fileName.textContent = selectedFile ? selectedFile.name : 'No file selected';
    };

    fileTrigger.addEventListener('keydown', (event) => {
      if (fileInput.disabled) {
        return;
      }

      if (event.key === 'Enter' || event.key === ' ') {
        event.preventDefault();
        fileInput.click();
      }
    });
    fileInput.addEventListener('change', setFileName);
    setFileName();
  }

  function initializeChunkingControls() {
    const strategySelect = document.querySelector('[data-chunking-strategy-select]');
    const fixedFields = document.querySelector('[data-fixed-chunking-fields]');
    const fixedSizeInput = document.querySelector('[data-fixed-chunk-size]');
    const fixedOverlapInput = document.querySelector('[data-fixed-chunk-overlap]');
    if (!strategySelect || !fixedFields || !fixedSizeInput || !fixedOverlapInput) {
      return;
    }

    const updateVisibility = () => {
      const isFixed = strategySelect.value === 'fixed_sized';
      fixedFields.hidden = !isFixed;
      fixedSizeInput.disabled = strategySelect.disabled || !isFixed;
      fixedOverlapInput.disabled = strategySelect.disabled || !isFixed;
    };

    strategySelect.addEventListener('change', updateVisibility);
    updateVisibility();
  }
}());
