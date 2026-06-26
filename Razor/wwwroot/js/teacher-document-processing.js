(function () {
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

    addAssignedSubjectOption(message);
    showSubjectAssignedMessage(message);
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
    const select = document.querySelector('[data-upload-subject-select]');
    if (!select) {
      return;
    }

    const option = select.querySelector(`option[value="${subjectId}"]`);
    if (!option) {
      return;
    }

    const wasSelected = select.value === subjectId;
    option.remove();

    if (wasSelected) {
      select.value = '';
    }

    const hasSubjects = Array.from(select.options).some((candidate) => candidate.value);
    toggleUploadAvailability(hasSubjects);
  }

  function addAssignedSubjectOption(message) {
    const select = document.querySelector('[data-upload-subject-select]');
    if (!select) {
      return;
    }

    const subjectId = message?.subjectId || message?.SubjectId;
    if (!subjectId || select.querySelector(`option[value="${subjectId}"]`)) {
      return;
    }

    const subjectCode = message?.subjectCode || message?.SubjectCode || '';
    const subjectName = message?.subjectName || message?.SubjectName || '';

    const option = document.createElement('option');
    option.value = subjectId;
    option.textContent = `${subjectCode}: ${subjectName}`;

    const placeholder = select.querySelector('option[value=""]');
    if (placeholder?.nextSibling) {
      select.insertBefore(option, placeholder.nextSibling);
    } else {
      select.appendChild(option);
    }

    sortSubjectOptions(select);
    toggleUploadAvailability(true);
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

  function toggleUploadAvailability(hasSubjects) {
    const warning = document.querySelector('[data-no-subject-warning]');
    if (warning) {
      warning.hidden = hasSubjects;
    }

    const subjectSelect = document.querySelector('[data-upload-subject-select]');
    if (subjectSelect) {
      subjectSelect.disabled = !hasSubjects;
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

  function sortSubjectOptions(select) {
    const options = Array.from(select.querySelectorAll('option'))
      .filter((option) => option.value);

    options.sort((left, right) => left.textContent.localeCompare(right.textContent));
    options.forEach((option) => {
      select.appendChild(option);
    });
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
