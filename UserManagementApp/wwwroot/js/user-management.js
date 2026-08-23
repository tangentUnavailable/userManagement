// User management page interactions:
//   - Select all / select none checkbox in the table header
//   - Toolbar buttons enable based on whether rows are selected
//   - AJAX calls for Block, Unblock, Delete, and Delete Unverified
//   - Convert ISO timestamps into relative time strings ("5 minutes ago")
//   - Bootstrap tooltip initialization
//   - Modal-based confirmation dialogs (replaces window.confirm browser alerts)

$(document).ready(function () {
  // --- State that guards against double confirmation / double execution. ---
  // `actionInProgress` is true while the confirmation modal is open OR while an
  // AJAX request is in flight. Any further clicks are ignored, which is what
  // prevents the "double confirmation" bug (two confirm dialogs) and the
  // follow-up "operation failed" toast (the second AJAX request would otherwise
  // hit the UserStatusMiddleware after the user blocked/deleted themselves and
  // get redirected to the login page as HTML instead of JSON).
  var actionInProgress = false;
  var pendingConfirmCallback = null; // callback registered when the modal is shown
  var confirmationModal = null;

  // Grab the anti-forgery token from the hidden form field, falling back
  // to the cookie if this page doesn't have a form on it.
  function getAntiForgeryToken() {
    var token = $("input[name='__RequestVerificationToken']").val();
    if (!token) {
      token = getCookie("__RequestVerificationToken");
    }
    return token;
  }

  function getCookie(name) {
    var match = document.cookie.match(new RegExp("(^| )" + name + "=([^;]+)"));
    return match ? decodeURIComponent(match[2]) : null;
  }

  // Select-all in the header checkbox toggles every row checkbox below it.
  $("#selectAllCheckbox").on("change", function () {
    var isChecked = $(this).is(":checked");
    $(".row-checkbox").prop("checked", isChecked);
    updateToolbarButtons();
  });

  // When a row checkbox changes, update the header checkbox to reflect
  // the new state (all, none, or indeterminate).
  $(document).on("change", ".row-checkbox", function () {
    var total = $(".row-checkbox").length;
    var checkedCount = $(".row-checkbox:checked").length;

    if (checkedCount === 0) {
      $("#selectAllCheckbox")
        .prop("checked", false)
        .prop("indeterminate", false);
    } else if (checkedCount === total) {
      $("#selectAllCheckbox")
        .prop("checked", true)
        .prop("indeterminate", false);
    } else {
      $("#selectAllCheckbox")
        .prop("checked", false)
        .prop("indeterminate", true);
    }

    updateToolbarButtons();
  });

  // Toolbar buttons are always visible but disabled when nothing is selected
  // or while an action is already in progress.
  function updateToolbarButtons() {
    var selectedCount = $(".row-checkbox:checked").length;
    $("#selectionCount").text(selectedCount + " selected");

    var selectionDisabled = selectedCount === 0;
    $("#blockBtn, #unblockBtn, #deleteBtn").prop(
      "disabled",
      selectionDisabled || actionInProgress,
    );
    // "Delete Unverified" needs no selection.
    $("#deleteUnverifiedBtn").prop("disabled", actionInProgress);
  }

  // --- Bootstrap modal confirmation (replaces window.confirm) ---
  function initConfirmationModal() {
    var $modalEl = $("#confirmModal");
    if (!$modalEl.length) {
      return; // not on the user-management page
    }

    confirmationModal = new bootstrap.Modal($modalEl[0], {
      backdrop: "static",
      keyboard: false,
    });

    // The confirm button runs the pending callback exactly once. Guarding on
    // `pendingConfirmCallback` also protects against double-clicking the
    // Confirm button itself.
    $modalEl.find(".btn-confirm").on("click", function () {
      if (!pendingConfirmCallback) {
        return;
      }
      var cb = pendingConfirmCallback;
      pendingConfirmCallback = null;
      $(this).prop("disabled", true);

      confirmationModal.hide();
      if (typeof cb === "function") {
        cb();
      }
    });

    // Whenever the modal closes, if the callback was never run (Cancel button,
    // backdrop click, or Esc), reset the in-flight state so the user can try
    // again. (When the user did confirm, `pendingConfirmCallback` is already
    // null, so we leave `actionInProgress` alone while the AJAX runs.)
    $modalEl.on("hidden.bs.modal", function () {
      if (pendingConfirmCallback !== null) {
        pendingConfirmCallback = null;
        actionInProgress = false;
        updateToolbarButtons();
      }
    });
  }

  // Show the confirmation modal. Returns false (and does nothing) if an action
  // is already in progress, which is what prevents multiple confirmation
  // dialogs from stacking up on a double-click.
  function confirmAction(message, callback, confirmClass, confirmText) {
    if (!confirmationModal || actionInProgress) {
      return false;
    }

    actionInProgress = true;
    pendingConfirmCallback = callback;

    $("#confirmModal .modal-body").text(message);

    var $confirmBtn = $("#confirmModal .btn-confirm");
    $confirmBtn
      .removeClass("btn-danger btn-primary btn-warning")
      .addClass(confirmClass || "btn-danger")
      .text(confirmText || "Confirm")
      .prop("disabled", false);

    confirmationModal.show();
    return true;
  }

  // Shared AJAX helper for the toolbar actions.
  function performAction(url, payload, errorMessage, callback) {
    $.ajax({
      url: url,
      type: "POST",
      headers: { RequestVerificationToken: getAntiForgeryToken() },
      contentType: "application/json",
      data: payload ? JSON.stringify(payload) : null,
      success: function (response) {
        callback(response);
      },
      error: function () {
        showNotification(
          errorMessage || "An error occurred while processing your request.",
          "danger",
        );
        finishAction();
      },
    });
  }

  // Reset the in-flight state after an action finishes without navigating away.
  function finishAction() {
    actionInProgress = false;
    pendingConfirmCallback = null;
    updateToolbarButtons();
  }

  // --- Toolbar button handlers ---

  // Block selected users via AJAX.
  $("#blockBtn").on("click", function () {
    var selectedIds = getSelectedUserIds();
    if (selectedIds.length === 0) {
      return;
    }

    confirmAction(
      "Block " + selectedIds.length + " user(s)? Blocked users cannot log in.",
      function () {
        performAction(
          "/Block",
          { ids: selectedIds },
          "An error occurred while blocking users.",
          function (response) {
            handleActionResponse(response, "block");
          },
        );
      },
      "btn-danger",
      "Block",
    );
  });

  // Unblock selected users via AJAX.
  $("#unblockBtn").on("click", function () {
    var selectedIds = getSelectedUserIds();
    if (selectedIds.length === 0) {
      return;
    }

    confirmAction(
      "Unblock " + selectedIds.length + " user(s)?",
      function () {
        performAction(
          "/Unblock",
          { ids: selectedIds },
          "An error occurred while unblocking users.",
          function (response) {
            handleActionResponse(response, "unblock");
          },
        );
      },
      "btn-primary",
      "Unblock",
    );
  });

  // Hard delete selected users (not a soft delete, this is permanent).
  $("#deleteBtn").on("click", function () {
    var selectedIds = getSelectedUserIds();
    if (selectedIds.length === 0) {
      return;
    }

    confirmAction(
      "Delete " + selectedIds.length + " user(s)? This action cannot be undone.",
      function () {
        performAction(
          "/Delete",
          { ids: selectedIds },
          "An error occurred while deleting users.",
          function (response) {
            handleActionResponse(response, "delete");
          },
        );
      },
      "btn-danger",
      "Delete",
    );
  });

  // Delete all unverified accounts at once.
  $("#deleteUnverifiedBtn").on("click", function () {
    confirmAction(
      "Delete ALL unverified users? This action cannot be undone.",
      function () {
        performAction(
          "/DeleteUnverified",
          null,
          "An error occurred while deleting unverified users.",
          function (response) {
            handleActionResponse(response, "delete-unverified");
          },
        );
      },
      "btn-danger",
      "Delete All",
    );
  });

  // Collect the data-user-id attribute from every checked row.
  function getSelectedUserIds() {
    var ids = [];
    $(".row-checkbox:checked").each(function () {
      ids.push($(this).data("user-id").toString());
    });
    return ids;
  }

  // Process the JSON response from the toolbar actions.
  function handleActionResponse(response, actionType) {
    // A non-object response (e.g. an HTML login page) typically means the
    // server redirected to /Login mid-request because the account was
    // invalidated (blocked/deleted). Route the user to the login page instead
    // of surfacing a confusing "operation failed" message.
    if (
      typeof response !== "object" ||
      response === null ||
      Array.isArray(response)
    ) {
      finishAction();
      window.location.href = "/Login";
      return;
    }

    if (response.success) {
      // When the action touched the currently signed-in user, the
      // UserStatusMiddleware will sign them out on the next request. Rather than
      // a fleeting toast, hand the reason to /Login?notice=... and navigate
      // there so the login page can display it persistently.
      if (response.redirectToLogin) {
        var notice = encodeURIComponent(response.message || "You are being signed out.");
        window.location.href = "/Login?notice=" + notice;
        return;
      }

      showNotification(
        response.message || "Operation completed successfully.",
        "success",
      );

      $(".row-checkbox").prop("checked", false);
      $("#selectAllCheckbox")
        .prop("checked", false)
        .prop("indeterminate", false);
      updateToolbarButtons();

      // Wait a beat so the user sees the notification, then reload the table.
      setTimeout(function () {
        location.reload();
      }, 1000);
    } else {
      showNotification(response.message || "Operation failed.", "danger");
      finishAction();
    }
  }

  // Toast notifications that auto-dismiss after 5 seconds.
  function showNotification(message, type) {
    $("#toast-container").remove();

    var toastClass = "";
    var iconClass = "";
    switch (type) {
      case "success":
        toastClass = "bg-success";
        iconClass = "bi bi-check-circle";
        break;
      case "danger":
        toastClass = "bg-danger";
        iconClass = "bi bi-x-circle";
        break;
      case "warning":
        toastClass = "bg-warning text-dark";
        iconClass = "bi bi-exclamation-triangle";
        break;
      default:
        toastClass = "bg-info text-dark";
        iconClass = "bi bi-info-circle";
    }

    var toastHtml =
      '<div id="toast-container" class="toast-container position-fixed bottom-0 end-0 p-3">' +
      '<div class="toast align-items-center text-white ' +
      toastClass +
      ' border-0" role="alert" aria-live="assertive" aria-atomic="true">' +
      '<div class="d-flex">' +
      '<div class="toast-body">' +
      '<i class="' +
      iconClass +
      ' me-2"></i>' +
      message +
      "</div>" +
      '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>' +
      "</div>" +
      "</div>" +
      "</div>";

    $("body").append(toastHtml);

    var toastEl = new bootstrap.Toast($("#toast-container .toast")[0], {
      delay: 5000,
    });
    toastEl.show();
  }

  // Convert an ISO timestamp into a human readable relative time string.
  function updateRelativeTime() {
    $(".last-seen-cell span[data-last-seen]").each(function () {
      var $el = $(this);
      var isoString = $el.data("last-seen");
      if (!isoString) return;

      var date = new Date(isoString);
      $el.text(formatRelativeTime(date));
    });
  }

  function formatRelativeTime(date) {
    var now = new Date();
    var diffSeconds = Math.floor((now - date) / 1000);

    if (diffSeconds < 0) return "just now";
    if (diffSeconds < 5) return "just now";
    if (diffSeconds < 60) return "less than a minute ago";

    var diffMinutes = Math.floor(diffSeconds / 60);
    if (diffMinutes < 60) {
      return diffMinutes + " minute" + (diffMinutes === 1 ? "" : "s") + " ago";
    }

    var diffHours = Math.floor(diffMinutes / 60);
    if (diffHours < 24) {
      return diffHours + " hour" + (diffHours === 1 ? "" : "s") + " ago";
    }

    var diffDays = Math.floor(diffHours / 24);
    if (diffDays < 7) {
      return diffDays + " day" + (diffDays === 1 ? "" : "s") + " ago";
    }

    var diffWeeks = Math.floor(diffDays / 7);
    if (diffWeeks < 4) {
      return diffWeeks + " week" + (diffWeeks === 1 ? "" : "s") + " ago";
    }

    var diffMonths = Math.floor(diffDays / 30);
    return diffMonths + " month" + (diffMonths === 1 ? "" : "s") + " ago";
  }

  // --- Initialize everything ---
  initConfirmationModal();
  updateToolbarButtons();
  updateRelativeTime();
  setInterval(updateRelativeTime, 30000);

  // Turn on Bootstrap tooltips anywhere they are used.
  var tooltipTriggerList = [].slice.call(
    document.querySelectorAll('[data-bs-toggle="tooltip"]'),
  );
  tooltipTriggerList.map(function (tooltipTriggerEl) {
    return new bootstrap.Tooltip(tooltipTriggerEl);
  });
});
