// =============================================================================
// User Management JavaScript
// <important>
//     Handles all interactive behavior on the user management table page:
//     1. Select-all / deselect-all checkbox in the table header
//     2. Toolbar button enable/disable based on row selection
//     3. AJAX calls for Block / Unblock / Delete / Delete Unverified actions
//     4. Relative time display (e.g., "5 minutes ago", "less than a minute ago")
//     5. Bootstrap tooltip initialization
// </important>
// =============================================================================

$(document).ready(function() {
  // -------------------------------------------------------------------------
  // <nota bene>
  //     Get the anti-forgery token cookie for AJAX POST requests.
  //     ASP.NET Core validates this on every POST to prevent CSRF attacks.
  // </nota bene>
  function getAntiForgeryToken() {
    var token = $("input[name='__RequestVerificationToken']").val();
    if (!token) {
      // <note> Fall back to cookie-based token (for AJAX-only pages) </note>
      token = getCookie("__RequestVerificationToken");
    }
    return token;
  }

  function getCookie(name) {
    var match = document.cookie.match(new RegExp("(^| )" + name + "=([^;]+)"));
    return match ? decodeURIComponent(match[2]) : null;
  }

  // -------------------------------------------------------------------------
  // <important>
  //     Select-all / deselect-all logic.
  //     Clicking the header checkbox selects or deselects ALL row checkboxes.
  //     The header checkbox has NO label (per the task requirement).
  // </important>
  // -------------------------------------------------------------------------
  $("#selectAllCheckbox").on("change", function() {
    var isChecked = $(this).is(":checked");
    $(".row-checkbox").prop("checked", isChecked);
    updateToolbarButtons();
  });

  // <note>
  //     When individual row checkboxes change, update the header checkbox state.
  //     If all are checked → header is checked.
  //     If none are checked → header is unchecked.
  //     If some are checked → header is indeterminate.
  // </note>
  $(document).on("change", ".row-checkbox", function() {
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

  // -------------------------------------------------------------------------
  // <important>
  //     Update toolbar button enabled/disabled state based on selection.
  //     The toolbar itself is ALWAYS visible — only individual buttons toggle.
  // </important>
  // -------------------------------------------------------------------------
  function updateToolbarButtons() {
    var selectedCount = $(".row-checkbox:checked").length;
    $("#selectionCount").text(selectedCount + " selected");

    // Block and Unblock can only act on selected rows
    $("#blockBtn").prop("disabled", selectedCount === 0);
    $("#unblockBtn").prop("disabled", selectedCount === 0);
    $("#deleteBtn").prop("disabled", selectedCount === 0);
  }

  // -------------------------------------------------------------------------
  // <important>
  //     Toolbar button click handlers — each sends an AJAX POST to the
  //     corresponding controller action. The actions are in the toolbar ONLY,
  //     not in individual rows.
  // </important>
  // -------------------------------------------------------------------------

  // <note> Block selected users </note>
  $("#blockBtn").on("click", function() {
    var selectedIds = getSelectedUserIds();
    if (selectedIds.length === 0) return;

    if (
      !confirm(
        "Block " +
        selectedIds.length +
        " user(s)? Blocked users cannot log in.",
      )
    )
      return;

    $.ajax({
      url: "/Block",
      type: "POST",
      headers: { RequestVerificationToken: getAntiForgeryToken() },
      contentType: "application/json",
      data: JSON.stringify({ ids: selectedIds }),
      success: function(response) {
        handleActionResponse(response, "block");
      },
      error: function() {
        showNotification("An error occurred while blocking users.", "danger");
      },
    });
  });

  // <note> Unblock selected users </note>
  $("#unblockBtn").on("click", function() {
    var selectedIds = getSelectedUserIds();
    if (selectedIds.length === 0) return;

    if (!confirm("Unblock " + selectedIds.length + " user(s)?")) return;

    $.ajax({
      url: "/Unblock",
      type: "POST",
      headers: { RequestVerificationToken: getAntiForgeryToken() },
      contentType: "application/json",
      data: JSON.stringify({ ids: selectedIds }),
      success: function(response) {
        handleActionResponse(response, "unblock");
      },
      error: function() {
        showNotification("An error occurred while unblocking users.", "danger");
      },
    });
  });

  // <important> Delete selected users (HARD DELETE — not soft delete) </important>
  $("#deleteBtn").on("click", function() {
    var selectedIds = getSelectedUserIds();
    if (selectedIds.length === 0) return;

    if (
      !confirm(
        "Delete " +
        selectedIds.length +
        " user(s)? This action cannot be undone.",
      )
    )
      return;

    $.ajax({
      url: "/Delete",
      type: "POST",
      headers: { RequestVerificationToken: getAntiForgeryToken() },
      contentType: "application/json",
      data: JSON.stringify({ ids: selectedIds }),
      success: function(response) {
        handleActionResponse(response, "delete");
      },
      error: function() {
        showNotification("An error occurred while deleting users.", "danger");
      },
    });
  });

  // <important> Delete all unverified users (bulk operation, no selection needed) </important>
  $("#deleteUnverifiedBtn").on("click", function() {
    if (!confirm("Delete ALL unverified users? This action cannot be undone."))
      return;

    $.ajax({
      url: "/DeleteUnverified",
      type: "POST",
      headers: { RequestVerificationToken: getAntiForgeryToken() },
      success: function(response) {
        handleActionResponse(response, "delete-unverified");
      },
      error: function() {
        showNotification(
          "An error occurred while deleting unverified users.",
          "danger",
        );
      },
    });
  });

  // -------------------------------------------------------------------------
  // <nota bene>
  //     Helper: Get the IDs of all selected (checked) row checkboxes.
  //     Returns an array of GUID strings.
  // </nota bene>
  function getSelectedUserIds() {
    var ids = [];
    $(".row-checkbox:checked").each(function() {
      ids.push($(this).data("user-id").toString());
    });
    return ids;
  }

  // -------------------------------------------------------------------------
  // <important>
  //     Handle the JSON response from toolbar actions.
  //     On success: show notification, deselect rows, refresh the table.
  //     On failure: show error notification.
  // </important>
  function handleActionResponse(response, actionType) {
    if (response && response.success) {
      showNotification(
        response.message || "Operation completed successfully.",
        "success",
      );

      // <note> Deselect all rows after a successful action </note>
      $(".row-checkbox").prop("checked", false);
      $("#selectAllCheckbox")
        .prop("checked", false)
        .prop("indeterminate", false);
      updateToolbarButtons();

      // <nota bene> Reload the table to reflect the changes (new data, sorting) </nota bene>
      setTimeout(function() {
        location.reload();
      }, 1000);
    } else {
      showNotification(response?.message || "Operation failed.", "danger");
    }
  }

  // -------------------------------------------------------------------------
  // <important>
  //     Status messages — shown as Bootstrap toast notifications.
  // </important>
  // -------------------------------------------------------------------------
  function showNotification(message, type) {
    // <note> Remove any existing toast to avoid stacking </note>
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

    // <important> Show the toast and auto-dismiss after 5 seconds </important>
    var toastEl = new bootstrap.Toast($("#toast-container .toast")[0], {
      delay: 5000,
    });
    toastEl.show();
  }

  // -------------------------------------------------------------------------
  // <important>
  //     Relative time display — converts ISO timestamps to human-readable
  //     relative time (e.g., "5 minutes ago", "less than a minute ago",
  //     "3 weeks ago", "2 hours ago").
  // </important>
  // -------------------------------------------------------------------------
  function updateRelativeTime() {
    $(".last-seen-cell span[data-last-seen]").each(function() {
      var $el = $(this);
      var isoString = $el.data("last-seen");
      if (!isoString) return;

      var date = new Date(isoString);
      $el.text(formatRelativeTime(date));
    });
  }

  // <note>
  //     Format a Date as relative time string.
  //     Examples: "less than a minute ago", "5 minutes ago",
  //     "2 hours ago", "3 days ago", "just now"
  // </note>
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

  // <important>
  //     Update relative time every 30 seconds and on initial load.
  //     This keeps the display current without requiring a page refresh.
  // </important>
  updateRelativeTime();
  setInterval(updateRelativeTime, 30000);

  // -------------------------------------------------------------------------
  // <note>
  //     Initialize Bootstrap tooltips on toolbar buttons and any
  //     other elements with data-bs-toggle="tooltip".
  // </note>
  var tooltipTriggerList = [].slice.call(
    document.querySelectorAll('[data-bs-toggle="tooltip"]'),
  );
  tooltipTriggerList.map(function(tooltipTriggerEl) {
    return new bootstrap.Tooltip(tooltipTriggerEl);
  });
});
