// User management page interactions:
//   - Select all / select none checkbox in the table header
//   - Toolbar buttons enable based on whether rows are selected
//   - AJAX calls for Block, Unblock, Delete, and Delete Unverified
//   - Convert ISO timestamps into relative time strings ("5 minutes ago")
//   - Bootstrap tooltip initialization

$(document).ready(function() {
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
  $("#selectAllCheckbox").on("change", function() {
    var isChecked = $(this).is(":checked");
    $(".row-checkbox").prop("checked", isChecked);
    updateToolbarButtons();
  });

  // When a row checkbox changes, update the header checkbox to reflect
  // the new state (all, none, or indeterminate).
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

  // Toolbar buttons are always visible but disabled when nothing is selected.
  function updateToolbarButtons() {
    var selectedCount = $(".row-checkbox:checked").length;
    $("#selectionCount").text(selectedCount + " selected");

    $("#blockBtn").prop("disabled", selectedCount === 0);
    $("#unblockBtn").prop("disabled", selectedCount === 0);
    $("#deleteBtn").prop("disabled", selectedCount === 0);
  }

  // Block selected users with a confirmation dialog.
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

  // Unblock selected users.
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

  // Hard delete selected users (not a soft delete, this is permanent).
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

  // Delete all unverified accounts at once.
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

  // Collect the data-user-id attribute from every checked row.
  function getSelectedUserIds() {
    var ids = [];
    $(".row-checkbox:checked").each(function() {
      ids.push($(this).data("user-id").toString());
    });
    return ids;
  }

  // Process the JSON response from the toolbar actions.
  // On success: notify, clear checkboxes, then reload the table.
  function handleActionResponse(response, actionType) {
    if (response && response.success) {
      showNotification(
        response.message || "Operation completed successfully.",
        "success",
      );

      $(".row-checkbox").prop("checked", false);
      $("#selectAllCheckbox")
        .prop("checked", false)
        .prop("indeterminate", false);
      updateToolbarButtons();

      // Wait a beat so the user sees the notification, then reload.
      setTimeout(function() {
        location.reload();
      }, 1000);
    } else {
      showNotification(response?.message || "Operation failed.", "danger");
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
    $(".last-seen-cell span[data-last-seen]").each(function() {
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

  // Refresh relative time on load and every 30 seconds.
  updateRelativeTime();
  setInterval(updateRelativeTime, 30000);

  // Turn on Bootstrap tooltips anywhere they are used.
  var tooltipTriggerList = [].slice.call(
    document.querySelectorAll('[data-bs-toggle="tooltip"]'),
  );
  tooltipTriggerList.map(function(tooltipTriggerEl) {
    return new bootstrap.Tooltip(tooltipTriggerEl);
  });
});
