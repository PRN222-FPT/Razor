(function () {
  initializeFilePicker();

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
}());
